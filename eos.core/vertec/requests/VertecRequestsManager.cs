namespace eos.core.vertec.requests
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Web;
    using System.Xml;
    using configuration;

    public class VertecRequestsManager
    {
        private readonly Configuration _configuration;
        private readonly HttpClient _httpClient;
        private string _jsonWebToken;

        public VertecRequestsManager(Configuration configuration)
        {
            this._configuration = configuration;
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

        public static string RemoveIllegalXmlCharacters(string xml)
        {
            return Regex.Replace(xml, "[\x00-\x08\x0B\x0C\x0E-\x1F]", string.Empty, RegexOptions.Compiled);
        }

        public async Task<XmlDocument> Execute(VertecRequest request)
        {
            await this.EnsureLoggedIn();
            request.Token = this._jsonWebToken;
            
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

            return xmlDoc;
        }
    }
}