using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using eos.core.configuration;
using Microsoft.Extensions.DependencyInjection;

namespace eos.core;

public static class PluginLoader
{
  public static async Task LoadPluginsAsync(string pluginFolder, IServiceCollection serviceCollection)
  {
    string[] pluginFiles = Directory.GetFiles(pluginFolder, "eos.*.dll");
    foreach (string pluginFile in pluginFiles)
    {
      try
      {
        var assembly = Assembly.LoadFrom(pluginFile);
        var pluginTypes = assembly.GetTypes().Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var pluginType in pluginTypes)
        {
          IPlugin plugin = (IPlugin)Activator.CreateInstance(pluginType);
          if (plugin != null)
          {
            await plugin.InitializeAsync(serviceCollection);
            Type configType = plugin.GetPluginConfigType();
            if (configType != null)
            {
              Configuration.RegisterConfigPart(configType);
            }
          }
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error loading plugin from {pluginFile}: {ex.Message}");
      }
    }
  }
}
