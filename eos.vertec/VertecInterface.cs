﻿using System.Globalization;
using System.Text.RegularExpressions;
using eos.core.configuration;
using eos.core.vertec.cache.holidays;
using eos.core.vertec.cache.presence;
using eos.core.vertec.cache.userinfo;
using eos.core.vertec.cache.worklog;
using eos.core.vertec.requests;
using eos.vertec;

namespace eos.core.vertec
{
  public class VertecInterface
  {
    private readonly VertecConfig _vertecConfig;
    private readonly Dictionary<string, Regex> _regexCache;
    private readonly WorkLogCacheManager _workLogCacheManager;
    private readonly HolidayEntryCacheManager _holidayEntryCacheManager;
    private readonly UserInfoCacheManager _userInfoCacheManager;
    private readonly PresenceCacheManager _presenceCacheManager;

    public VertecInterface(Configuration configuration)
    {
      this._vertecConfig = configuration.Get<VertecConfig>();
      var vertecRequestManager = new VertecRequestsManager(configuration);
      this._workLogCacheManager = new WorkLogCacheManager(configuration, vertecRequestManager);
      this._holidayEntryCacheManager = new HolidayEntryCacheManager(configuration, vertecRequestManager);
      this._userInfoCacheManager = new UserInfoCacheManager(configuration, vertecRequestManager);
      this._presenceCacheManager = new PresenceCacheManager(configuration, vertecRequestManager);
      this._regexCache = this.BuildRegexCache(configuration);
    }

    private Dictionary<string, Regex> BuildRegexCache(Configuration configuration)
    {
      var cache = new Dictionary<string, Regex>();
      if (_vertecConfig.AggregationConfig != null)
      {
        foreach (var configItem in _vertecConfig.AggregationConfig)
        {
          if (!cache.ContainsKey(configItem.Match))
          {
            cache.Add(configItem.Match, new Regex(configItem.Match, RegexOptions.Compiled | RegexOptions.IgnoreCase));
          }
        }
      }
      return cache;
    }

    public async Task UpdateMonth(int year, int month)
    {
      await this._workLogCacheManager.ForceReCache(new WorkLogCacheKey { Year = year, Month = month });
      await this._presenceCacheManager.ForceReCache(new PresenceCacheKey { Year = year, Month = month });
    }

    public async Task InitCache()
    {
      await _holidayEntryCacheManager.GetData();
      var userInfo = await _userInfoCacheManager.GetData();
      var firstWorkDay = ParseDay(userInfo.FirstWorkDay);
      var now = DateTime.Today;
      var rangeStart = new DateTime(firstWorkDay.Year, firstWorkDay.Month, 1);
      while (true)
      {
        if (rangeStart.Year > now.Year || (rangeStart.Year == now.Year && rangeStart.Month > now.Month))
        {
          Console.WriteLine("Done initializing the cache");
          break;
        }

        await this._workLogCacheManager.GetData(new WorkLogCacheKey(rangeStart));
        await this._presenceCacheManager.GetData(new PresenceCacheKey(rangeStart));

        rangeStart = rangeStart.AddMonths(1);
      }
    }

    private async Task<List<Presence>> LoadPresenceCacheInRange(DateTime? from, DateTime? to)
    {
      if (!from.HasValue || !to.HasValue)
      {
        (DateTime min, DateTime max) = _presenceCacheManager.GetMinMaxRange();
        from ??= min;
        to ??= max;
      }

      var results = new List<Presence>();
      int year = from.Value.Year;
      int month = from.Value.Month;
      while (true)
      {
        if (year > to.Value.Year || (year == to.Value.Year && month > to.Value.Month))
        {
          break;
        }

        results.AddRange(await _presenceCacheManager.GetData(new PresenceCacheKey { Year = year, Month = month }));

        ++month;
        if (month > 12)
        {
          ++year;
          month = 1;
        }
      }

      return results;
    }

    private async Task<List<WorkLogEntry>> LoadWorkLogCacheInRange(DateTime? from, DateTime? to)
    {
      if (!from.HasValue || !to.HasValue)
      {
        (DateTime min, DateTime max) = _workLogCacheManager.GetMinMaxRange();
        from ??= min;
        to ??= max;
      }

      var results = new List<WorkLogEntry>();
      int year = from.Value.Year;
      int month = from.Value.Month;
      while (true)
      {
        if (year > to.Value.Year || (year == to.Value.Year && month > to.Value.Month))
        {
          break;
        }

        results.AddRange(await _workLogCacheManager.GetData(new WorkLogCacheKey { Year = year, Month = month }));

        ++month;
        if (month > 12)
        {
          ++year;
          month = 1;
        }
      }

      return results;
    }

    private string FormatDay(DateTime? dateTime)
    {
      return dateTime.HasValue ? $"{dateTime.Value.Year:D4}-{dateTime.Value.Month:D2}-{dateTime.Value.Day:D2}" : null;
    }

