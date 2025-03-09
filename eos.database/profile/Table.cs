namespace eos.core.database
{
  public class Table
  {
    public string Name { get; set; }
    public bool Export { get; set; }
    public bool IsEntryPoint { get; set; }
    public string LookupProperty { get; set; }
  }
}
