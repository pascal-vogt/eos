using eos.core;
using eos.core.vertec;
using Microsoft.Extensions.DependencyInjection;

namespace eos.vertec;

public class VertecPlugin : IPlugin
{
  public Task InitializeAsync(IServiceCollection serviceCollection)
  {
    serviceCollection.AddSingleton<IArgumentProcessor, VertecArgumentsProcessor>();
    return Task.CompletedTask;
  }

  public Type? GetPluginConfigType() => typeof(VertecConfig);
}
