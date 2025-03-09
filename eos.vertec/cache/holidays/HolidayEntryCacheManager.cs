namespace eos.core.vertec.cache.holidays
{
  using System.Collections.Generic;
  using System.Linq;
  using System.Threading.Tasks;
  using System.Xml;
  using configuration;
  using requests;

  public class HolidayEntryCacheManager : VertecKeylessCacheManager<List<HolidayEntry>>
  {
    private readonly VertecRequestsManager _vertecRequestsManager;

    public HolidayEntryCacheManager(Configuration configuration, VertecRequestsManager vertecRequestsManager)
      : base(configuration, "holidays")
    {
      this._vertecRequestsManager = vertecRequestsManager;
    }

    public override string GetFileName()
    {
      return "holidays.json";
    }

    public override async Task<List<HolidayEntry>> LoadDataRemotely()
    {
      var request = new VertecRequest
      {
        Ocl = $"abwesenheit",
        Members = new[] { "datum", "bisDatum", "beschreibung" },
        Expressions = new KeyValuePair<string, string>[] { },
      };

      var xmlDoc = await _vertecRequestsManager.Execute(request);

      var entries = new List<HolidayEntry>();
      foreach (XmlElement leistung in xmlDoc.DocumentElement.ChildNodes[0].ChildNodes[0].ChildNodes)
      {
        var entry = new HolidayEntry();
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

      return entries.OrderBy(o => o.FromDate).ThenBy(o => o.ToDate).ToList();
    }
  }
}
