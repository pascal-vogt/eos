namespace eos.cli.options
{
  using CommandLine;

  [Verb("db")]
  public class DatabaseOptions
  {
    [Option('c', "connection-string", HelpText = "Sets the connection string we will be working with")]
    public string ConnectionString { get; set; }

    [Option("dummy-files", HelpText = "Replace files with dummy small equivalents")]
    public bool DummyFiles { get; set; }

    [Option("id", HelpText = "Pick an ID (only does something combined with other options)")]
    public string Id { get; set; }

    [Option("export-to", HelpText = "Exports DB contents to the given file (see also: --schema, --table, --id, --profile, --dummy-files)")]
    public string ExportTo { get; set; }

    [Option("profile", HelpText = "Specifies a file where advanced options may be specified (in which directions to explore foreign keys, etc)")]
    public string Profile { get; set; }
  }
}
