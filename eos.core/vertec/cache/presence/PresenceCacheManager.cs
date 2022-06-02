namespace eos.core.vertec.cache.presence
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Xml;
    using configuration;
    using requests;

    public class PresenceCacheManager: VertecCacheManager<PresenceCacheKey, List<Presence>>
    {
        private static readonly Regex FileNameRegex = new("presence-([0-9]{4})-([0-9]{2}).json");
        private readonly VertecRequestsManager _vertecRequestsManager;
        
        public PresenceCacheManager(Configuration configuration, VertecRequestsManager vertecRequestsManager) : base(configuration, "presence")
        {
            this._vertecRequestsManager = vertecRequestsManager;
        }

        public override string GetFileName(PresenceCacheKey cacheKey)
        {
            return $"presence-{cacheKey.Year:D4}-{cacheKey.Month:D2}.json";
        }

        public override PresenceCacheKey GetCacheKey(string fileName)
        {
            var match = FileNameRegex.Match(fileName);
            if (!match.Success)
            {
                return null;                
            }

            return new PresenceCacheKey
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

        public override async Task<List<Presence>> LoadDataRemotely(PresenceCacheKey cacheKey)
        {
            var nextMonth = new DateTime(cacheKey.Year, cacheKey.Month, 1).AddMonths(1);
            var timeRestriction = $"(von >= encodeDate({cacheKey.Year},{cacheKey.Month},1)) and (von < encodeDate({nextMonth.Year},{nextMonth.Month},1))";
            var request = new VertecRequest
            {
                Ocl = $"projektBearbeiter->select(loginName = '{this._configuration.VertecUser}').praesenzzeiten->select({timeRestriction})",
                Members = new[]
                {
                    "von",
                    "bis",
                    "text"
                },
                Expressions = Array.Empty<KeyValuePair<string, string>>()
            };
            var xmlDoc = await this._vertecRequestsManager.Execute(request);

            var entries = new List<Presence>();
            foreach (XmlElement presenceElement in xmlDoc.DocumentElement.ChildNodes[0].ChildNodes[0].ChildNodes)
            {
                var entry = new Presence();
                entries.Add(entry);
                foreach (XmlNode node in presenceElement.ChildNodes)
                {
                    if (node is not XmlElement propertyTag)
                    {
                        continue;
                    }
                    
                    switch (propertyTag.Name)
                    {
                        case "von":
                            entry.From = propertyTag.InnerText; // yyyy-MM-dd format
                            break;
                        case "bis":
                            entry.To = propertyTag.InnerText; // yyyy-MM-dd format
                            break;
                        case "text":
                            entry.Text = propertyTag.InnerText;
                            break;
                    }
                }
            }
            
            return entries.OrderBy(o => o.From).ToList();
        }
    }
}