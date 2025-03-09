using System;
using System.Threading.Tasks;

namespace eos.core;

public abstract class AbstractArgumentProcessor<T> : IArgumentProcessor
  where T : class
{
  public async Task ProcessArgumentsAsync(object args)
  {
    await this.ProcessArgumentsAsync((T)args);
  }

  public abstract Type GetArgumentsType();
  protected abstract Task ProcessArgumentsAsync(T args);
}
