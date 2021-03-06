﻿namespace eos.cli
{
    using System;
    using System.ServiceProcess;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using CommandLine;
    using core.configuration;
    using core.services;
    using eos.core.vertec;
    using options;

    class Program
    {
        static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<VertecOptions, ConfigOptions, ServicesOptions>(args)
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
            else if (o.Aggregate)
            {
                await vertec.Aggregate(o.Text, o.Regex, from, to);
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