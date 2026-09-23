namespace WeeklySalesCoach.Services.Coaching;

/// <summary>The structured coaching feedback. Mutable so the manager can edit it in place.</summary>
public class CoachingContent
{
    public string Summary { get; set; } = "";
    public List<FeedbackItem> Strengths { get; set; } = [];
    public List<FeedbackItem> Improvements { get; set; } = [];
    public List<ActionItem> Actions { get; set; } = [];
    public List<string> DataGaps { get; set; } = [];
}

public class FeedbackItem
{
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Evidence { get; set; } = "";
}

public class ActionItem
{
    public string Action { get; set; } = "";
    public string Why { get; set; } = "";
    public string SuccessSignal { get; set; } = "";
}

public class InvalidCoachingContentException(string message, Exception? inner = null) : Exception(message, inner);
