namespace eos.core.vertec
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Xml;

    public class VertecRequest
    {
        public string Token { get; set; }
        public string Ocl { get; set; }
        
        public IEnumerable<string> Members { get; set; }
        
        public IEnumerable<KeyValuePair<string, string>> Expressions { get; set; }

        public VertecRequest()
        {
        }

        public override string ToString()
        {
            var doc = new XmlDocument();
            var envelope = doc.CreateElement("Envelope");
            doc.AppendChild(envelope);
            var header = doc.CreateElement("Header");
            envelope.AppendChild(header);
            var basicAuth = doc.CreateElement("BasicAuth");
            header.AppendChild(basicAuth);
            var token = doc.CreateElement("Token");
            token.InnerText = this.Token;
            basicAuth.AppendChild(token);
            var query = doc.CreateElement("Query");
            envelope.AppendChild(query);
            var selection = doc.CreateElement("Selection");
            query.AppendChild(selection);
            var ocl = doc.CreateElement("ocl");
            ocl.InnerText = this.Ocl;
            selection.AppendChild(ocl);
            var resultdef = doc.CreateElement("Resultdef");
            query.AppendChild(resultdef);
            foreach (var member in this.Members) 
            {
                var memberEl = doc.CreateElement("member");
                memberEl.InnerText = member;
                resultdef.AppendChild(memberEl);
            }
            foreach (var expression in this.Expressions)
            {
                var expressionEl = doc.CreateElement("expression");
                resultdef.AppendChild(expressionEl);
                var alias = doc.CreateElement("alias");
                alias.InnerText = expression.Key;
                expressionEl.AppendChild(alias);
                var expressionOcl = doc.CreateElement("ocl");
                expressionOcl.InnerText = expression.Value;
                expressionEl.AppendChild(expressionOcl);
            }
            var ms = new MemoryStream();
            doc.Save(ms);
            
            var settings = new XmlWriterSettings();
            settings.OmitXmlDeclaration = true;
            settings.Indent = true;
            settings.NewLineOnAttributes = true;

            var stringBuilder = new StringBuilder();
            using (var xmlWriter = XmlWriter.Create(stringBuilder, settings))
            {
                doc.Save(xmlWriter);
            }

            return stringBuilder.ToString();
        }
    }
}