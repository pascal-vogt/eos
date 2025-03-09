using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using eos.core;
using Microsoft.Extensions.DependencyInjection;

namespace eos.cli
{
  class Program
  {
    static async Task Main(string[] args)
    {
      var serviceCollection = new ServiceCollection();
      serviceCollection.AddSingleton<IArgumentProcessor, ConfigArgumentsProcessor>();
      string? pluginFolder = Path.GetDirectoryName(Environment.ProcessPath);
      if (pluginFolder != null)
      {
        await PluginLoader.LoadPluginsAsync(pluginFolder, serviceCollection);
      }
      var serviceProvider = serviceCollection.BuildServiceProvider();
      var argumentProcessors = serviceProvider.GetServices<IArgumentProcessor>().ToList();

      await Parser
        .Default.ParseArguments(args, argumentProcessors.Select(o => o.GetArgumentsType()).ToArray())
        .WithParsedAsync(options => ProcessOptions(options, argumentProcessors));
    }

    private static async Task ProcessOptions(object optionsToProcess, List<IArgumentProcessor> argumentProcessors)
    {
      await argumentProcessors.First(o => o.GetArgumentsType() == optionsToProcess.GetType()).ProcessArgumentsAsync(optionsToProcess);
    }
  }
}
