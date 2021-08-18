namespace eos.core.vertec.cache.worklog
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Xml;
    using configuration;
    using requests;

    public class WorkLogCacheManager : VertecCacheManager<WorkLogCacheKey, List<WorkLogEntry>>
    {
        private static readonly Regex FileNameRegex = new Regex("work-logs-([0-9]{4})-([0-9]{2}).json");
        private readonly VertecRequestsManager _vertecRequestsManager;
        
        public WorkLogCacheManager(Configuration configuration, VertecRequestsManager vertecRequestsManager) : base(configuration, "work logs")
        {
            this._vertecRequestsManager = vertecRequestsManager;
        }

        public override string GetFileName(WorkLogCacheKey cacheKey)
        {
            return $"work-logs-{cacheKey.Year:D4}-{cacheKey.Month:D2}.json";
        }

        public override WorkLogCacheKey GetCacheKey(string fileName)
        {
            var match = FileNameRegex.Match(fileName);
            if (!match.Success)
            {
                return null;                
            }

            return new WorkLogCacheKey
            {
                Year = int.Parse(match.Groups[1].Value),
                Month = int.Parse(match.Groups[2].Value)
            };
        }

        public Tuple<DateTime, DateTime> GetMinMaxRange()
        {
            DateTime? min = null;
            DateTime? max = null;
            this.GetAllCacheKeys()
                .ForEach(key =>
                {
                    if (!min.HasValue || min.Value > key.RangeStart)
                    {
                        min = key.RangeStart;
                    }
                    if (!max.HasValue || max.Value < key.RangeEnd)
                    {
                        max = key.RangeEnd;
                    }
                });

            if (!max.HasValue || !min.HasValue)
            {
                throw new Exception("no min-max range found");
            }
            return new Tuple<DateTime, DateTime>(min.Value, max.Value);
        }

        public override async Task<List<WorkLogEntry>> LoadDataRemotely(WorkLogCacheKey cacheKey)
        {
            var rangeStart = cacheKey.RangeStart;
            var rangeEnd = cacheKey.RangeEnd;
            
            var entries = new List<WorkLogEntry>();
            entries.AddRange(await this.GetEntriesInternal("offeneLeistungen", rangeStart, rangeEnd));
            entries.AddRange(await this.GetEntriesInternal("verrechneteLeistungen", rangeStart, rangeEnd));
            return entries.OrderBy(o => o.Date).ThenBy(o => o.CreaDate).ToList();;
        }
        
        private async Task<List<WorkLogEntry>> GetEntriesInternal(string collectionName, DateTime rangeStart, DateTime rangeEnd)
        {
            var timeRestriction = $"(datum >= encodeDate({rangeStart.Year},{rangeStart.Month},{rangeStart.Day})) and (datum <= encodeDate({rangeEnd.Year},{rangeEnd.Month},{rangeEnd.Day}))";
            var request = new VertecRequest
            {
                Ocl = $"projektBearbeiter->select(loginName = '{this._configuration.VertecUser}').{collectionName}->select({timeRestriction})",
                Members = new[]
                {
                    "datum",
                    "text",
                    "minutenint",
                    "creationDateTime"
                },
                Expressions = new []
                {
                    new KeyValuePair<string, string>("projekt", "projekt.code"),
                    new KeyValuePair<string, string>("phase", "phase.code")
                }
            };
            var xmlDoc = await this._vertecRequestsManager.Execute(request);

            var entries = new List<WorkLogEntry>();
            foreach (XmlElement leistung in xmlDoc.DocumentElement.ChildNodes[0].ChildNodes[0].ChildNodes)
            {
                var entry = new WorkLogEntry();
                entries.Add(entry);
                foreach (XmlNode node in leistung.ChildNodes)
                {
                    var propertyTag = node as XmlElement;
                    if (propertyTag == null)
                    {
                        continue;
                    }
                    
                    switch (propertyTag.Name)
                    {
                        case "datum":
                            entry.Date = propertyTag.InnerText; // yyyy-MM-dd format
                            break;
                        case "minutenInt":
                            entry.Hours = int.Parse(propertyTag.InnerText) / 60m;
                            break;
                        case "text":
                            entry.Description = propertyTag.InnerText;
                            break;
                        case "projekt":
                            entry.Project = propertyTag.InnerText;
                            break;
                        case "phase":
                            entry.Phase = propertyTag.InnerText;
                            break;
                        case "creationDateTime":
                            entry.CreaDate= propertyTag.InnerText;
                            break;
                    }
                }
            }
            
            return entries;
        }
    }
}