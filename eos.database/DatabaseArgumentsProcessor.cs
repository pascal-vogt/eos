using eos.cli.options;
using eos.core;
using eos.core.database;

namespace eos.database;

public class DatabaseArgumentsProcessor : AbstractArgumentProcessor<DatabaseOptions>
{
  public override Type GetArgumentsType() => typeof(DatabaseOptions);

  protected override async Task ProcessArgumentsAsync(DatabaseOptions o)
  {
    if (o.ExportTo != null && o.Profile != null && o.Id != null && o.ConnectionString != null)
    {
      var databaseService = new DatabaseService { ConnectionString = o.ConnectionString };
      await databaseService.Export(o.ExportTo, o.Profile, o.Id, o.DummyFiles);
    }
    else
    {
      Console.WriteLine("The following options all need to have a value: --connection-string, --profile, --export-to, --id");
    }
  }
}
