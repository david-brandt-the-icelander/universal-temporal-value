using System.Globalization;
using System.Threading;
using Xunit;

namespace Utv.Tests;

public class CultureIndependenceTests
{
    [Fact]
    public void OutputDoesNotDependOnTheCurrentCulture()
    {
        const string json = """{"schema":"utv/1.0","kind":"civil","value":{"form":"date","fields":{"year":"-1","month":2,"day":29},"calendar":{"id":"gregory","chronology":"proleptic"}}}""";
        string invariant = Run(CultureInfo.InvariantCulture);

        // A culture with a different negative sign and digit grouping; constructible even in invariant-globalization mode.
        var odd = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        odd.NumberFormat.NegativeSign = "~";
        odd.NumberFormat.NumberGroupSeparator = "'";
        Assert.Equal(invariant, Run(odd));
        Assert.Contains("day 29 is not in 1..28 for -1-02", invariant, System.StringComparison.Ordinal);
        Assert.Contains("month -12 is not in 1..12", invariant, System.StringComparison.Ordinal);
        Assert.DoesNotContain("~", invariant, System.StringComparison.Ordinal);

        string Run(CultureInfo culture)
        {
            CultureInfo saved = CultureInfo.CurrentCulture;
            CultureInfo savedUi = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                return GregorianCivil.Validate(json).ToJson()
                    + UtvValidator.ValidateStructural("""{"schema":"utv/1.0","kind":"civil","value":{"form":"date","fields":{"month":-12},"calendar":{"id":"gregory","chronology":"proleptic"}}}""").ToJson()
                    + GregorianCivil.Validate("""{"schema":"utv/1.0","kind":"civil","value":{"form":"date","fields":{"month":-12},"calendar":{"id":"gregory","chronology":"proleptic"}}}""").ToJson();
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
                CultureInfo.CurrentUICulture = savedUi;
            }
        }
    }
}
