using eos.cli.options;
using eos.core.configuration;

namespace eos.core.vertec;

public class VertecArgumentsProcessor : AbstractArgumentProcessor<VertecOptions>
{
  public override Type GetArgumentsType() => typeof(VertecOptions);

  protected override async Task ProcessArgumentsAsync(VertecOptions o)
  {
    var config = await Configuration.Load();
    var vertec = new VertecInterface(config);

    DateTime? from = null;
    if (o.From != null)
    {
      from = VertecInterface.ParseDay(o.From);
    }

    DateTime? to = null;
    if (o.To != null)
    {
      to = VertecInterface.ParseDay(o.To);
    }

    if (o.Today)
    {
      from = DateTime.Today;
      to = DateTime.Today;
    }

    if (o.CurrentMonth)
    {
      var today = DateTime.Today;
      from = new DateTime(today.Year, today.Month, 1);
      to = today;
    }

    if (o.InitCache)
    {
      await vertec.InitCache();
    }
    else if (o.CachedMonthToUpdate != null)
    {
      VertecInterface.ParseMonth(o.CachedMonthToUpdate, out int year, out int month);

      await vertec.UpdateMonth(year, month);
    }
    else if (o.List)
    {
      await vertec.ListWorkLogEntries(o.Text, o.Regex, from, to);
    }
    else if (o.ListPresence)
    {
      await vertec.ListPresenceEntries(o.Text, o.Regex, from, to);
    }
    else if (o.Aggregate)
    {
      await vertec.Aggregate(o.Text, o.Regex, from, to);
    }
    else if (o.AggregatePresence)
    {
      await vertec.AggregatePresence(o.Text, o.Regex, from, to);
    }
    else if (o.Overtime)
    {
      await vertec.Overtime(null);
    }
    else if (o.OvertimeAt != null)
    {
      DateTime? at = VertecInterface.ParseDay(o.OvertimeAt);
      await vertec.Overtime(at);
    }
    else if (o.CheckPresence)
    {
      await vertec.CheckPresence(from, to);
    }
  }
}
