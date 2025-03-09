using eos.core;
using Microsoft.Extensions.DependencyInjection;

namespace eos.database;

public class DatabasePlugin : IPlugin
{
  public Task InitializeAsync(IServiceCollection serviceCollection)
  {
    serviceCollection.AddSingleton<IArgumentProcessor, DatabaseArgumentsProcessor>();
    return Task.CompletedTask;
  }

  public Type? GetPluginConfigType() => null;
}