    private async Task<List<Presence>> FilterPresenceEntries(string textFilter, string regexFilter, DateTime? from, DateTime? to)
    {
      var unfiltered = await LoadPresenceCacheInRange(from, to);
      string fromFormatted = FormatDay(from);
      string toFormatted = FormatDay(to);
      var compiledRegexFilter = regexFilter != null ? new Regex(regexFilter, RegexOptions.Compiled) : null;

      return unfiltered
        .Where(entry =>
        {
          if (fromFormatted != null && string.Compare(fromFormatted, entry.From, StringComparison.InvariantCulture) > 0)
          {
            return false;
          }

          if (toFormatted != null && string.Compare(toFormatted, entry.From, StringComparison.InvariantCulture) < 0)
          {
            return false;
          }

          if (textFilter != null)
          {
            if (entry.Text.IndexOf(textFilter, StringComparison.InvariantCulture) == -1)
            {
              return false;
            }
          }

          if (compiledRegexFilter != null)
          {
            if (!compiledRegexFilter.IsMatch(entry.Text))
            {
              return false;
            }
          }

          return true;
        })
        .ToList();
    }

    private async Task<List<WorkLogEntry>> FilterWorkLogEntries(string textFilter, string regexFilter, DateTime? from, DateTime? to)
    {
      var unfiltered = await LoadWorkLogCacheInRange(from, to);
      string fromFormatted = FormatDay(from);
      string toFormatted = FormatDay(to);
      var compiledRegexFilter = regexFilter != null ? new Regex(regexFilter, RegexOptions.Compiled) : null;

      return unfiltered
        .Where(entry =>
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
            if (
              entry.Description.IndexOf(textFilter, StringComparison.InvariantCulture) == -1
              && entry.Project.IndexOf(textFilter, StringComparison.InvariantCulture) == -1
              && entry.Phase.IndexOf(textFilter, StringComparison.InvariantCulture) == -1
            )
            {
              return false;
            }
          }

          if (compiledRegexFilter != null)
          {
            if (
              !compiledRegexFilter.IsMatch(entry.Description)
              && !compiledRegexFilter.IsMatch(entry.Project)
              && !compiledRegexFilter.IsMatch(entry.Phase)
            )
            {
              return false;
            }
          }

          return true;
        })
        .ToList();
    }

    public async Task CheckPresence(DateTime? from, DateTime? to)
    {
      Console.WriteLine("Checking if work logs match presences");
      var filteredWorkLogEntries = (await FilterWorkLogEntries(null, null, from, to))
        .GroupBy(o => ParseDay(o.Date))
        .ToDictionary(g => g.Key, g => g.ToList());
      var filteredPresenceEntries = (await FilterPresenceEntries(null, null, from, to))
        .GroupBy(o =>
        {
          var time = ParseTime(o.From);
          var day = new DateTime(time.Year, time.Month, time.Day);
          return day;
        })
        .ToDictionary(g => g.Key, g => g.ToList());
      var dates = filteredPresenceEntries.Keys.Union(filteredWorkLogEntries.Keys).OrderBy(o => o).ToList();
      foreach (var date in dates)
      {
        decimal totalPresence = 0m;
        decimal totalLogged = 0m;
        if (filteredWorkLogEntries.TryGetValue(date, out var workLogs))
        {
          totalLogged += workLogs.Sum(workLog => workLog.Hours);
        }

        if (filteredPresenceEntries.TryGetValue(date, out var presences))
        {
          foreach (var presence in presences)
          {
            var presenceFrom = ParseTime(presence.From);
            var presenceTo = ParseTime(presence.To);
            totalPresence += (decimal)(presenceTo - presenceFrom).TotalHours;
          }
        }
        // some minutes count result in rounding errors, let's try to fix this
        totalPresence = Math.Round(totalPresence, 5);

        if (totalPresence != totalLogged)
        {
          Console.WriteLine(
            $"- {date.Day:D2}.{date.Month:D2}.{date.Year:D4} : logged {totalLogged.ToString(CultureInfo.InvariantCulture)}h, presence {totalPresence.ToString(CultureInfo.InvariantCulture)}h"
          );
        }
      }
    }

    public async Task ListWorkLogEntries(string textFilter, string regexFilter, DateTime? from, DateTime? to)
    {
      var filteredEntries = await FilterWorkLogEntries(textFilter, regexFilter, from, to);
      foreach (var entry in filteredEntries)
      {
        Console.WriteLine($"- {entry.Date} {(entry.Hours).ToString("N2", CultureInfo.InvariantCulture)}h {entry.Description}");
      }
    }

    public async Task ListPresenceEntries(string textFilter, string regexFilter, DateTime? from, DateTime? to)
    {
      var filteredEntries = await FilterPresenceEntries(textFilter, regexFilter, from, to);
      foreach (var entry in filteredEntries)
      {
        Console.WriteLine($"- {entry.From} - {entry.To}: {entry.Text}");
      }
    }

    private string GetAggregationKey(WorkLogEntry entry)
    {
      // default to project name
      if (this._vertecConfig.AggregationConfig == null)
      {
        return entry.Project;
      }

      // config is available
      foreach (var item in this._vertecConfig.AggregationConfig)
      {
        string value = null;
        foreach (string key in item.Key.Split(","))
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

    public async Task Aggregate(string textFilter, string regexFilter, DateTime? from, DateTime? to)
    {
      var filteredEntries = await FilterWorkLogEntries(textFilter, regexFilter, from, to);
      decimal totalAmount = 0m;
      var amountPerProject = new Dictionary<string, decimal>();
      filteredEntries.ForEach(entry =>
      {
        string aggregationKey = this.GetAggregationKey(entry);
        totalAmount += entry.Hours;
        decimal sumSoFar = 0m;
        amountPerProject.TryGetValue(aggregationKey, out sumSoFar);
        sumSoFar += entry.Hours;
        amountPerProject[aggregationKey] = sumSoFar;
      });
      Console.WriteLine("Per project:");
      foreach (string project in amountPerProject.Keys.OrderBy(s => s))
      {
        decimal hous = amountPerProject[project];
        Console.WriteLine($"- {hous.ToString("N2", CultureInfo.InvariantCulture).PadLeft(7)}h {project}");
      }
      Console.WriteLine($"\nTotal: {totalAmount.ToString("N2", CultureInfo.InvariantCulture)}h");
    }

    public async Task AggregatePresence(string textFilter, string regexFilter, DateTime? from, DateTime? to)
    {
      var filteredEntries = await FilterPresenceEntries(textFilter, regexFilter, from, to);
      decimal totalPresence = 0m;
      filteredEntries.ForEach(presence =>
      {
        var presenceFrom = ParseTime(presence.From);
        var presenceTo = ParseTime(presence.To);
        totalPresence += (decimal)(presenceTo - presenceFrom).TotalHours;
      });
      Console.WriteLine($"Total: {totalPresence.ToString("N2", CultureInfo.InvariantCulture)}h");
    }

    public static void ParseMonth(string s, out int year, out int month)
    {
      if (Regex.Match(s, @"\d{4}-\d{2}").Success)
      {
        int[] date = s.Split("-").Select(v => int.Parse(v)).ToArray();
        year = date[0];
        month = date[1];
      }
      else if (Regex.Match(s, @"\d{2}.\d{4}").Success)
      {
        int[] date = s.Split(".").Select(v => int.Parse(v)).ToArray();
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
        int[] date = s.Split("-").Select(int.Parse).ToArray();
        return new DateTime(date[0], date[1], date[2]);
      }
      else if (Regex.Match(s, @"\d{2}.\d{2}.\d{4}").Success)
      {
        int[] date = s.Split(".").Select(int.Parse).ToArray();
        return new DateTime(date[2], date[1], date[0]);
      }
      else
      {
        throw new Exception($"{s} is not in the yyyy-MM-dd or dd.MM.yyyy format");
      }
    }

    public static DateTime ParseTime(string s) // 2021-02-04T13:41:00
    {
      var regex = new Regex(@"(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})");
      var match = regex.Match(s);
      if (match.Success)
      {
        int[] date = match.Groups.Values.Skip(1).Select(g => g.Value).Select(int.Parse).ToArray();
        return new DateTime(date[0], date[1], date[2], date[3], date[4], date[5]);
      }
      else
      {
        throw new Exception($"{s} is not in the yyyy-MM-ddThh:mm:ss format");
      }
    }

    private async Task<HashSet<string>> GetHolidayTestHashSet()
    {
      var result = new HashSet<string>();
      foreach (var holiday in await _holidayEntryCacheManager.GetData())
      {
        if (holiday.FromDate == holiday.ToDate)
        {
          result.Add(holiday.FromDate);
        }
        else
        {
          var iter = ParseDay(holiday.FromDate);
          string formatted = holiday.FromDate;
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
      var filteredEntries = await FilterWorkLogEntries(null, null, null, null);
      var holidayTestHashSet = await GetHolidayTestHashSet();
      var firstWorkDay = ParseDay((await _userInfoCacheManager.GetData()).FirstWorkDay);
      var end = atDateTime ?? DateTime.Today;
      decimal totalAmount = 0m;
      decimal expectedAmount = 0m;
      var iter = firstWorkDay;
      int i = 0;
      while (iter < end)
      {
        string formatted = FormatDay(iter);
        if (i < filteredEntries.Count && string.Compare(filteredEntries[i].Date, formatted, StringComparison.InvariantCulture) < 0)
        {
          ++i;
        }

        bool isHoliday = holidayTestHashSet.Contains(formatted);
        bool isWeekend = iter.DayOfWeek == DayOfWeek.Saturday || iter.DayOfWeek == DayOfWeek.Sunday;
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
