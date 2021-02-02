namespace eos.cli
{
    using System;
    using System.Threading.Tasks;
    using CommandLine;
    using core.configuration;
    using eos.core.vertec;
    using options;

    class Program
    {
        static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<VertecOptions, ConfigOptions>(args)
                .WithParsedAsync(ProcessOptions);
        }
        
        private static async Task ProcessOptions(object options)
        {
            switch (options)
            {
                case ConfigOptions configOptions:
                    await ProcessConfigOptions(configOptions);
                    break;
                case VertecOptions vertecOptions:
                    await ProcessVertecOptions(vertecOptions);
                    break;
            }
        }

        private static async Task ProcessConfigOptions(ConfigOptions o)
        {
            if (o.InitConfig)
            {
                Configuration config;
                if (Configuration.HasConfigFile())
                {
                    config = await Configuration.Load();
                }
                else
                {
                    config = new Configuration();
                }
                await config.Save();
            }
        }

        private static async Task ProcessVertecOptions(VertecOptions o)
        {
            var config = await Configuration.Load();
            var vertec = new VertecInterface(config);

            DateTime? from = null;
            if (o.From != null)
            {
                from = VertecInterface.ParseDay(o.From);
            }

            DateTime? to = null;
            if (o.To != null)
            {
                to = VertecInterface.ParseDay(o.To);
            }
            
            if (o.InitCache)
            {
                await vertec.InitCache();
            }
            else if (o.CachedMonthToUpdate != null)
            {
                VertecInterface.ParseMonth(o.CachedMonthToUpdate, out var year, out var month);

                await vertec.UpdateWorkLogCache(year, month);
            }
            else if (o.List)
            {
                await vertec.ListEntries(o.Text, from, to);
            }
            else if (o.Aggregate)
            {
                await vertec.Aggregate(o.Text, from, to);
            }
            else if (o.Overtime)
            {
                await vertec.Overtime(null);
            }
            else if (o.OvertimeAt != null)
            {
                DateTime? at = VertecInterface.ParseDay(o.OvertimeAt);
                await vertec.Overtime(at);
            }
        }
    }
}