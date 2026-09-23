using WeeklySalesCoach.Data;

namespace WeeklySalesCoach.Services.Coaching;

/// <summary>Everything the AI (and the reviewing manager) sees for one rep and one week.</summary>
public record CoachingContext(
    int RepId,
    int WeekId,
    DateOnly WeekStart,
    DateOnly WeekEnd,
    string DealTerm,
    string RepTerm,
    RepProfile Rep,
    MetricsSnapshot Current,
    MetricsSnapshot? Previous,
    MetricsSnapshot TeamAverage,
    double QuotaAttainmentPercent,
    IReadOnlyList<DealSnapshot> Deals,
    string? ManagerNote);

public record RepProfile(string Name, string Title, int TenureMonths, decimal WeeklyQuota, string PersonalGoal, FeedbackStyle FeedbackStyle);

public record MetricsSnapshot(
    double CallsMade,
    double CallsConnected,
    double EmailsSent,
    double EmailsReplied,
    double NewLeads,
    double ApplicationsSubmitted,
    double DealsWon,
    decimal WonVolume,
    double DealsLost,
    double AvgLeadResponseHours);

public record DealSnapshot(
    string Client,
    string Product,
    decimal Amount,
    string Stage,
    int DaysInStage,
    int DaysSinceLastContact,
    string Notes,
    string? LostReason);
