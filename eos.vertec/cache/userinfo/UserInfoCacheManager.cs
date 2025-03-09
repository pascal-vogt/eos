namespace eos.core.vertec.cache.userinfo
{
  using System.Collections.Generic;
  using System.IO;
  using System.Net.Http;
  using System.Text;
  using System.Text.Json;
  using System.Threading.Tasks;
  using System.Xml;
  using configuration;
  using requests;

  public class UserInfoCacheManager : VertecKeylessCacheManager<UserInfo>
  {
    private readonly VertecRequestsManager _vertecRequestsManager;

    public UserInfoCacheManager(Configuration configuration, VertecRequestsManager vertecRequestsManager)
      : base(configuration, "user info")
    {
      this._vertecRequestsManager = vertecRequestsManager;
    }

    public override string GetFileName()
    {
      return "userinfo.json";
    }

    public override async Task<UserInfo> LoadDataRemotely()
    {
      var request = new VertecRequest
      {
        Ocl = $"projektBearbeiter->select(loginName = '{this._vertecConfig.User}')",
        Members = new[] { "eintrittPer" },
        Expressions = new KeyValuePair<string, string>[] { },
      };

      var xmlDoc = await this._vertecRequestsManager.Execute(request);

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
              return new UserInfo { FirstWorkDay = propertyTag.InnerText };
          }
        }
      }

      return null;
    }
  }
}
