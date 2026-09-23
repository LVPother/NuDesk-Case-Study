using WeeklySalesCoach.Services;

namespace WeeklySalesCoach.Tests;

public class FmtTests
{
    [Fact]
    public void Formats_money_percent_and_compact_values_in_en_us()
    {
        Assert.Equal("$95,000", Fmt.Money(95_000m));
        Assert.Equal("$312K", Fmt.CompactMoney(312_000m));
        Assert.Equal("$1.2M", Fmt.CompactMoney(1_200_000m));
        Assert.Equal("$950", Fmt.CompactMoney(950m));
        Assert.Equal("75%", Fmt.Percent(75.0));
        Assert.Equal("58.7%", Fmt.Percent(58.7));
    }
}
