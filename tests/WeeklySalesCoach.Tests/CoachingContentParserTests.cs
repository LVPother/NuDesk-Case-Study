using WeeklySalesCoach.Services.Coaching;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class CoachingContentParserTests
{
    [Fact]
    public void Parses_valid_json()
    {
        var content = CoachingContentParser.Parse(TestContent.ValidJson);

        Assert.Equal("Solid week.", content.Summary);
        Assert.Single(content.Strengths);
        Assert.Equal("Summit Auto Repair: 10 days in Documents Pending.", content.Improvements[0].Evidence);
        Assert.Equal("Call every Documents Pending client by Tuesday", content.Actions[0].Action);
        Assert.Empty(content.DataGaps);
    }

    [Fact]
    public void Parses_json_wrapped_in_code_fences()
    {
        var fenced = "```json\n" + TestContent.ValidJson + "\n```";

        Assert.Equal("Solid week.", CoachingContentParser.Parse(fenced).Summary);
    }

    [Fact]
    public void Treats_missing_data_gaps_as_empty()
    {
        var json = TestContent.ValidJson.Replace("\"dataGaps\": []", "\"dataGaps\": null");

        Assert.Empty(CoachingContentParser.Parse(json).DataGaps);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{ \"summary\": ")]
    public void Rejects_empty_or_malformed_json(string raw)
    {
        Assert.Throws<InvalidCoachingContentException>(() => CoachingContentParser.Parse(raw));
    }

    [Fact]
    public void Rejects_a_strength_without_evidence()
    {
        var json = TestContent.ValidJson.Replace("\"evidence\": \"Delgado Family mortgage, $95,000, funded 09-17.\"", "\"evidence\": \"\"");

        var ex = Assert.Throws<InvalidCoachingContentException>(() => CoachingContentParser.Parse(json));
        Assert.Contains("evidence", ex.Message);
    }

    [Fact]
    public void Keeps_only_the_first_three_items_when_the_ai_returns_more()
    {
        var four = CoachingContentParser.Parse(TestContent.ValidJson);
        four.Strengths = [.. Enumerable.Range(1, 4).Select(i => new FeedbackItem { Title = $"S{i}", Detail = "d", Evidence = $"e{i}" })];
        var json = CoachingContentParser.Serialize(four); // what an AI response with a 4th item looks like

        var content = CoachingContentParser.Parse(json);

        Assert.Equal(["S1", "S2", "S3"], content.Strengths.Select(s => s.Title));
    }

    [Fact]
    public void Validate_still_rejects_more_than_three_items_in_manager_edits()
    {
        var content = CoachingContentParser.Parse(TestContent.ValidJson);
        for (var i = 0; i < 3; i++)
            content.Strengths.Add(new FeedbackItem { Title = "t", Detail = "d", Evidence = "e" });

        Assert.Throws<InvalidCoachingContentException>(() => CoachingContentParser.Validate(content));
    }

    [Fact]
    public void Rejects_empty_actions()
    {
        var content = CoachingContentParser.Parse(TestContent.ValidJson);
        content.Actions.Clear();

        Assert.Throws<InvalidCoachingContentException>(() => CoachingContentParser.Validate(content));
    }

    [Fact]
    public void Unescapes_double_escaped_unicode_and_quotes_inside_strings()
    {
        // Some models double-escape inside structured output: the JSON string then contains a literal — or \".
        var json = TestContent.ValidJson
            .Replace("\"summary\": \"Solid week.\"", "\"summary\": \"Solid week \\\\u2014 notes: \\\\\\\"closing not scheduled\\\\\\\"\"");

        var content = CoachingContentParser.Parse(json);

        Assert.Equal("Solid week — notes: \"closing not scheduled\"", content.Summary);
    }

    [Fact]
    public void Serialize_and_FromStored_round_trip()
    {
        var content = CoachingContentParser.Parse(TestContent.ValidJson);

        var restored = CoachingContentParser.FromStored(CoachingContentParser.Serialize(content));

        Assert.Equal(CoachingContentParser.Serialize(content), CoachingContentParser.Serialize(restored));
    }
}
