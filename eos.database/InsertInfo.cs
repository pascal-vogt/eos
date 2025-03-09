namespace eos.core.database
{
  using System.Collections.Generic;

  public class InsertInfo
  {
    public string Sql { get; set; }
    public string PrimaryKeyVariable { get; set; }
    public List<string> ReferencedVariables { get; set; }
  }
}
