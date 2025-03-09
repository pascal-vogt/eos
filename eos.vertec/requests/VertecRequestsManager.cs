using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using eos.core.configuration;
using eos.vertec;

namespace eos.core.vertec.requests
{
  public class VertecRequestsManager
  {
    private readonly VertecConfig _vertecConfig;
    private readonly HttpClient _httpClient;
    private string _jsonWebToken;

    public VertecRequestsManager(Configuration configuration)
    {
      this._vertecConfig = configuration.Get<VertecConfig>();
      this._httpClient = new HttpClient();
    }

    private async Task EnsureLoggedIn()
    {
      if (this._jsonWebToken == null)
      {
        await this.Login();
      }
    }

    private async Task Login()
    {
      string vertecPassword = GetVertecPassword(this._vertecConfig);

      string userNameUrlEncoded = HttpUtility.UrlEncode(this._vertecConfig.User);
      string passwordUrlEncoded = HttpUtility.UrlEncode(vertecPassword);
      var result = await this._httpClient.PostAsync(
        $"{this._vertecConfig.BaseUrl}/auth/xml",
        new StringContent($"vertec_username={userNameUrlEncoded}&password={passwordUrlEncoded}", Encoding.UTF8)
      );
      this._jsonWebToken = await result.Content.ReadAsStringAsync();
    }

    public static string GetVertecPassword(VertecConfig configuration)
    {
      var pass = new StringBuilder();
      Console.Write($"Vertec password for {configuration.User}:");
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
      } while (key.Key != ConsoleKey.Enter);
      Console.WriteLine();
      string vertecPassword = pass.ToString();
      return vertecPassword;
    }

    public static string RemoveIllegalXmlCharacters(string xml)
    {
      return Regex.Replace(xml, "[\x00-\x08\x0B\x0C\x0E-\x1F]", string.Empty, RegexOptions.Compiled);
    }

    public async Task<XmlDocument> Execute(VertecRequest request)
    {
      await this.EnsureLoggedIn();
      request.Token = this._jsonWebToken;

      var result = await this._httpClient.PostAsync($"{this._vertecConfig.BaseUrl}/xml", new StringContent(request.ToString(), Encoding.UTF8));

      string xmlOut = await result.Content.ReadAsStringAsync();

      // this shouldn't be necessary but i've seen Vertec produce illegal XML characters and filtering them out
      // seems to be the easiest way to deal with this problem considering we cannot change their source code
      xmlOut = RemoveIllegalXmlCharacters(xmlOut);

      var xmlDoc = new XmlDocument();
      try
      {
        xmlDoc.Load(new MemoryStream(Encoding.UTF8.GetBytes(xmlOut)));
      }
      catch (Exception)
      {
        Console.WriteLine(xmlOut);
        throw;
      }

      return xmlDoc;
    }
  }
}
