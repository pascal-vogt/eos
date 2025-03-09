using eos.vertec;

namespace eos.core.vertec.cache
{
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Text.Json;
  using System.Threading.Tasks;
  using configuration;

  public class DummyCacheKey
  {
    public static readonly DummyCacheKey Instance = new DummyCacheKey();

    public override string ToString()
    {
      return string.Empty;
    }
  }

  public abstract class VertecKeylessCacheManager<T> : VertecCacheManager<DummyCacheKey, T>
    where T : class
  {
    public VertecKeylessCacheManager(Configuration configuration, string name)
      : base(configuration, name) { }

    public abstract string GetFileName();

    public override string GetFileName(DummyCacheKey cacheKey)
    {
      return this.GetFileName();
    }

    public override DummyCacheKey GetCacheKey(string fileName)
    {
      if (fileName == this.GetFileName(DummyCacheKey.Instance))
      {
        return DummyCacheKey.Instance;
      }

      return null;
    }

    public abstract Task<T> LoadDataRemotely();

    public override async Task<T> LoadDataRemotely(DummyCacheKey cacheKey)
    {
      return await this.LoadDataRemotely();
    }

    public async Task<T> GetData()
    {
      return await this.GetData(DummyCacheKey.Instance);
    }

    public async Task<T> ForceReCache()
    {
      return await this.ForceReCache(DummyCacheKey.Instance);
    }
  }

  public abstract class VertecCacheManager<T, U>
    where T : class
    where U : class
  {
    protected readonly VertecConfig _vertecConfig;
    private readonly string _name;

    public VertecCacheManager(Configuration configuration, string name)
    {
      this._vertecConfig = configuration.Get<VertecConfig>();
      this._name = name;
    }

    public abstract string GetFileName(T cacheKey);

    public string GetFilePath(T cacheKey)
    {
      return Path.Join(this._vertecConfig.CacheLocation, this.GetFileName(cacheKey));
    }

    public abstract T GetCacheKey(string fileName);

    public List<T> GetAllCacheKeys()
    {
      return Directory
        .GetFiles(this._vertecConfig.CacheLocation)
        .Select(path => Path.GetFileName(path))
        .Select(file => this.GetCacheKey(file))
        .Where(key => key != null)
        .ToList();
    }

    public abstract Task<U> LoadDataRemotely(T cacheKey);

    public async Task<U> GetData(T cacheKey)
    {
      string path = this.GetFilePath(cacheKey);
      if (File.Exists(path))
      {
        string text = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<U>(text);
      }
      else
      {
        return await this.ForceReCache(cacheKey);
      }
    }

    public async Task<U> ForceReCache(T cacheKey)
    {
      Console.WriteLine($"Caching {_name} {cacheKey}");
      var data = await this.LoadDataRemotely(cacheKey);
      await this.SetData(cacheKey, data);
      return data;
    }

    public async Task SetData(T cacheKey, U data)
    {
      string jsonString = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
      await File.WriteAllTextAsync(this.GetFilePath(cacheKey), jsonString);
    }
  }
}
