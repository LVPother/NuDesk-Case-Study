namespace WeeklySalesCoach.Services.Coaching;

public static class Calc
{
    /// <summary>Won volume as a percentage of quota, rounded to 1 decimal. 0 when there is no quota.</summary>
    public static double Attainment(decimal won, decimal quota) =>
        quota <= 0 ? 0 : Math.Round((double)(won / quota) * 100, 1);

    public static int DaysBetween(DateOnly from, DateOnly to) => Math.Max(0, to.DayNumber - from.DayNumber);
}
