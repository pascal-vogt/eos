namespace eos.cli.options
{
    using CommandLine;

    [Verb("vertec")]
    public class VertecOptions
    {
        [Option("init", HelpText = "Initializes the Vertec Cache")]
        public bool InitCache { get; set; }

        [Option( "update-month", HelpText = "Updates a single month of the Vertec Cache (needs a month in the yyyy-MM format or MM.yyyy format)")]
        public string CachedMonthToUpdate { get; set; }
        
        [Option( "text", HelpText = "Text filter")]
        public string Text { get; set; }
        
        [Option( "from", HelpText = "Lower bound filter for the date in the dd.MM.yyyy or yyyy-MM-dd format")]
        public string From { get; set; }
        
        [Option( "to", HelpText = "Upper bound filter for the date in the dd.MM.yyyy or yyyy-MM-dd format")]
        public string To { get; set; }
        
        [Option("today", HelpText = "Like --from, --to for today's day")]
        public bool Today { get; set; }
        
        [Option("current-month", HelpText = "Like --from, --to for the current month (partial, until today)")]
        public bool CurrentMonth { get; set; }

        [Option("list", HelpText = "List items from the vertec cache (can be filtered)")]
        public bool List { get; set; }
        
        [Option("aggregate", HelpText = "List items from the vertec cache (can be filtered)")]
        public bool Aggregate { get; set; }
        
        [Option("overtime", HelpText = "Compute current overtime")]
        public bool Overtime { get; set; }
        
        [Option("overtime-at", HelpText = "Compute overtime at a specified day")]
        public string OvertimeAt { get; set; }
        
        [Option("check-presence", HelpText = "Check if the presence matches the logged hours")]
        public bool CheckPresence { get; set; }
    }
}