namespace eos.core.database
{
  public class KeyDescriptor
  {
    public TableDescriptor TableDescriptor { get; set; }
    public string ColumnName { get; set; }

    public override string ToString()
    {
      return $"{TableDescriptor}.[{ColumnName}]";
    }
  }
}
