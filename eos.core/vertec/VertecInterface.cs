namespace eos.core.vertec
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Web;
    using System.Xml;
    using configuration;

    public class VertecInterface
    {
        private readonly Configuration _configuration;
        private string _jsonWebToken;
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, Regex> _regexCache;
        
        public VertecInterface(Configuration configuration)
        {
            this._configuration = configuration;
            this._httpClient = new HttpClient();
            this._regexCache = this.BuildRegexCache(configuration);
        }

        private Dictionary<string, Regex> BuildRegexCache(Configuration configuration)
        {
            var cache = new Dictionary<string, Regex>();
            if (configuration.VertecAggregationConfig != null)
            {
                foreach (var configItem in configuration.VertecAggregationConfig)
                {
                    if (!cache.ContainsKey(configItem.Match))
                    {
                        cache.Add(configItem.Match, new Regex(configItem.Match, RegexOptions.Compiled | RegexOptions.IgnoreCase));
                    }
                }
            }
            return cache;
        }

        private async Task Login()
        {
            var pass = new StringBuilder();
            Console.Write($"Vertec password for {this._configuration.VertecUser}:");
            ConsoleKeyInfo key;

            do 
            {
                key = Console.ReadKey(true);
                
                if (!char.IsControl(key.KeyChar)) 
                {
                    pass.Append(key.KeyChar);
                } 
                else 
                {
                    if (key.Key == ConsoleKey.Backspace && pass.Length > 0) 
                    {
                        pass.Remove(pass.Length - 1, 1);
                    }
                }
            }
            while (key.Key != ConsoleKey.Enter);
            Console.WriteLine();

            var userNameUrlEncoded = HttpUtility.UrlEncode(this._configuration.VertecUser);
            var passwordUrlEncoded = HttpUtility.UrlEncode(pass.ToString());
            var result = await this._httpClient.PostAsync($"{this._configuration.VertecURL}/auth/xml",
                new StringContent($"vertec_username={userNameUrlEncoded}&password={passwordUrlEncoded}",
                    Encoding.UTF8));
            this._jsonWebToken = await result.Content.ReadAsStringAsync();
        }

        private string GetWorkLogCacheFileLocation(int year, int month)
        {
            return Path.Join(this._configuration.VertecCacheLocation, $"work-logs-{year.ToString("D4")}-{month.ToString("D2")}.json");
        }
        
        private string GetHolidayCacheFileLocation()
        {
            return Path.Join(this._configuration.VertecCacheLocation, $"holidays.json");
        }
        
        private string GetFirstWorkDayFileLocation()
        {
            return Path.Join(this._configuration.VertecCacheLocation, $"first-work-day.json");
        }
        
        private Tuple<DateTime, DateTime> GetMinMaxRange()
        {
            DateTime? min = null;
            DateTime? max = null;
            Directory.GetFiles(this._configuration.VertecCacheLocation)
                .Select(f => Path.GetFileName(f))
                .Where(f => f.StartsWith("work-logs-"))
                .Select(f => Regex.Split(f, @"[-\.]"))
                .Select(p => new DateTime(int.Parse(p[2]), int.Parse(p[3]), 1))
                .ToList()
                .ForEach(date =>
                {
                    if (!min.HasValue || min.Value > date)
                    {
                        min = date;
                    }
                    if (!max.HasValue || max.Value < date)
                    {
                        max = new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
                    }
                });

            if (!max.HasValue || !min.HasValue)
            {
                throw new Exception("no min-max range found");
            }
            return new Tuple<DateTime, DateTime>(min.Value, max.Value);
        }

        private bool HasWorkLogCache(int year, int month)
        {
            var location = GetWorkLogCacheFileLocation(year, month);
            return File.Exists(location);
        }
        
        private bool HasHolidayCache()
        {
            var location = GetHolidayCacheFileLocation();
            return File.Exists(location);
        }

        public async Task UpdateWorkLogCache(int year, int month)
        {
            var rangeStart = new DateTime(year, month, 1);
            var rangeEnd = new DateTime(rangeStart.Year, rangeStart.Month, DateTime.DaysInMonth(rangeStart.Year, rangeStart.Month));
            var entries = await GetEntries(rangeStart, rangeEnd);
            entries = entries.OrderBy(o => o.Date).ThenBy(o => o.CreaDate).ToList();
            var jsonString = JsonSerializer.Serialize(entries, new JsonSerializerOptions
            {  
                WriteIndented = true
            });
            await File.WriteAllTextAsync(GetWorkLogCacheFileLocation(year, month), jsonString);
        }

        private async Task UpdateHolidayCache()
        {
            var entries = await GetHolidays();
            entries = entries.OrderBy(o => o.FromDate).ThenBy(o => o.ToDate).ToList();
            var jsonString = JsonSerializer.Serialize(entries, new JsonSerializerOptions
            {  
                WriteIndented = true
            });
            await File.WriteAllTextAsync(GetHolidayCacheFileLocation(), jsonString);
        }

        private async Task<List<VertecEntry>> LoadWorkLogCache(int year, int month)
        {
            if (!HasWorkLogCache(year, month))
            {
                return new List<VertecEntry>();
            }
            
            var text = await File.ReadAllTextAsync(GetWorkLogCacheFileLocation(year, month));
            return JsonSerializer.Deserialize<List<VertecEntry>>(text);
        }

        private async Task<List<Holiday>> LoadHolidayCache()
        {
            if (!HasHolidayCache())
            {
                return new List<Holiday>();
            }
            
            var text = await File.ReadAllTextAsync(GetHolidayCacheFileLocation());
            return JsonSerializer.Deserialize<List<Holiday>>(text);
        }

        private async Task<List<VertecEntry>> GetEntries(DateTime rangeStart, DateTime rangeEnd)
        {
            var entries = new List<VertecEntry>();
            entries.AddRange(await this.GetEntriesInternal("offeneLeistungen", rangeStart, rangeEnd));
            entries.AddRange(await this.GetEntriesInternal("verrechneteLeistungen", rangeStart, rangeEnd));
            return entries;
        }

        private static string RemoveIllegalXmlCharacters(string xml)
        {
            return Regex.Replace(xml, "[\x00-\x08\x0B\x0C\x0E-\x1F]", string.Empty, RegexOptions.Compiled);
        }

        private async Task<List<Holiday>> GetHolidays()
        {
            if (this._jsonWebToken == null)
            {
                await this.Login();
            }
            
            var request = new VertecRequest
            {
                Token = this._jsonWebToken,
                Ocl = $"abwesenheit",
                Members = new[]
                {
                    "datum",
                    "bisDatum",
                    "beschreibung"
                },
                Expressions = new KeyValuePair<string, string>[]
                {
                }
            };
            
            var result = await this._httpClient.PostAsync($"{this._configuration.VertecURL}/xml",
                new StringContent(request.ToString(), Encoding.UTF8));

            var xmlOut = await result.Content.ReadAsStringAsync();
            
            // this shouldn't be necessary but i've seen Vertec produce illegal XML characters and filtering them out
            // seems to be the easiest way to deal with this problem considering we cannot change their source code
            xmlOut = RemoveIllegalXmlCharacters(xmlOut);

            var xmlDoc= new XmlDocument();
            try
            {
                xmlDoc.Load(new MemoryStream(Encoding.UTF8.GetBytes(xmlOut)));
            }
            catch (Exception e)
            {
                Console.WriteLine(xmlOut);
                throw;
            }
            
            var entries = new List<Holiday>();
            foreach (XmlElement leistung in xmlDoc.DocumentElement.ChildNodes[0].ChildNodes[0].ChildNodes)
            {
                var entry = new Holiday();
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
                            entry.FromDate = propertyTag.InnerText; // yyyy-MM-dd format
                            break;
                        case "bisDatum":
                            entry.ToDate = propertyTag.InnerText; // yyyy-MM-dd format
                            break;
                        case "beschreibung":
                            entry.Name = propertyTag.InnerText;
                            break;
                    }
                }
            }
            
            return entries;
        }

        private async Task<List<VertecEntry>> GetEntriesInternal(string collectionName, DateTime rangeStart, DateTime rangeEnd)
        {
            if (this._jsonWebToken == null)
            {
                await this.Login();
            }
            var timeRestriction = $"(datum >= encodeDate({rangeStart.Year},{rangeStart.Month},{rangeStart.Day})) and (datum <= encodeDate({rangeEnd.Year},{rangeEnd.Month},{rangeEnd.Day}))";
            var request = new VertecRequest
            {
                Token = this._jsonWebToken,
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

            var result = await this._httpClient.PostAsync($"{this._configuration.VertecURL}/xml",
                new StringContent(request.ToString(), Encoding.UTF8));
            
            var xmlOut = await result.Content.ReadAsStringAsync();

            // this shouldn't be necessary but i've seen Vertec produce illegal XML characters and filtering them out
            // seems to be the easiest way to deal with this problem considering we cannot change their source code
            xmlOut = RemoveIllegalXmlCharacters(xmlOut);

            var xmlDoc= new XmlDocument();
            try
            {
                xmlDoc.Load(new MemoryStream(Encoding.UTF8.GetBytes(xmlOut)));
            }
            catch (Exception e)
            {
                Console.WriteLine(xmlOut);
                throw;
            }

            var entries = new List<VertecEntry>();
            foreach (XmlElement leistung in xmlDoc.DocumentElement.ChildNodes[0].ChildNodes[0].ChildNodes)
            {
                var entry = new VertecEntry();
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

        private async Task<string> GetUserFirstWorkDay()
        {
            var text = await File.ReadAllTextAsync(GetFirstWorkDayFileLocation());
            return JsonSerializer.Deserialize<string>(text);
        }
        
        public async Task CacheUserFirstWorkDay()
        {
            if (this._jsonWebToken == null)
            {
                await this.Login();
            }

            var request = new VertecRequest
            {
                Token = this._jsonWebToken,
                Ocl = $"projektBearbeiter->select(loginName = '{this._configuration.VertecUser}')",
                Members = new[]
                {
                    "eintrittPer"
                },
                Expressions = new KeyValuePair<string, string>[]
                {
                }
            };

            var result = await this._httpClient.PostAsync($"{this._configuration.VertecURL}/xml",
                new StringContent(request.ToString(), Encoding.UTF8));
            
            var xmlOut = await result.Content.ReadAsStringAsync();

            var xmlDoc= new XmlDocument();
            xmlDoc.Load(new MemoryStream(Encoding.UTF8.GetBytes(xmlOut)));
            
            foreach (XmlElement projektBearbeiter in xmlDoc.DocumentElement.ChildNodes[0].ChildNodes[0].ChildNodes)
            {
                foreach (XmlNode node in projektBearbeiter.ChildNodes)
                {
                    var propertyTag = node as XmlElement;
                    if (propertyTag == null)
                    {
                        continue;
                    }
                    
                    switch (propertyTag.Name)
                    {
                        case "eintrittPer":
                            var dateComponents = propertyTag.InnerText;
                            var jsonString = JsonSerializer.Serialize(dateComponents, new JsonSerializerOptions
                            {  
                                WriteIndented = true
                            });
                            await File.WriteAllTextAsync(GetFirstWorkDayFileLocation(), jsonString);
                            break;
                    }
                }
            }
        }

        public async Task InitCache()
        {
            await UpdateHolidayCache();
            await CacheUserFirstWorkDay();
            var firstWorkDay = ParseDay(await GetUserFirstWorkDay());
            var now = DateTime.Today;
            var rangeStart = new DateTime(firstWorkDay.Year, firstWorkDay.Month, 1);
            while (true)
            {
                if (rangeStart.Year > now.Year || (rangeStart.Year == now.Year && rangeStart.Month > now.Month))
                {
                    Console.WriteLine("Done initializing the cache");
                    break;
                }

                if (!this.HasWorkLogCache(rangeStart.Year, rangeStart.Month))
                {
                    Console.WriteLine($"Caching {rangeStart.Month.ToString("D2")}.{rangeStart.Year}");
                    await this.UpdateWorkLogCache(rangeStart.Year, rangeStart.Month);
                }

                rangeStart = rangeStart.AddMonths(1);
            }
        }

        private async Task<List<VertecEntry>> LoadCacheInRange(DateTime? from, DateTime? to)
        {
            if (!from.HasValue || !to.HasValue)
            {
                var (min, max) = GetMinMaxRange();
                from ??= min;
                to ??= max;
            }
            
            var results = new List<VertecEntry>();
            var year = from.Value.Year;
            var month = from.Value.Month;
            while (true)
            {
                if (year > to.Value.Year || (year == to.Value.Year && month > to.Value.Month))
                {
                    break;
                }

                results.AddRange(await LoadWorkLogCache(year, month));

                ++month;
                if (month > 12)
                {
                    ++year;
                    month = 0;
                }
            }

            return results;
        }

        private string FormatDay(DateTime? dateTime)
        {
            return dateTime.HasValue ? $"{dateTime.Value.Year:D4}-{dateTime.Value.Month:D2}-{dateTime.Value.Day:D2}" : null;
        }

        private async Task<List<VertecEntry>> FilterEntries(string textFilter, DateTime? from, DateTime? to)
        {
            var unfiltered = await LoadCacheInRange(from, to);
            var fromFormatted = FormatDay(from);
            var toFormatted = FormatDay(to);
            
            return unfiltered.Where(entry =>
            {
                if (fromFormatted != null && string.Compare(fromFormatted, entry.Date, StringComparison.InvariantCulture) > 0)
                {
                    return false;
                }
                
                if (toFormatted != null && string.Compare(toFormatted, entry.Date, StringComparison.InvariantCulture) < 0)
                {
                    return false;
                }
                
                if (textFilter != null)
                {
                    if (entry.Description.IndexOf(textFilter, StringComparison.InvariantCulture) == -1 && entry.Project.IndexOf(textFilter, StringComparison.InvariantCulture) == -1 &&
                        entry.Phase.IndexOf(textFilter, StringComparison.InvariantCulture) == -1)
                    {
                        return false;
                    }
                }

                return true;
            }).ToList();
        }

        public async Task ListEntries(string textFilter, DateTime? from, DateTime? to)
        {
            var filteredEntries = await FilterEntries(textFilter, from, to);
            foreach (var entry in filteredEntries)
            {
                Console.WriteLine($"- {entry.Date} {(entry.Hours).ToString("N2", CultureInfo.InvariantCulture)}h {entry.Description}");
            }
        }

        private string GetAggregationKey(VertecEntry entry)
        {
            // default to project name
            if (this._configuration.VertecAggregationConfig == null)
            {
                return entry.Project;
            }

            // config is available
            foreach (var item in this._configuration.VertecAggregationConfig)
            {
                string value = null;
                foreach (var key in item.Key.Split(","))
                {
                    switch (key)
                    {
                        case "Project":
                            value = entry.Project;
                            break;
                        case "Phase":
                            value = entry.Phase;
                            break;
                        case "Description":
                            value = entry.Description;
                            break;
                        default:
                            throw new ArgumentException(item.Key);
                    }

                    var regex = this._regexCache[item.Match];
                    if (regex.IsMatch(value))
                    {
                        return regex.Replace(value, item.Replacement);
                    }
                }
            }

            return "?";
        }

        public async Task Aggregate(string textFilter, DateTime? from, DateTime? to)
        {
            var filteredEntries = await FilterEntries(textFilter, from, to);
            var totalAmount = 0m;
            var amountPerProject = new Dictionary<string, decimal>();
            filteredEntries.ForEach(entry =>
            {
                var aggregationKey = this.GetAggregationKey(entry);
                totalAmount += entry.Hours;
                var sumSoFar = 0m;
                amountPerProject.TryGetValue(aggregationKey, out sumSoFar);
                sumSoFar += entry.Hours;
                amountPerProject[aggregationKey] = sumSoFar;
            });
            Console.WriteLine("Per project:");
            foreach (var project in amountPerProject.Keys.OrderBy(s => s))
            {
                var hous = amountPerProject[project];
                Console.WriteLine($"- {hous.ToString("N2", CultureInfo.InvariantCulture).PadLeft(7)}h {project}");                
            }
            Console.WriteLine($"\nTotal: {totalAmount.ToString("N2", CultureInfo.InvariantCulture)}h");
        }
        
        public static void ParseMonth(string s, out int year, out int month)
        {
            if (Regex.Match(s, @"\d{4}-\d{2}").Success)
            {
                var date = s.Split("-").Select(v => int.Parse(v)).ToArray();
                year = date[0];
                month = date[1];
            } 
            else if (Regex.Match(s, @"\d{2}.\d{4}").Success)
            {
                var date = s.Split(".").Select(v => int.Parse(v)).ToArray();
                year = date[1];
                month = date[0];
            }
            else
            {
                throw new Exception($"{s} is not in the yyyy-MM or MM.yyyy format");
            }
        }
        
        public static DateTime ParseDay(string s)
        {
            if (Regex.Match(s, @"\d{4}-\d{2}-\d{2}").Success)
            {
                var date = s.Split("-").Select(int.Parse).ToArray();
                return new DateTime(date[0], date[1], date[2]);
            } 
            else if (Regex.Match(s, @"\d{2}.\d{2}.\d{4}").Success)
            {
                var date = s.Split(".").Select(int.Parse).ToArray();
                return new DateTime(date[2], date[1], date[0]);
            }
            else
            {
                throw new Exception($"{s} is not in the yyyy-MM-dd or dd.MM.yyyy format");
            }
        }

        private async Task<HashSet<string>> GetHolidayTestHashSet()
        {
            var result = new HashSet<string>();
            foreach (var holiday in await LoadHolidayCache())
            {
                if (holiday.FromDate == holiday.ToDate)
                {
                    result.Add(holiday.FromDate);
                }
                else
                {
                    var iter = ParseDay(holiday.FromDate);
                    var formatted = holiday.FromDate;
                    while (string.Compare(formatted, holiday.ToDate, StringComparison.InvariantCulture) <= 0)
                    {
                        result.Add(formatted);
                        
                        iter = iter.AddDays(1);
                        formatted = FormatDay(iter);
                    }
                }
            }

            return result;
        }

        public async Task Overtime(DateTime? atDateTime)
        {
            var filteredEntries = await FilterEntries(null, null, null);
            var holidayTestHashSet = await GetHolidayTestHashSet();
            var firstWorkDay = ParseDay(await GetUserFirstWorkDay());
            var end = atDateTime ?? DateTime.Today;
            var totalAmount = 0m;
            var expectedAmount = 0m;
            var iter = firstWorkDay;
            var i = 0;
            while (iter < end)
            {
                var formatted = FormatDay(iter);
                if (i < filteredEntries.Count && string.Compare(filteredEntries[i].Date, formatted, StringComparison.InvariantCulture) < 0)
                {
                    ++i;
                }

                var isHoliday = holidayTestHashSet.Contains(formatted);
                var isWeekend = iter.DayOfWeek == DayOfWeek.Saturday || iter.DayOfWeek == DayOfWeek.Sunday;
                if (!isHoliday && !isWeekend)
                {
                    // TODO: account for lower work percentages (stored on user object)
                    expectedAmount += 8.3m;
                }

                while (i < filteredEntries.Count && filteredEntries[i].Date == formatted)
                {
                    totalAmount += filteredEntries[i].Hours;
                    ++i;
                }
                
                iter = iter.AddDays(1);
            }
            
            Console.WriteLine($"Overtime: {(totalAmount - expectedAmount).ToString("N2", CultureInfo.InvariantCulture)}");
        }
    }
}