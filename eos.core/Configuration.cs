using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace eos.core.configuration
{
  public class Configuration
  {
    private JsonObject _config;
    private static List<Type> s_partsRegistry = new();
    private static JsonSerializerOptions s_jsonSerializerOptions = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never };

    private static string GetConfigFilePath()
    {
      return Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "eos.config");
    }

    public static async Task<Configuration> Load()
    {
      await using FileStream text = File.OpenRead(GetConfigFilePath());
      return new Configuration
      {
        _config = (
          await JsonNode.ParseAsync(
            text,
            new JsonNodeOptions { PropertyNameCaseInsensitive = true },
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip },
            CancellationToken.None
          )
        )?.AsObject(),
      };
    }

    public static bool HasConfigFile()
    {
      return File.Exists(GetConfigFilePath());
    }

    public T Get<T>()
      where T : class
    {
      var subConfig = this._config[typeof(T).Name];
      if (subConfig == null)
      {
        var constructor = typeof(T).GetConstructor([]);
        if (constructor == null)
        {
          throw new Exception($"No parameterless constructor found for type {typeof(T).Name}");
        }
        T value = (T)constructor.Invoke([]);
        this._config[typeof(T).Name] = JsonSerializer.SerializeToNode(value, s_jsonSerializerOptions);
        return value;
      }

      return subConfig.Deserialize<T>()!;
    }

    public void Set<T>(T value)
    {
      this._config[typeof(T).Name] = JsonSerializer.SerializeToNode(value, s_jsonSerializerOptions);
    }

    public async Task Save()
    {
      foreach (var part in s_partsRegistry)
      {
        if (!this._config.ContainsKey(part.Name))
        {
          var constructor = part.GetConstructor([]);
          if (constructor == null)
          {
            throw new Exception($"No parameterless constructor found for type {part.Name}");
          }
          object value = constructor.Invoke([]);
          this._config.Add(part.Name, JsonSerializer.SerializeToNode(value, s_jsonSerializerOptions));
        }
      }
      string text = JsonSerializer.Serialize(_config, s_jsonSerializerOptions);
      string path = GetConfigFilePath();
      await File.WriteAllTextAsync(path, text, Encoding.UTF8);
      Console.WriteLine($"Saved {path}");
    }

    public static void RegisterConfigPart(Type configType)
    {
      s_partsRegistry.Add(configType);
    }
  }
}
