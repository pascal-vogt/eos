using System;
using System.Threading.Tasks;
using eos.cli.options;
using eos.core.configuration;

namespace eos.core;

public class ConfigArgumentsProcessor : AbstractArgumentProcessor<ConfigOptions>
{
  public override Type GetArgumentsType() => typeof(ConfigOptions);

  protected override async Task ProcessArgumentsAsync(ConfigOptions o)
  {
    if (o.InitConfig)
    {
      Configuration config;
      if (Configuration.HasConfigFile())
      {
        config = await Configuration.Load();
      }
      else
      {
        config = new Configuration();
      }

      await config.Save();
    }
  }
}
