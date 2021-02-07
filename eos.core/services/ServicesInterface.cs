namespace eos.core.services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.ServiceProcess;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using configuration;

    public class ServicesInterface
    {
        private readonly Configuration _configuration;

        public ServicesInterface(Configuration configuration)
        {
            this._configuration = configuration;
            if (this._configuration.ServiceAliases == null)
            {
                this._configuration.ServiceAliases = new List<ServiceAlias>();
            }
        }

        public async Task Alias(string alias, string serviceName)
        {
            var existing = this._configuration.ServiceAliases.FirstOrDefault(o => o.Alias == alias);
            if (existing != null)
            {
                existing.Name = serviceName;
            }
            else
            {
                this._configuration.ServiceAliases.Add(new ServiceAlias
                {
                    Alias = alias,
                    Name = serviceName
                });
            }

            await this._configuration.Save();
        }
        
        public async Task UnAlias(string alias)
        {
            this._configuration.ServiceAliases = this._configuration.ServiceAliases.Where(o => o.Alias != alias).ToList();

            await this._configuration.Save();
        }

        public void List(string filterText, string filterRegex)
        {
            var regexFilter = filterRegex != null ? new Regex(filterRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase) : null;
            foreach (var service in ServiceController.GetServices())
            {
                if (filterText != null)
                {
                    if (service.ServiceName.IndexOf(filterText, StringComparison.InvariantCulture) == -1 && service.DisplayName.IndexOf(filterText, StringComparison.InvariantCulture) == -1)
                    {
                        continue;
                    }
                }

                if (regexFilter != null)
                {
                    if (!regexFilter.IsMatch(service.ServiceName) && !regexFilter.IsMatch(service.DisplayName))
                    {
                        continue;
                    }
                }
                Console.WriteLine($"- \"{service.ServiceName}\" \"{service.DisplayName}\"");
            }
        }
        
        private ServiceController GetService(string alias)
        {
            var serviceName = this._configuration.ServiceAliases.FirstOrDefault(o => o.Alias == alias)?.Name;
            return ServiceController.GetServices().FirstOrDefault(o => o.ServiceName == serviceName);
        }

        public void Status(string alias)
        {
            var service = GetService(alias);
            Console.WriteLine(service.Status);
        }

        public void Start(string alias)
        {
            var service = GetService(alias);
            service.Start();
        }
        
        public void Stop(string alias)
        {
            var service = GetService(alias);
            service.Stop();
        }

        public void ListAliases()
        {
            var services = this._configuration.ServiceAliases.ToDictionary(o => o.Name, o => o.Alias);
            foreach (var service in ServiceController.GetServices().Where(o => services.ContainsKey(o.ServiceName)))
            {
                var alias = services[service.ServiceName];
                Console.WriteLine($"- {alias} {service.Status}");
            }
        }
    }
}