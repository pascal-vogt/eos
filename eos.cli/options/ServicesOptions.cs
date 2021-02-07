namespace eos.cli.options
{
    using CommandLine;

    [Verb("services")]
    public class ServicesOptions
    {
        [Option( "text", HelpText = "Text filter")]
        public string Text { get; set; }
        
        [Option( "regex", HelpText = "Regex filter")]
        public string Regex { get; set; }

        [Option("list", HelpText = "List all services (can be filtered)")]
        public bool List { get; set; }
        
        [Option( "alias", HelpText = "Define a new alias or update an existing one (--alias vr=OVRService)")]
        public string Alias { get; set; }
        
        [Option("aliases", HelpText = "List all aliases")]
        public bool Aliases { get; set; }
        
        [Option( "unalias", HelpText = "Remove an alias (--unalias vr)")]
        public string UnAlias { get; set; }
        
        [Option( "status", HelpText = "Status of a service (takes an alias as a parameter)")]
        public string Status { get; set; }
        
        [Option( "start", HelpText = "Start a service (takes an alias as a parameter)")]
        public string Start { get; set; }
        
        [Option( "stop", HelpText = "Stop a service (takes an alias as a parameter)")]
        public string Stop { get; set; }
    }
}