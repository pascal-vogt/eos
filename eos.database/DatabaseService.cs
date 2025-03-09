namespace eos.core.database
{
  using System;
  using System.Collections.Generic;
  using System.Data.SqlClient;
  using System.Globalization;
  using System.IO;
  using System.Linq;
  using System.Text;
  using System.Text.Json;
  using System.Threading.Tasks;

  public class DatabaseService
  {
    public string ConnectionString { get; set; }

    /// <summary>
    /// Lists all tables in the database. Useful for building a new profile
    /// </summary>
    /// <returns></returns>
    private async Task<List<TableDescriptor>> ListTables()
    {
      var results = new List<TableDescriptor>();

      await using var conn = new SqlConnection(this.ConnectionString);
      const string query = @"select schema_name(t.schema_id) s, t.name t from sys.tables t order by s, t;";
      var cmd = new SqlCommand(query, conn);
      conn.Open();
      var dataReader = await cmd.ExecuteReaderAsync();

      if (dataReader.HasRows)
      {
        while (await dataReader.ReadAsync())
        {
          string schema = dataReader.GetString(0);
          string table = dataReader.GetString(1);
          results.Add(new TableDescriptor { Name = table, Schema = schema });
        }
      }

      await dataReader.CloseAsync();
      conn.Close();

      return results;
    }

    /// <summary>
    /// List all foreign keys in the database (useful for building a profile and following links from table to
    /// table)
    /// </summary>
    /// <returns></returns>
    private async Task<List<ForeignKeyDescriptor>> ListForeignKeys()
    {
      var results = new List<ForeignKeyDescriptor>();

      using (var conn = new SqlConnection(this.ConnectionString))
      {
        const string query =
          @"
select
	schema_name(st.schema_id),
    st.name,
	sc.name,
	schema_name(tt.schema_id),
    tt.name,
	tc.name
from
    sys.foreign_key_columns as fk
inner join sys.tables as st on fk.parent_object_id = st.object_id
inner join sys.columns as sc on fk.parent_object_id = sc.object_id and fk.parent_column_id = sc.column_id
inner join sys.tables as tt on fk.referenced_object_id = tt.object_id
inner join sys.columns as tc on fk.referenced_object_id = tc.object_id and fk.referenced_column_id = tc.column_id
;";
        var cmd = new SqlCommand(query, conn);
        conn.Open();
        var dataReader = await cmd.ExecuteReaderAsync();

        if (dataReader.HasRows)
        {
          while (await dataReader.ReadAsync())
          {
            string sourceSchema = dataReader.GetString(0);
            string sourceTable = dataReader.GetString(1);
            string sourceColumn = dataReader.GetString(2);
            string targetSchema = dataReader.GetString(3);
            string targetTable = dataReader.GetString(4);
            string targetColumn = dataReader.GetString(5);
            results.Add(
              new ForeignKeyDescriptor
              {
                Source = new KeyDescriptor
                {
                  ColumnName = sourceColumn,
                  TableDescriptor = new TableDescriptor { Name = sourceTable, Schema = sourceSchema },
                },
                Target = new KeyDescriptor
                {
                  ColumnName = targetColumn,
                  TableDescriptor = new TableDescriptor { Name = targetTable, Schema = targetSchema },
                },
              }
            );
          }
        }

        await dataReader.CloseAsync();
        conn.Close();

        return results;
      }
    }

    /// <summary>
    /// Locate the primary key name of a table
    /// </summary>
    /// <param name="tableDescriptor"></param>
    /// <returns></returns>
    private async Task<string> GetPrimaryKey(TableDescriptor tableDescriptor)
    {
      await using var conn = new SqlConnection(this.ConnectionString);
      string result = string.Empty;

      string query =
        @"
SELECT
    column_name
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS TC

INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS KU
    ON TC.CONSTRAINT_TYPE = 'PRIMARY KEY'
    AND TC.CONSTRAINT_NAME = KU.CONSTRAINT_NAME
    AND KU.table_name=@table and KU.TABLE_SCHEMA = @schema

ORDER BY
     KU.TABLE_NAME
    ,KU.ORDINAL_POSITION
;";
      var cmd = new SqlCommand(query, conn);
      cmd.Parameters.Add(new SqlParameter("table", tableDescriptor.Name));
      cmd.Parameters.Add(new SqlParameter("schema", tableDescriptor.Schema));

      conn.Open();
      var dataReader = await cmd.ExecuteReaderAsync();

      if (dataReader.HasRows)
      {
        if (await dataReader.ReadAsync())
        {
          result = dataReader.GetString(0);
        }
      }

      await dataReader.CloseAsync();
      conn.Close();

      return result;
    }

    /// <summary>
    /// List all columns in a table (useful for querying and writing inserts for that table)
    /// </summary>
    /// <param name="tableDescriptor"></param>
    /// <returns></returns>
    private async Task<List<ColumnDescriptor>> ListColumns(TableDescriptor tableDescriptor)
    {
      var results = new List<ColumnDescriptor>();

      await using var conn = new SqlConnection(this.ConnectionString);
      string query =
        @"
select COLUMN_NAME, DATA_TYPE
from INFORMATION_SCHEMA.COLUMNS IC
where TABLE_NAME = @table and TABLE_SCHEMA = @schema;
;";
      var cmd = new SqlCommand(query, conn);
      cmd.Parameters.Add(new SqlParameter("table", tableDescriptor.Name));
      cmd.Parameters.Add(new SqlParameter("schema", tableDescriptor.Schema));

      conn.Open();
      var dataReader = await cmd.ExecuteReaderAsync();

      if (dataReader.HasRows)
      {
        while (await dataReader.ReadAsync())
        {
          string columnName = dataReader.GetString(0);
          string columnType = dataReader.GetString(1);
          results.Add(new ColumnDescriptor { Name = columnName, Type = columnType });
        }
      }

      await dataReader.CloseAsync();
      conn.Close();

      return results;
    }

    /// <summary>
    /// Collects all data needed to export INSERTs later on
    /// </summary>
    /// <param name="profilePath"></param>
    /// <param name="id"></param>
    /// <param name="dummyFiles"></param>
    /// <returns></returns>
    private async Task<InsertsData> CollectExportData(string profilePath, string id, bool dummyFiles)
    {
      var data = new InsertsData
      {
        Inserts = new List<InsertInfo>(),
        Profile = await this.ReadProfile(profilePath),
        IdHasBeenDefined = new HashSet<string>(),
        IdToVariable = new Dictionary<string, string>(),
        RowsCountPerTable = new Dictionary<string, long>(),
      };
      data.MainTable = data.Profile.Tables.Where(o => o.IsEntryPoint).Select(o => o.Name).SingleOrDefault();
      await CollectExportDataImplementation(TableDescriptor.FromString(data.MainTable), id, dummyFiles, data);
      return data;
    }

    /// <summary>
    /// Returns a replacement query for an ID of a table using some lookup column (useful for tables that behave
    /// like enums and are not meant to be copied)
    /// </summary>
    /// <param name="table"></param>
    /// <param name="idProperty"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    private async Task<string> GetLookupSql(Table table, string idProperty, string id)
    {
      await using var conn = new SqlConnection(this.ConnectionString);
      string query = $"select {table.LookupProperty} from {table.Name} where [{idProperty}] = @id;";
      var cmd = new SqlCommand(query, conn);
      cmd.Parameters.Add(new SqlParameter("id", id));

      conn.Open();
      var dataReader = await cmd.ExecuteReaderAsync();

      string substitute = id;
      if (dataReader.HasRows)
      {
        while (await dataReader.ReadAsync())
        {
          string lookupColumnValue = dataReader.GetString(0);
          substitute = $"(select {idProperty} from {table.Name} where {table.LookupProperty} = '{lookupColumnValue}')";
        }
      }

      await dataReader.CloseAsync();
      conn.Close();

      return substitute;
    }

    /// <summary>
    /// Gets a new or existing variable name. Subsequent calls with the same parameters will always return the same
    /// variable name.
    /// </summary>
    /// <param name="fullyQualifiedTableName"></param>
    /// <param name="id"></param>
    /// <param name="data"></param>
    /// <param name="isBeingDefined"></param>
    /// <returns></returns>
    private string GetVariableName(TableDescriptor tableDescriptor, string id, InsertsData data, bool isBeingDefined)
    {
      string table = tableDescriptor.Name;
      string variableName;
      if (!data.IdToVariable.TryGetValue(id, out variableName))
      {
        if (!data.RowsCountPerTable.ContainsKey(table))
        {
          data.RowsCountPerTable[table] = 0;
        }

        variableName = $"@{table.Substring(0, 1).ToLower()}{table.Substring(1)}{data.RowsCountPerTable[table]}";
        ++data.RowsCountPerTable[table];
        data.IdToVariable.Add(id, variableName);
      }

      if (isBeingDefined)
      {
        data.IdHasBeenDefined.Add(id);
      }

      return variableName;
    }

    /// <summary>
    /// Collects all data needed for generating INSERTs.
    /// </summary>
    /// <param name="table"></param>
    /// <param name="id"></param>
    /// <param name="dummyFiles"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private async Task CollectExportDataImplementation(TableDescriptor tableDescriptor, string id, bool dummyFiles, InsertsData data)
    {
      // more than one reference might point to this, let's just visit it once
      if (data.IdHasBeenDefined.Contains(id))
      {
        return;
      }

      string table = tableDescriptor.ToString();
      string primaryKeyVariableName = GetVariableName(tableDescriptor, id, data, true);
      string pkColumn = await this.GetPrimaryKey(tableDescriptor);
      var foreignKeys = await this.ListForeignKeys();
      var columns = await this.ListColumns(tableDescriptor);
      string columnsForSql = string.Join(',', columns.Select(c => $"[{c.Name}]"));
      var referencedVariables = new List<string>();

      var outboundForeignKeys = foreignKeys.Where(o => o.Source.TableDescriptor.ToString() == table).ToDictionary(o => o.Source.ColumnName, o => o);

      await using (var conn = new SqlConnection(this.ConnectionString))
      {
        string query = $"select {columnsForSql} from {table} where [{pkColumn}] = @id";
        var cmd = new SqlCommand(query, conn);
        cmd.Parameters.Add(new SqlParameter("id", id));

        conn.Open();
        var dataReader = await cmd.ExecuteReaderAsync();

        if (dataReader.HasRows)
        {
          while (await dataReader.ReadAsync())
          {
            var sb = new StringBuilder();
            sb.Append($"INSERT INTO {table} (");
            int index = 0;
            foreach (var column in columns)
            {
              if (index > 0)
              {
                sb.Append(", ");
              }

              sb.Append($"[{column.Name}]");
              ++index;
            }

            sb.Append(")\n  VALUES (");

            index = 0;
            foreach (var column in columns)
            {
              if (index > 0)
              {
                sb.Append(", ");
              }

              if (dataReader.IsDBNull(index))
              {
                sb.Append("null");
                ++index;
                continue;
              }

              if (column.Name == pkColumn && data.IdToVariable.TryGetValue(dataReader.GetString(index), out string pKey))
              {
                sb.Append(pKey);
                ++index;
                continue;
              }

              switch (column.Type)
              {
                case "nvarchar":
                case "varchar":
                  string stringValue = dataReader.GetValue(index).ToString();
                  bool hasForeignKeyToExportableTable = outboundForeignKeys.TryGetValue(column.Name, out var foreignKey);
                  if (
                    hasForeignKeyToExportableTable && data.Profile.Tables.Any(o => o.Name == foreignKey.Target.TableDescriptor.ToString() && o.Export)
                  )
                  {
                    bool isFollowingForeignKeyOk = data.Profile.ForeignKeys.Any(o =>
                      o.Name == foreignKey.Source.ToString() && o.Follow && o.GoToParent
                    );
                    if (isFollowingForeignKeyOk)
                    {
                      await CollectExportDataImplementation(foreignKey.Target.TableDescriptor, stringValue, dummyFiles, data);
                    }

                    // the ID needs to be replaced by the corresponding variable in order to make sure
                    // we won't link to old data
                    string variableName = GetVariableName(foreignKey.Target.TableDescriptor, stringValue, data, false);
                    referencedVariables.Add(variableName);
                    sb.Append(variableName);
                  }
                  else
                  {
                    bool replaced = false;
                    if (hasForeignKeyToExportableTable)
                    {
                      // IDs that link to tables that we don't generate might still change in another
                      // database. if the config defines a property usable for lookups we need to resolve
                      // the corresponding value and replace the ID with a nested query
                      var referencedTable = data.Profile.Tables.Single(o => o.Name == foreignKey.Target.TableDescriptor.ToString());
                      if (referencedTable.LookupProperty != null)
                      {
                        string replacementForId = await GetLookupSql(referencedTable, foreignKey.Target.ColumnName, stringValue);
                        sb.Append(replacementForId);
                        replaced = true;
                      }
                    }

                    // TODO: we will need something better for escaping
                    if (!replaced)
                    {
                      sb.Append($"'{stringValue.Replace("'", "''")}'");
                    }
                  }

                  break;
                case "bigint":
                  long bigIntValue = dataReader.GetInt64(index);
                  sb.Append(bigIntValue.ToString(CultureInfo.InvariantCulture));
                  break;
                case "decimal":
                  decimal decimalValue = dataReader.GetDecimal(index);
                  sb.Append(decimalValue.ToString(CultureInfo.InvariantCulture));
                  break;
                case "varbinary":
                  var varbinaryValue = dataReader.GetSqlBinary(index);
                  string content = "0x" + string.Concat(Array.ConvertAll(varbinaryValue.Value, x => x.ToString("X2")));
                  if (dummyFiles)
                  {
                    // JPEG
                    if (content.StartsWith("0xFFD8"))
                    {
                      // 10x10 pixels, green
                      content =
                        "0xFFD8FFE000104A46494600010101012C012C0000FFFE00134372656174656420776974682047494D50FFE202B04943435F50524F46494C45000101000002A06C636D73043000006D6E74725247422058595A2007E500080012000C000A0015616373704D5346540000000000000000000000000000000000000000000000000000F6D6000100000000D32D6C636D7300000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000D64657363000001200000004063707274000001600000003677747074000001980000001463686164000001AC0000002C7258595A000001D8000000146258595A000001EC000000146758595A00000200000000147254524300000214000000206754524300000214000000206254524300000214000000206368726D0000023400000024646D6E640000025800000024646D64640000027C000000246D6C756300000000000000010000000C656E5553000000240000001C00470049004D00500020006200750069006C0074002D0069006E002000730052004700426D6C756300000000000000010000000C656E55530000001A0000001C005000750062006C0069006300200044006F006D00610069006E000058595A20000000000000F6D6000100000000D32D736633320000000000010C42000005DEFFFFF325000007930000FD90FFFFFBA1FFFFFDA2000003DC0000C06E58595A200000000000006FA0000038F50000039058595A20000000000000249F00000F840000B6C458595A2000000000000062970000B787000018D9706172610000000000030000000266660000F2A700000D59000013D000000A5B6368726D00000000000300000000A3D70000547C00004CCD0000999A0000266700000F5C6D6C756300000000000000010000000C656E5553000000080000001C00470049004D00506D6C756300000000000000010000000C656E5553000000080000001C0073005200470042FFDB0043000302020302020303030304030304050805050404050A070706080C0A0C0C0B0A0B0B0D0E12100D0E110E0B0B1016101113141515150C0F171816141812141514FFDB00430103040405040509050509140D0B0D1414141414141414141414141414141414141414141414141414141414141414141414141414141414141414141414141414FFC2001108000A000A03011100021101031101FFC4001500010100000000000000000000000000000003FFC4001501010100000000000000000000000000000007FFDA000C030100021003100000019CF65800FFC40014100100000000000000000000000000000020FFDA00080101000105021FFFC40014110100000000000000000000000000000020FFDA0008010301013F011FFFC40014110100000000000000000000000000000020FFDA0008010201013F011FFFC40014100100000000000000000000000000000020FFDA0008010100063F021FFFC40014100100000000000000000000000000000020FFDA0008010100013F211FFFDA000C030100020003000000106DBFFFC40014110100000000000000000000000000000020FFDA0008010301013F101FFFC40014110100000000000000000000000000000020FFDA0008010201013F101FFFC40014100100000000000000000000000000000020FFDA0008010100013F101FFFD9";
                    }
                  }

                  sb.Append(content);
                  break;
                case "int":
                  int intValue = dataReader.GetInt32(index);
                  sb.Append(intValue.ToString(CultureInfo.InvariantCulture));
                  break;
                case "bit":
                  bool boolValue = dataReader.GetBoolean(index);
                  sb.Append(boolValue ? '1' : '0');
                  break;
                case "datetime2":
                  var dateTimeValue = dataReader.GetDateTime(index);
                  sb.Append($"(select convert(datetime2, '{dateTimeValue:s}'))");
                  break;
                default:
                  throw new ArgumentException(column.Type, nameof(column.Type));
              }

              ++index;
            }

            sb.Append(");");

            data.Inserts.Add(
              new InsertInfo
              {
                Sql = sb.ToString(),
                PrimaryKeyVariable = primaryKeyVariableName,
                ReferencedVariables = referencedVariables,
              }
            );
          }
        }

        await dataReader.CloseAsync();
        conn.Close();
      }

      var inboundForeignKeys = foreignKeys.Where(o => o.Target.TableDescriptor.ToString() == table).ToList();

      foreach (var inboundForeignKey in inboundForeignKeys)
      {
        if (
          data.Profile.Tables.Any(o => o.Name == inboundForeignKey.Source.TableDescriptor.ToString() && !o.IsEntryPoint && o.Export)
          && data.Profile.ForeignKeys.Any(o => o.Name == inboundForeignKey.Source.ToString() && o.Follow && !o.GoToParent)
        )
        {
          string pkOtherTable = await this.GetPrimaryKey(inboundForeignKey.Source.TableDescriptor);
          await using var conn = new SqlConnection(this.ConnectionString);
          string query =
            $"select [{pkOtherTable}] from [{inboundForeignKey.Source.TableDescriptor.Schema}].[{inboundForeignKey.Source.TableDescriptor.Name}] where [{inboundForeignKey.Source.ColumnName}] = @id;";
          var cmd = new SqlCommand(query, conn);
          cmd.Parameters.Add(new SqlParameter("id", id));

          conn.Open();
          var dataReader = await cmd.ExecuteReaderAsync();

          if (dataReader.HasRows)
          {
            while (await dataReader.ReadAsync())
            {
              string otherId = dataReader.GetString(0);
              await this.CollectExportDataImplementation(inboundForeignKey.Source.TableDescriptor, otherId, dummyFiles, data);
            }
          }

          await dataReader.CloseAsync();
          conn.Close();
        }
      }
    }

    private async Task<Profile> ReadProfile(string profilePath)
    {
      return JsonSerializer.Deserialize<Profile>(await File.ReadAllTextAsync(profilePath));
    }

    private async Task CreateNewProfile(string profilePath)
    {
      var profile = new Profile
      {
        Tables = (await this.ListTables()).Select(o => new Table { Name = $"[{o.Schema}].[{o.Name}]", Export = false }).ToList(),
        ForeignKeys = (await this.ListForeignKeys())
          .Select(o => new ForeignKey
          {
            Name = $"[{o.Source.TableDescriptor.Schema}].[{o.Source.TableDescriptor.Name}].[{o.Source.ColumnName}]",
            Follow = true,
            GoToParent = false,
          })
          .ToList(),
      };

      await File.WriteAllTextAsync(profilePath, JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
    }

    public async Task Export(string exportToPath, string profile, string entryPointId, bool dummyFiles)
    {
      if (profile != null && !File.Exists(profile))
      {
        Console.WriteLine($"Creating new profile {profile}");
        await CreateNewProfile(profile);
        Console.WriteLine("Exiting: You may now update the file and the run another command");
        return;
      }

      await using var file = new StreamWriter(exportToPath);

      var data = await CollectExportData(profile, entryPointId, dummyFiles);
      await file.WriteLineAsync("BEGIN");
      foreach ((string id, string alias) in data.IdToVariable)
      {
        await file.WriteLineAsync($"declare {alias} as varchar(255) = LOWER(CONVERT(varchar(255), NEWID())); -- formerly: '{id}';");
      }

      var availableReferences = new HashSet<string>();
      bool addedSomething = true;
      while (addedSomething && availableReferences.Count < data.Inserts.Count)
      {
        addedSomething = false;

        foreach (var insert in data.Inserts)
        {
          if (availableReferences.Contains(insert.PrimaryKeyVariable))
          {
            // already visited
            continue;
          }

          bool missingReference = insert.ReferencedVariables.Any(referencedVariable => !availableReferences.Contains(referencedVariable));
          if (missingReference)
          {
            // we need to come back to this later
            continue;
          }

          await file.WriteLineAsync(insert.Sql);
          availableReferences.Add(insert.PrimaryKeyVariable);

          // others might become available now, let's loop again
          addedSomething = true;
        }
      }

      if (availableReferences.Count < data.Inserts.Count)
      {
        var allReferences = data.Inserts.Select(o => o.PrimaryKeyVariable).ToHashSet();
        foreach (var insert in data.Inserts)
        {
          if (availableReferences.Contains(insert.PrimaryKeyVariable))
          {
            // already visited
            continue;
          }

          string missingReferences = string.Join(
            ',',
            insert.ReferencedVariables.Where(referencedVariable => !allReferences.Contains(referencedVariable))
          );
          await file.WriteLineAsync($"-- Warning: Missing dependency: {missingReferences}");
          await file.WriteLineAsync(insert.Sql);
        }
      }

      await file.WriteLineAsync("END");
    }
  }
}
