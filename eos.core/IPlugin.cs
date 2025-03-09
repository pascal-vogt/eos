using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace eos.core;

public interface IPlugin
{
  Task InitializeAsync(IServiceCollection serviceCollection);
  Type? GetPluginConfigType();
}
