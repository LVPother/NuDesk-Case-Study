using System.Globalization;

namespace WeeklySalesCoach.Services;

/// <summary>Display formatting, always en-US regardless of the server's culture.</summary>
public static class Fmt
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    public static string Money(decimal value) => value.ToString("C0", Us);

    public static string CompactMoney(decimal value) => value switch
    {
        >= 1_000_000m => "$" + (value / 1_000_000m).ToString("0.#", Us) + "M",
        >= 1_000m => "$" + (value / 1_000m).ToString("0.#", Us) + "K",
        _ => Money(value),
    };

    public static string Percent(double value) => value.ToString("0.#", Us) + "%";
}
