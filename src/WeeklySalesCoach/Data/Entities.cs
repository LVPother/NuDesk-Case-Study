namespace WeeklySalesCoach.Data;

public enum UserRole { Manager, Rep }
public enum FeedbackStyle { Direct, Encouraging }
public enum ReportStatus { NotGenerated, Draft, Approved }
public enum ReportSource { Live, Demo }
public enum EmailStatus { Queued, Sent, Failed }

public class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }
    public int? RepId { get; set; }
    public Rep? Rep { get; set; }
}

public class Rep
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public string Email { get; set; } = "";
    public int TenureMonths { get; set; }
    public decimal WeeklyQuota { get; set; }
    public string PersonalGoal { get; set; } = "";
    public FeedbackStyle FeedbackStyle { get; set; }
}

public class Week
{
    public int Id { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsCurrent { get; set; }
}

public class WeeklyMetrics
{
    public int Id { get; set; }
    public int RepId { get; set; }
    public Rep? Rep { get; set; }
    public int WeekId { get; set; }
    public Week? Week { get; set; }
    public int CallsMade { get; set; }
    public int CallsConnected { get; set; }
    public int EmailsSent { get; set; }
    public int EmailsReplied { get; set; }
    public int NewLeads { get; set; }
    public int ApplicationsSubmitted { get; set; }
    public int DealsWon { get; set; }
    public decimal WonVolume { get; set; }
    public int DealsLost { get; set; }
    public double AvgLeadResponseHours { get; set; }
}

public class Deal
{
    public int Id { get; set; }
    public int RepId { get; set; }
    public Rep? Rep { get; set; }
    public string ClientName { get; set; } = "";
    public string Product { get; set; } = "";
    public decimal Amount { get; set; }
    public string Stage { get; set; } = "";
    public DateOnly CreatedOn { get; set; }
    public DateOnly StageChangedOn { get; set; }
    public DateOnly LastContactOn { get; set; }
    public string Notes { get; set; } = "";
    public string? LostReason { get; set; }
}

public class CoachingReport
{
    public int Id { get; set; }
    public int RepId { get; set; }
    public Rep? Rep { get; set; }
    public int WeekId { get; set; }
    public Week? Week { get; set; }
    public string? ManagerNote { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.NotGenerated;
    public string? ContentJson { get; set; }
    public ReportSource? Source { get; set; }
    public bool IsEdited { get; set; }
    public string? ModelName { get; set; }
    public DateTime? GeneratedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public int? ApprovedByUserId { get; set; }
}

/// <summary>Outbox: the email composed from an approved report. Stays Queued until an email provider is configured.</summary>
public class FeedbackEmail
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public CoachingReport? Report { get; set; }
    public string ToAddress { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public EmailStatus Status { get; set; } = EmailStatus.Queued;
    public string Provider { get; set; } = "";
    public string? Error { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
}
