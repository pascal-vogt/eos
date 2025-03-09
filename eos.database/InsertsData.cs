namespace eos.core.database
{
  using System.Collections.Generic;

  public class InsertsData
  {
    public List<InsertInfo> Inserts { get; set; }
    public Profile Profile { get; set; }
    public string MainTable { get; set; }
    public Dictionary<string, string> IdToVariable { get; set; }
    public Dictionary<string, long> RowsCountPerTable { get; set; }
    public HashSet<string> IdHasBeenDefined { get; set; }
  }
}
