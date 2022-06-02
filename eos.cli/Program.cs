namespace eos.cli
{
    using System;
    using System.Threading.Tasks;
    using CommandLine;
    using core.configuration;
    using core.database;
    using core.services;
    using core.vertec;
    using options;

    class Program
    {
        static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<VertecOptions, ConfigOptions, ServicesOptions, DatabaseOptions>(args)
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
                case ServicesOptions servicesOptions:
                    await ProcessServiceOptions(servicesOptions);
                    break;
                case DatabaseOptions databaseOptions:
                    await ProcessDatabaseOptions(databaseOptions);
                    break;
            }
        }

        private static async Task ProcessDatabaseOptions(DatabaseOptions o)
        {
            if (o.ExportTo != null && o.Profile != null && o.Id != null && o.ConnectionString != null)
            {
                var databaseService = new DatabaseService
                {
                    ConnectionString = o.ConnectionString
                };
                await databaseService.Export(o.ExportTo, o.Profile, o.Id, o.DummyFiles);
            }
            else
            {
                Console.WriteLine("The following options all need to have a value: --connection-string, --profile, --export-to, --id");
            }
        }

        private static async Task ProcessServiceOptions(ServicesOptions o)
        {
            var config = await Configuration.Load();
            var services = new ServicesInterface(config);
            
            if (o.List)
            {
                services.List(o.Text, o.Regex);
            }

            if (o.Aliases)
            {
                services.ListAliases();
            }

            if (o.Alias != null)
            {
                var a = o.Alias.Split("=");
                await services.Alias(a[0], a[1]);
            }

            if (o.UnAlias != null)
            {
                await services.UnAlias(o.UnAlias);
            }

            if (o.Status != null)
            {
                services.Status(o.Status);
            }
            
            if (o.Start != null)
            {
                services.Start(o.Start);
            }
            
            if (o.Stop != null)
            {
                services.Stop(o.Stop);
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

            if (o.Today)
            {
                from = DateTime.Today;
                to = DateTime.Today;
            }
            
            if (o.CurrentMonth)
            {
                var today = DateTime.Today;
                from = new DateTime(today.Year, today.Month, 1);
                to = today;
            }
            
            if (o.InitCache)
            {
                await vertec.InitCache();
            }
            else if (o.CachedMonthToUpdate != null)
            {
                VertecInterface.ParseMonth(o.CachedMonthToUpdate, out var year, out var month);

                await vertec.UpdateMonth(year, month);
            }
            else if (o.List)
            {
                await vertec.ListWorkLogEntries(o.Text, o.Regex, from, to);
            }
            else if (o.ListPresence)
            {
                await vertec.ListPresenceEntries(o.Text, o.Regex, from, to);
            }
            else if (o.Aggregate)
            {
                await vertec.Aggregate(o.Text, o.Regex, from, to);
            }
            else if (o.AggregatePresence)
            {
                await vertec.AggregatePresence(o.Text, o.Regex, from, to);
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
            else if (o.CheckPresence)
            {
                await vertec.CheckPresence(from, to);
            }
        }
    }
}