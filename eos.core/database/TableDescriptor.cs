namespace eos.core.database
{
    using System.Linq;

    public class TableDescriptor
    {
        public string Name { get; set; }
        public string Schema { get; set; }

        public override string ToString()
        {
            return $"[{Schema}].[{Name}]";
        }

        public static TableDescriptor FromString(string fullyQualifiedTableName)
        {
            var tableNameParts = fullyQualifiedTableName
                .Split('.')
                .Select(s => s.Substring(1, s.Length - 2))
                .ToArray();

            return new TableDescriptor
            {
                Schema = tableNameParts[0],
                Name = tableNameParts[1]
            };
        }
    }
}