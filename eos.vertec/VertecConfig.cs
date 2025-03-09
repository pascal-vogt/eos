namespace eos.vertec;

public class VertecAggregationConfigItem
{
  public string? Key { get; set; }

  public string? Match { get; set; }

  public string? Replacement { get; set; }
}

public class VertecConfig
{
  public string? BaseUrl { get; set; }
  public string? User { get; set; }
  public string? CacheLocation { get; set; }
  public List<VertecAggregationConfigItem>? AggregationConfig { get; set; }
}
