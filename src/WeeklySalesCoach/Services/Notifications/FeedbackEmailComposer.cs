using System.Globalization;
using System.Text;
using WeeklySalesCoach.Services.Coaching;

namespace WeeklySalesCoach.Services.Notifications;

/// <summary>
/// Turns approved coaching content into a plain-text email. Deterministic on purpose: the text the rep
/// receives is exactly what the manager approved. (Polishing it with the AI would be a second
/// <c>IFeedbackGenerator</c>-style call over this output.)
/// </summary>
public static class FeedbackEmailComposer
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    public static EmailMessage Compose(string toAddress, string repName, string managerName,
        DateOnly weekStart, DateOnly weekEnd, CoachingContent content, bool isUpdate = false)
    {
        var firstName = repName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? repName;
        var subject = (isUpdate ? "Updated: your" : "Your") + $" coaching feedback for the week of {weekStart.ToString("MMM d", Us)}";

        var b = new StringBuilder();
        b.AppendLine($"Hi {firstName},");
        b.AppendLine();
        b.AppendLine(isUpdate
            ? $"{managerName} revised your coaching feedback for {weekStart.ToString("MMM d", Us)} – {weekEnd.ToString("MMM d, yyyy", Us)}. This version replaces the one you received earlier."
            : $"Here is your coaching feedback for {weekStart.ToString("MMM d", Us)} – {weekEnd.ToString("MMM d, yyyy", Us)}, reviewed by {managerName}.");
        b.AppendLine();
        b.AppendLine(content.Summary);
        b.AppendLine();

        b.AppendLine("WHAT WENT WELL");
        foreach (var item in content.Strengths)
            AppendItem(b, item.Title, item.Detail, "Evidence: " + item.Evidence);

        b.AppendLine();
        b.AppendLine("WHERE TO IMPROVE");
        foreach (var item in content.Improvements)
            AppendItem(b, item.Title, item.Detail, "Evidence: " + item.Evidence);

        b.AppendLine();
        b.AppendLine("YOUR FOCUS FOR NEXT WEEK");
        foreach (var action in content.Actions)
            AppendItem(b, action.Action, action.Why, string.IsNullOrWhiteSpace(action.SuccessSignal) ? null : "Success looks like: " + action.SuccessSignal);

        if (content.DataGaps.Count > 0)
        {
            b.AppendLine();
            b.AppendLine("NOT ENOUGH DATA TO ASSESS");
            foreach (var gap in content.DataGaps)
                b.AppendLine($"- {gap}");
        }

        b.AppendLine();
        b.AppendLine("Questions? Reply to this email or grab a few minutes with me this week.");
        b.AppendLine();
        b.AppendLine(managerName);

        return new EmailMessage(toAddress, subject, b.ToString().TrimEnd());
    }

    private static void AppendItem(StringBuilder b, string title, string detail, string? note)
    {
        b.AppendLine($"- {title}");
        if (!string.IsNullOrWhiteSpace(detail)) b.AppendLine($"  {detail}");
        if (!string.IsNullOrWhiteSpace(note)) b.AppendLine($"  {note}");
    }
}
