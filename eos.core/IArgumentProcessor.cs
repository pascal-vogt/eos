using System;
using System.Threading.Tasks;

namespace eos.core;

public interface IArgumentProcessor
{
  public Type GetArgumentsType();
  public Task ProcessArgumentsAsync(object args);
}
