namespace eos.core.configuration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    
    public class VertecAggregationConfigItem
    {
        public string Key { get; set; }
        
        public string Match { get; set; }
        
        public string Replacement { get; set; }
    }

    public class Configuration
    {
        public string VertecURL { get; set; }
        
        public string VertecUser { get; set; }

        public string VertecCacheLocation { get; set; }
        
        public List<VertecAggregationConfigItem> VertecAggregationConfig { get; set; }

        public static string GetConfigFilePath()
        {
            return Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "eos.config");
        }
        
        public static async Task<Configuration> Load()
        {
            var text = await File.ReadAllTextAsync(GetConfigFilePath(), Encoding.UTF8);
            return JsonSerializer.Deserialize<Configuration>(text);
        }
        
        public static bool HasConfigFile()
        {
            return File.Exists(GetConfigFilePath());
        }

        public async Task Save()
        {
            var text = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            var path = GetConfigFilePath();
            await File.WriteAllTextAsync(path, text, Encoding.UTF8);
            Console.WriteLine($"Initialized {path}");
        }
    }
}