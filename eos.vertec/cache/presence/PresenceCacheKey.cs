namespace eos.core.vertec.cache.presence
{
  using System;

  public class PresenceCacheKey
  {
    public PresenceCacheKey() { }

    public PresenceCacheKey(DateTime o)
    {
      this.Year = o.Year;
      this.Month = o.Month;
    }

    public int Year { get; set; }

    public int Month { get; set; }

    public DateTime RangeStart => new DateTime(Year, Month, 1);

    public DateTime RangeEnd => new DateTime(Year, Month, DateTime.DaysInMonth(Year, Month));

    public override string ToString()
    {
      return $"{Month:D2}.{Year:D4}";
    }
  }
}
