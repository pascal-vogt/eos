namespace eos.cli.options
{
    using CommandLine;

    [Verb("config")]
    public class ConfigOptions
    {
        [Option("init", HelpText = "Initializes the config file")]
        public bool InitConfig { get; set; }
    }
}