# Weekly Sales Coach Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Blazor web app where AI drafts weekly, evidence-based coaching feedback for each loan officer, and a manager reviews, edits and approves it before the rep sees it.

**Architecture:** A single Blazor Web App (.NET 10). Pages use static SSR by default, and the manager pages opt into `InteractiveServer`, so all code and the Gemini API key stay on the server. EF Core on SQLite stores the data, which is seeded on first run. The AI sits behind `IFeedbackGenerator`: `GeminiFeedbackGenerator` is used when an API key is configured, and `DemoFeedbackGenerator` (pre-generated JSON) otherwise. `CoachingService` owns the report lifecycle: generate → validate (with one retry) → Draft → edit → Approve.

**Tech Stack:** .NET 10 SDK (10.0.300), Blazor Web App (Interactive Server per page), EF Core 10 + SQLite, cookie authentication, ASP.NET Core `PasswordHasher`, Gemini REST API (`generateContent` with structured JSON output), xUnit.

**Spec:** `docs/superpowers/specs/2026-09-22-weekly-sales-coach-design.md`

## Global Constraints

- Target framework `net10.0`; solution root `C:\Users\Eduardo\Desarrollo2`. All commands below run from that root unless stated otherwise.
- **No git.** The author will run `git init` and make a single commit/push only when the code is final. Every "Checkpoint" step replaces the usual commit: run the full test suite and confirm it is green. Never run `git` commands.
- UI language: **English**.
- The Gemini API key must **never** be committed, sent to the browser, logged or shown in errors. It comes only from `dotnet user-secrets` or the environment variable `Gemini__ApiKey`.
- Style: nuDesk palette. Ink `#1F2937`, accent green `#3FA45B`, background `#F7F8F8`, mint `#E4F0EE`, muted text `#5B6B7A`, white cards with a subtle border, Manrope font. Do **not** use nuDesk's logo or name as the app's brand.
- The AI drafts and the manager decides: no numeric scoring or ranking of reps and no HR recommendations. Reps see only their own **approved** reports.
- Never collect or send sensitive attributes (age, gender, health, etc.). The AI input never contains other reps' names or individual data.
- Demo week: current = Mon 2026-09-14 → Fri 2026-09-18; previous = 2026-09-07 → 2026-09-11.
- Quick-login demo buttons must show the disclaimer: "Demo accounts — for demonstration purposes only."
- Demo-mode banner text: "Demo mode: showing pre-generated AI feedback. Configure a Gemini API key to generate live (see README)."

## Review Focus

1. **A rep with no previous-week data** (e.g. a new hire): the context must have `Previous = null`, the prompt must say so, and the evidence panel must show "—". No crash. → Task 5 test `Previous_is_null_when_there_is_no_previous_week`.
2. **A rep whose quota is 0**: attainment is 0% with no divide-by-zero, in both the context and the dashboard. → Task 5 test `Quota_attainment_is_zero_when_quota_is_zero`, Task 8 test `Attainment_is_zero_for_zero_quota`.
3. **The AI wraps its JSON in markdown code fences** (```` ```json ````): the response is still parsed. → Task 4 test `Parses_json_wrapped_in_code_fences`.
4. **A manager tries to change an approved report** (edit, regenerate or change the note): the change is rejected with a clear message and nothing changes. → Task 7 test `Approved_report_cannot_be_edited_regenerated_or_noted`.
5. **The demo feedback file lacks an entry for a rep** (e.g. a rep added to the seed later): the user gets a friendly "unavailable" error, not a crash, and the committed file must cover every seeded rep. → Task 6 tests `Unknown_rep_raises_unavailable` and `Committed_demo_feedback_covers_every_seeded_rep`.

---

## File Map

```
WeeklySalesCoach.slnx
src/WeeklySalesCoach/
  WeeklySalesCoach.csproj
  Program.cs                                  DI, auth, DB init, endpoints, export flag
  appsettings.json                            Domain/Gemini/Demo settings, connection string
  Configuration/AppOptions.cs                 DomainOptions, GeminiOptions, DemoOptions
  Data/Entities.cs                            AppUser, Rep, Week, WeeklyMetrics, Deal, CoachingReport + enums
  Data/AppDbContext.cs                        EF Core model
  Data/SeedData.cs                            demo team (FinServ loan officers)
  Data/DatabaseInitializer.cs                 EnsureCreated + seed on startup
  Data/Seed/demo-feedback.json                pre-generated feedback per rep (demo mode)
  Services/Fmt.cs                             money/percent formatting (en-US)
  Services/Coaching/CoachingContent.cs        feedback model (Summary, Strengths, Improvements, Actions, DataGaps)
  Services/Coaching/CoachingContentParser.cs  parse + validate + serialize AI output
  Services/Coaching/CoachingContext.cs        records describing what the AI sees
  Services/Coaching/Calc.cs                   attainment, day differences
  Services/Coaching/CoachingContextBuilder.cs builds CoachingContext from the DB
  Services/Coaching/CoachingService.cs        report lifecycle (generate/edit/approve/read)
  Services/Coaching/TeamDashboardService.cs   KPIs + rep cards for the dashboard
  Services/AI/IFeedbackGenerator.cs           interface + AiUnavailableException + AiFailureKind
  Services/AI/CoachingPrompt.cs               system instructions, user message, response schema
  Services/AI/GeminiFeedbackGenerator.cs      live Gemini REST client
  Services/AI/DemoFeedbackGenerator.cs        reads demo-feedback.json
  Services/AI/AiErrorMessages.cs              user-facing error text
  Services/AI/DemoFeedbackExporter.cs         dev-only: regenerate demo-feedback.json with live AI
  Auth/UserAuthService.cs                     credential check + ClaimsPrincipal
  Auth/ClaimsPrincipalExtensions.cs           GetUserId / GetRepId
  Auth/AuthEndpoints.cs                       POST /auth/login, /auth/quick-login, /auth/logout
  Components/App.razor                        (modify head: fonts)
  Components/Routes.razor                     AuthorizeRouteView
  Components/_Imports.razor
  Components/Layout/MainLayout.razor          top bar, sign-out, footer
  Components/Shared/PillBadge.razor, StatCard.razor, StatusChip.razor, DemoBanner.razor,
                    FeedbackView.razor, FeedbackItemsEditor.razor, EvidencePanel.razor
  Components/Pages/Login.razor                static SSR login + demo quick-login
  Components/Pages/Dashboard.razor            manager team view (interactive)
  Components/Pages/Review.razor               manager review/edit/approve (interactive)
  Components/Pages/MyFeedback.razor           rep's approved feedback (static SSR)
  Components/Pages/Error.razor
  wwwroot/app.css                             nuDesk-style design tokens and components
tests/WeeklySalesCoach.Tests/
  WeeklySalesCoach.Tests.csproj
  TestSupport/TestDb.cs, TestData.cs, TestContent.cs, FakeFeedbackGenerator.cs
  AppDbContextTests.cs, SeedDataTests.cs, CoachingContentParserTests.cs,
  CoachingContextBuilderTests.cs, CoachingPromptTests.cs, GeminiFeedbackGeneratorTests.cs,
  DemoFeedbackGeneratorTests.cs, AiErrorMessagesTests.cs, CoachingServiceTests.cs,
  TeamDashboardServiceTests.cs, FmtTests.cs, UserAuthServiceTests.cs, DemoFeedbackExporterTests.cs
README.md
docs/screenshots/*.png
```

---

### Task 1: Solution scaffold

**Files:**
- Create: `WeeklySalesCoach.slnx`, `src/WeeklySalesCoach/*` (template), `tests/WeeklySalesCoach.Tests/*` (template), `.gitignore`
- Modify: `src/WeeklySalesCoach/WeeklySalesCoach.csproj`
- Delete: `tests/WeeklySalesCoach.Tests/UnitTest1.cs`

**Interfaces:**
- Consumes: nothing
- Produces: web project namespace `WeeklySalesCoach`; test project namespace `WeeklySalesCoach.Tests` referencing the web project.

- [ ] **Step 1: Create the solution and projects**

```bash
cd /c/Users/Eduardo/Desarrollo2
dotnet new sln -n WeeklySalesCoach
dotnet new blazor -n WeeklySalesCoach -o src/WeeklySalesCoach -int Server -e
dotnet new xunit -n WeeklySalesCoach.Tests -o tests/WeeklySalesCoach.Tests
dotnet sln add src/WeeklySalesCoach/WeeklySalesCoach.csproj tests/WeeklySalesCoach.Tests/WeeklySalesCoach.Tests.csproj
dotnet add tests/WeeklySalesCoach.Tests reference src/WeeklySalesCoach
dotnet add src/WeeklySalesCoach package Microsoft.EntityFrameworkCore.Sqlite
dotnet new gitignore
rm tests/WeeklySalesCoach.Tests/UnitTest1.cs
```

(`dotnet new sln` in .NET 10 creates `WeeklySalesCoach.slnx`.)

- [ ] **Step 2: Make `dotnet run` use the project folder as its working directory, and ignore local DB files**

In `src/WeeklySalesCoach/WeeklySalesCoach.csproj`, add inside the first `<PropertyGroup>`:

```xml
    <RunWorkingDirectory>$(MSBuildProjectDirectory)</RunWorkingDirectory>
```

Append to `.gitignore`:

```
# Local SQLite database
*.db
*.db-shm
*.db-wal
```

- [ ] **Step 3: Verify it builds**

Run: `dotnet build`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 4: Checkpoint**

Run: `dotnet build` again. It must be green. (No git.)

---

### Task 2: Data model, DbContext and options

**Files:**
- Create: `src/WeeklySalesCoach/Data/Entities.cs`, `src/WeeklySalesCoach/Data/AppDbContext.cs`, `src/WeeklySalesCoach/Configuration/AppOptions.cs`
- Create: `tests/WeeklySalesCoach.Tests/TestSupport/TestDb.cs`, `tests/WeeklySalesCoach.Tests/TestSupport/TestData.cs`
- Test: `tests/WeeklySalesCoach.Tests/AppDbContextTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces:
  - Enums `UserRole { Manager, Rep }`, `FeedbackStyle { Direct, Encouraging }`, `ReportStatus { NotGenerated, Draft, Approved }`, `ReportSource { Live, Demo }` (namespace `WeeklySalesCoach.Data`)
  - Entities `AppUser`, `Rep`, `Week`, `WeeklyMetrics`, `Deal`, `CoachingReport` (properties exactly as in the code below)
  - `AppDbContext` with `Users`, `Reps`, `Weeks`, `WeeklyMetrics`, `Deals`, `CoachingReports`
  - `DomainOptions { DealTerm, RepTerm, WonStage, LostStage }`, `GeminiOptions { ApiKey, Model, TimeoutSeconds, IsConfigured }`, `DemoOptions { EnableQuickLogin, DemoFeedbackPath }` (namespace `WeeklySalesCoach.Configuration`, each with `const string Section`)
  - Test helpers `TestDb` (`CreateContext()`, `Factory`) and `TestData.AddWeek/AddRep/AddMetrics/AddDeal`

- [ ] **Step 1: Write the test helpers and the failing test**

`tests/WeeklySalesCoach.Tests/TestSupport/TestDb.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WeeklySalesCoach.Data;

namespace WeeklySalesCoach.Tests.TestSupport;

/// <summary>An isolated in-memory SQLite database that lives as long as this object.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        using var db = CreateContext();
        db.Database.EnsureCreated();
        Factory = new Factory_(this);
    }

    public DbContextOptions<AppDbContext> Options { get; }
    public IDbContextFactory<AppDbContext> Factory { get; }

    public AppDbContext CreateContext() => new(Options);

    public void Dispose() => _connection.Dispose();

    private sealed class Factory_(TestDb db) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => db.CreateContext();
    }
}
```

`tests/WeeklySalesCoach.Tests/TestSupport/TestData.cs`:

```csharp
using WeeklySalesCoach.Data;

namespace WeeklySalesCoach.Tests.TestSupport;

public static class TestData
{
    public static Week AddWeek(AppDbContext db, DateOnly start, bool isCurrent)
    {
        var week = new Week { StartDate = start, EndDate = start.AddDays(4), IsCurrent = isCurrent };
        db.Weeks.Add(week);
        return week;
    }

    public static Rep AddRep(AppDbContext db, string name, decimal quota = 100_000m)
    {
        var rep = new Rep
        {
            Name = name,
            Title = "Loan Officer",
            TenureMonths = 12,
            WeeklyQuota = quota,
            PersonalGoal = "Close larger loans",
            FeedbackStyle = FeedbackStyle.Direct,
        };
        db.Reps.Add(rep);
        return rep;
    }

    public static WeeklyMetrics AddMetrics(AppDbContext db, Rep rep, Week week, int calls = 10,
        decimal wonVolume = 0m, int dealsWon = 0, int applications = 2, double responseHours = 4.0)
    {
        var metrics = new WeeklyMetrics
        {
            Rep = rep, Week = week,
            CallsMade = calls, CallsConnected = calls / 2,
            EmailsSent = 20, EmailsReplied = 4, NewLeads = 5,
            ApplicationsSubmitted = applications, DealsWon = dealsWon, WonVolume = wonVolume,
            DealsLost = 0, AvgLeadResponseHours = responseHours,
        };
        db.WeeklyMetrics.Add(metrics);
        return metrics;
    }

    public static Deal AddDeal(AppDbContext db, Rep rep, string client, string stage, decimal amount,
        DateOnly stageChangedOn, DateOnly lastContactOn, string notes = "")
    {
        var deal = new Deal
        {
            Rep = rep, ClientName = client, Product = "Mortgage", Amount = amount, Stage = stage,
            CreatedOn = stageChangedOn, StageChangedOn = stageChangedOn, LastContactOn = lastContactOn,
            Notes = notes,
        };
        db.Deals.Add(deal);
        return deal;
    }
}
```

`tests/WeeklySalesCoach.Tests/AppDbContextTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class AppDbContextTests
{
    [Fact]
    public void Round_trips_a_report_with_enum_values()
    {
        using var t = new TestDb();
        using (var db = t.CreateContext())
        {
            var week = TestData.AddWeek(db, new DateOnly(2026, 9, 14), isCurrent: true);
            var rep = TestData.AddRep(db, "Ana Torres");
            db.CoachingReports.Add(new CoachingReport
            {
                Rep = rep, Week = week, Status = ReportStatus.Draft, Source = ReportSource.Demo,
            });
            db.SaveChanges();
        }

        using var check = t.CreateContext();
        var report = check.CoachingReports.Single();
        Assert.Equal(ReportStatus.Draft, report.Status);
        Assert.Equal(ReportSource.Demo, report.Source);
    }

    [Fact]
    public void Allows_only_one_report_per_rep_and_week()
    {
        using var t = new TestDb();
        using var db = t.CreateContext();
        var week = TestData.AddWeek(db, new DateOnly(2026, 9, 14), isCurrent: true);
        var rep = TestData.AddRep(db, "Ana Torres");
        db.CoachingReports.Add(new CoachingReport { Rep = rep, Week = week });
        db.SaveChanges();

        db.CoachingReports.Add(new CoachingReport { Rep = rep, Week = week });
        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~AppDbContextTests"`
Expected: build FAILS with errors like `The type or namespace name 'Data' does not exist in the namespace 'WeeklySalesCoach'`.

- [ ] **Step 3: Implement entities, DbContext and options**

`src/WeeklySalesCoach/Data/Entities.cs`:

```csharp
namespace WeeklySalesCoach.Data;

public enum UserRole { Manager, Rep }
public enum FeedbackStyle { Direct, Encouraging }
public enum ReportStatus { NotGenerated, Draft, Approved }
public enum ReportSource { Live, Demo }

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
```

`src/WeeklySalesCoach/Data/AppDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace WeeklySalesCoach.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Rep> Reps => Set<Rep>();
    public DbSet<Week> Weeks => Set<Week>();
    public DbSet<WeeklyMetrics> WeeklyMetrics => Set<WeeklyMetrics>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<CoachingReport> CoachingReports => Set<CoachingReport>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>().HasIndex(u => u.Username).IsUnique();
        b.Entity<AppUser>().Property(u => u.Role).HasConversion<string>();
        b.Entity<Rep>().Property(r => r.FeedbackStyle).HasConversion<string>();
        b.Entity<WeeklyMetrics>().HasIndex(m => new { m.RepId, m.WeekId }).IsUnique();
        b.Entity<CoachingReport>().HasIndex(r => new { r.RepId, r.WeekId }).IsUnique();
        b.Entity<CoachingReport>().Property(r => r.Status).HasConversion<string>();
        b.Entity<CoachingReport>().Property(r => r.Source).HasConversion<string>();
    }
}
```

`src/WeeklySalesCoach/Configuration/AppOptions.cs`:

```csharp
namespace WeeklySalesCoach.Configuration;

/// <summary>Industry-specific wording. Changing industries means changing these values and the seed, not the code.</summary>
public class DomainOptions
{
    public const string Section = "Domain";
    public string DealTerm { get; set; } = "Deal";
    public string RepTerm { get; set; } = "Sales Rep";
    public string WonStage { get; set; } = "Won";
    public string LostStage { get; set; } = "Lost";
}

public class GeminiOptions
{
    public const string Section = "Gemini";
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gemini-2.5-flash";
    public int TimeoutSeconds { get; set; } = 30;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

public class DemoOptions
{
    public const string Section = "Demo";
    public bool EnableQuickLogin { get; set; } = true;
    public string DemoFeedbackPath { get; set; } = "Data/Seed/demo-feedback.json";
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~AppDbContextTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Checkpoint**

Run: `dotnet test`
Expected: all tests pass.

---

### Task 3: Demo seed data and database initialization

**Files:**
- Create: `src/WeeklySalesCoach/Data/SeedData.cs`, `src/WeeklySalesCoach/Data/DatabaseInitializer.cs`
- Test: `tests/WeeklySalesCoach.Tests/SeedDataTests.cs`

**Interfaces:**
- Consumes: entities and `AppDbContext` (Task 2)
- Produces:
  - `SeedData.DemoPassword` (`"demo123"`)
  - `Task SeedData.SeedAsync(AppDbContext db, IPasswordHasher<AppUser> hasher, CancellationToken ct = default)`, idempotent
  - `Task DatabaseInitializer.InitializeAsync(IServiceProvider services, CancellationToken ct = default)`
  - Usernames: `roberto` (Manager), `ana`, `carlos`, `sofia`, `miguel`, `laura` (Reps)
  - Rep names: `Ana Torres`, `Carlos Mendoza`, `Sofía Herrera`, `Miguel Ortega`, `Laura Bennett`

- [ ] **Step 1: Write the failing test**

`tests/WeeklySalesCoach.Tests/SeedDataTests.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class SeedDataTests
{
    private static async Task<TestDb> SeededAsync()
    {
        var t = new TestDb();
        await using var db = t.CreateContext();
        await SeedData.SeedAsync(db, new PasswordHasher<AppUser>());
        return t;
    }

    [Fact]
    public async Task Seeds_the_demo_team_exactly_once()
    {
        using var t = await SeededAsync();
        await using (var again = t.CreateContext())
        {
            await SeedData.SeedAsync(again, new PasswordHasher<AppUser>());
        }

        await using var db = t.CreateContext();
        Assert.Equal(5, await db.Reps.CountAsync());
        Assert.Equal(6, await db.Users.CountAsync());
        Assert.Equal(2, await db.Weeks.CountAsync());
        Assert.Equal(1, await db.Weeks.CountAsync(w => w.IsCurrent));
        Assert.Equal(10, await db.WeeklyMetrics.CountAsync());
        var miguelReport = await db.CoachingReports.Include(r => r.Rep).SingleAsync();
        Assert.Equal("Miguel Ortega", miguelReport.Rep!.Name);
        Assert.Equal("Out 2 days this week for compliance training.", miguelReport.ManagerNote);
    }

    [Fact]
    public async Task Current_week_metrics_match_the_deal_pipeline()
    {
        using var t = await SeededAsync();
        await using var db = t.CreateContext();
        var week = await db.Weeks.SingleAsync(w => w.IsCurrent);
        var metrics = await db.WeeklyMetrics.Where(m => m.WeekId == week.Id).ToListAsync();
        var deals = await db.Deals.ToListAsync();

        foreach (var m in metrics)
        {
            var closedThisWeek = deals
                .Where(d => d.RepId == m.RepId && d.StageChangedOn >= week.StartDate && d.StageChangedOn <= week.EndDate)
                .ToList();
            var funded = closedThisWeek.Where(d => d.Stage == "Funded").ToList();
            Assert.Equal(m.DealsWon, funded.Count);
            Assert.Equal(m.WonVolume, funded.Sum(d => d.Amount));
            Assert.Equal(m.DealsLost, closedThisWeek.Count(d => d.Stage == "Lost"));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~SeedDataTests"`
Expected: build FAILS: `The name 'SeedData' does not exist in the current context`.

- [ ] **Step 3: Implement the seed and the initializer**

`src/WeeklySalesCoach/Data/SeedData.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace WeeklySalesCoach.Data;

/// <summary>
/// Fictional loan-origination team. Each rep is designed to exercise a different coaching skill:
/// Ana (recognize a top performer), Carlos (bottleneck), Sofía (encourage a new hire),
/// Miguel (respect manager context), Laura (align with a personal goal).
/// </summary>
public static class SeedData
{
    public const string DemoPassword = "demo123";

    public static async Task SeedAsync(AppDbContext db, IPasswordHasher<AppUser> hasher, CancellationToken ct = default)
    {
        if (await db.Reps.AnyAsync(ct)) return;

        var previous = new Week { StartDate = new DateOnly(2026, 9, 7), EndDate = new DateOnly(2026, 9, 11), IsCurrent = false };
        var current = new Week { StartDate = new DateOnly(2026, 9, 14), EndDate = new DateOnly(2026, 9, 18), IsCurrent = true };
        db.Weeks.AddRange(previous, current);

        var ana = NewRep("Ana Torres", "Senior Loan Officer", 48, 250_000m, "Mentor newer officers while keeping volume above quota", FeedbackStyle.Direct);
        var carlos = NewRep("Carlos Mendoza", "Loan Officer", 12, 150_000m, "Improve application-to-funding conversion", FeedbackStyle.Direct);
        var sofia = NewRep("Sofía Herrera", "Junior Loan Officer", 2, 80_000m, "Close my first loans and build a steady pipeline", FeedbackStyle.Encouraging);
        var miguel = NewRep("Miguel Ortega", "Senior Loan Officer", 84, 200_000m, "Win more commercial (SBA) clients", FeedbackStyle.Direct);
        var laura = NewRep("Laura Bennett", "Loan Officer", 24, 150_000m, "Close larger loans", FeedbackStyle.Encouraging);
        db.Reps.AddRange(ana, carlos, sofia, miguel, laura);

        db.Users.Add(User("roberto", "Roberto Díaz", UserRole.Manager, null));
        db.Users.AddRange(
            User("ana", ana.Name, UserRole.Rep, ana),
            User("carlos", carlos.Name, UserRole.Rep, carlos),
            User("sofia", sofia.Name, UserRole.Rep, sofia),
            User("miguel", miguel.Name, UserRole.Rep, miguel),
            User("laura", laura.Name, UserRole.Rep, laura));

        //                    calls conn emails repl leads apps won volume   lost resp(h)
        db.WeeklyMetrics.AddRange(
            M(ana, previous,     40,  17,  48,  15,  8,   5,  2, 268_000m, 1, 8.8),
            M(ana, current,      38,  16,  45,  14,  9,   6,  3, 312_000m, 1, 9.5),
            M(carlos, previous,  65,  19,  85,   8, 11,   6,  1, 110_000m, 3, 3.4),
            M(carlos, current,   72,  21,  90,   9, 12,   7,  1,  95_000m, 2, 3.1),
            M(sofia, previous,   28,   7,  35,   5,  4,   1,  0,       0m, 1, 6.5),
            M(sofia, current,    40,  12,  50,  10,  6,   3,  1,  62_000m, 0, 4.0),
            M(miguel, previous,  45,  18,  55,  15,  8,   5,  3, 410_000m, 0, 4.2),
            M(miguel, current,   22,   9,  30,   8,  5,   2,  1, 140_000m, 1, 7.0),
            M(laura, previous,   33,  14,  58,  20,  5,   3,  2,  79_000m, 0, 2.2),
            M(laura, current,    35,  15,  60,  22,  5,   4,  2,  88_000m, 0, 2.0));

        // Day numbers are days of September 2026: created, stage changed, last contact.
        db.Deals.AddRange(
            D(ana, "Hartwell Family", "Mortgage", 210_000m, "Funded", 2, 16, 16, "Funded after rate-lock follow-ups on 09-12 and 09-15."),
            D(ana, "M. Castillo", "Refinance", 72_000m, "Funded", 3, 15, 15, "Smooth file; all documents on first request."),
            D(ana, "J. Rivera", "Auto Loan", 30_000m, "Funded", 9, 17, 17, "Same-week approval and funding."),
            D(ana, "Brightside Bakery LLC", "SBA Loan", 180_000m, "Underwriting", 1, 15, 17, "Underwriter requested 2025 tax returns; client sending Monday."),
            D(ana, "T. Nguyen", "Auto Loan", 28_000m, "Lead", 14, 14, 16, "Inbound web lead; first call two days after inquiry."),
            D(ana, "J. Okafor", "Personal Loan", 12_000m, "Lead", 10, 10, 10, "Inbound web lead. Not contacted yet."),
            D(ana, "Pinecrest Dental", "Refinance", 350_000m, "Lost", 1, 17, 17, "Negotiated for two weeks.", "Competitor offered a 0.25% lower rate."),

            D(carlos, "Delgado Family", "Mortgage", 95_000m, "Funded", 1, 17, 17, "Funded on schedule."),
            D(carlos, "Summit Auto Repair", "SBA Loan", 120_000m, "Documents Pending", 2, 8, 9, "Waiting on 3 months of bank statements."),
            D(carlos, "K. Patel", "Refinance", 240_000m, "Documents Pending", 4, 10, 10, "Requested pay stubs and W-2s."),
            D(carlos, "L. Moreno", "Personal Loan", 15_000m, "Documents Pending", 8, 11, 11, "Requested proof of income."),
            D(carlos, "B. Foster", "Mortgage", 310_000m, "Application", 15, 17, 17, "Application submitted; pre-approval pending."),
            D(carlos, "Greenway Landscaping", "SBA Loan", 60_000m, "Lost", 1, 16, 9, "Documents requested 09-09.", "Client went silent after the document request."),
            D(carlos, "R. Chen", "Auto Loan", 25_000m, "Lost", 7, 15, 14, "Compared offers.", "Chose dealer financing."),

            D(sofia, "A. Ramos", "Refinance", 62_000m, "Funded", 3, 18, 18, "First funded loan! Walked the client through closing documents."),
            D(sofia, "Lopez Family", "Mortgage", 280_000m, "Application", 11, 16, 17, "Application complete; ordering appraisal."),
            D(sofia, "Harbor Café", "SBA Loan", 90_000m, "Documents Pending", 8, 15, 17, "Followed up twice on missing documents."),
            D(sofia, "D. Park", "Personal Loan", 10_000m, "Lead", 17, 17, 17, "Inbound lead; intro call done."),

            D(miguel, "Whitaker Family", "Mortgage", 140_000m, "Funded", 1, 15, 15, "Funded; client very satisfied."),
            D(miguel, "Coastal Freight Co.", "SBA Loan", 350_000m, "Underwriting", 1, 11, 11, "Waiting on equipment appraisal."),
            D(miguel, "S. Ivanova", "Refinance", 190_000m, "Approved", 2, 12, 12, "Approved; closing date not yet scheduled."),
            D(miguel, "E. Walsh", "Mortgage", 260_000m, "Application", 8, 10, 10, "Application received; credit pull pending."),
            D(miguel, "P. Grant", "HELOC", 75_000m, "Lost", 3, 16, 16, "Client reconsidering.", "Client postponed the renovation."),

            D(laura, "N. Brooks", "Auto Loan", 33_000m, "Funded", 8, 15, 15, "Funded in 7 days."),
            D(laura, "M. Diaz", "HELOC", 55_000m, "Funded", 2, 17, 17, "Funded; client asked about a future refinance."),
            D(laura, "Oakridge Pharmacy", "SBA Loan", 400_000m, "Lead", 16, 16, 18, "Referral from an existing client. Intro call went well."),
            D(laura, "J. Harper", "Refinance", 45_000m, "Underwriting", 4, 14, 18, "All documents in; underwriting on track."),
            D(laura, "C. Evans", "Auto Loan", 22_000m, "Application", 15, 16, 18, "Application submitted."));

        db.CoachingReports.Add(new CoachingReport
        {
            Rep = miguel, Week = current, Status = ReportStatus.NotGenerated,
            ManagerNote = "Out 2 days this week for compliance training.",
        });

        await db.SaveChangesAsync(ct);

        AppUser User(string username, string displayName, UserRole role, Rep? rep)
        {
            var user = new AppUser { Username = username, DisplayName = displayName, Role = role, Rep = rep };
            user.PasswordHash = hasher.HashPassword(user, DemoPassword);
            return user;
        }
    }

    private static Rep NewRep(string name, string title, int tenureMonths, decimal quota, string goal, FeedbackStyle style) =>
        new() { Name = name, Title = title, TenureMonths = tenureMonths, WeeklyQuota = quota, PersonalGoal = goal, FeedbackStyle = style };

    private static WeeklyMetrics M(Rep rep, Week week, int calls, int connected, int emails, int replied,
        int leads, int applications, int won, decimal volume, int lost, double responseHours) =>
        new()
        {
            Rep = rep, Week = week, CallsMade = calls, CallsConnected = connected, EmailsSent = emails,
            EmailsReplied = replied, NewLeads = leads, ApplicationsSubmitted = applications, DealsWon = won,
            WonVolume = volume, DealsLost = lost, AvgLeadResponseHours = responseHours,
        };

    private static Deal D(Rep rep, string client, string product, decimal amount, string stage,
        int createdDay, int stageChangedDay, int lastContactDay, string notes, string? lostReason = null) =>
        new()
        {
            Rep = rep, ClientName = client, Product = product, Amount = amount, Stage = stage,
            CreatedOn = new DateOnly(2026, 9, createdDay),
            StageChangedOn = new DateOnly(2026, 9, stageChangedDay),
            LastContactOn = new DateOnly(2026, 9, lastContactDay),
            Notes = notes, LostReason = lostReason,
        };
}
```

`src/WeeklySalesCoach/Data/DatabaseInitializer.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace WeeklySalesCoach.Data;

public static class DatabaseInitializer
{
    /// <summary>Creates the SQLite schema (no migrations needed for the demo) and seeds it on first run.</summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
        await SeedData.SeedAsync(db, hasher, ct);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~SeedDataTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Checkpoint**

Run: `dotnet test`
Expected: all tests pass.

---

### Task 4: Coaching content model, parser and validator

**Files:**
- Create: `src/WeeklySalesCoach/Services/Coaching/CoachingContent.cs`, `src/WeeklySalesCoach/Services/Coaching/CoachingContentParser.cs`
- Create: `tests/WeeklySalesCoach.Tests/TestSupport/TestContent.cs` (the `ValidJson` part only; `SampleContext` is added in Task 5)
- Test: `tests/WeeklySalesCoach.Tests/CoachingContentParserTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces (namespace `WeeklySalesCoach.Services.Coaching`):
  - `class CoachingContent { string Summary; List<FeedbackItem> Strengths; List<FeedbackItem> Improvements; List<ActionItem> Actions; List<string> DataGaps }`
  - `class FeedbackItem { string Title; string Detail; string Evidence }`, `class ActionItem { string Action; string Why; string SuccessSignal }`
  - `class InvalidCoachingContentException(string message, Exception? inner = null) : Exception`
  - `static CoachingContentParser.Parse(string raw) : CoachingContent` (strips code fences, validates, throws `InvalidCoachingContentException`)
  - `static CoachingContentParser.Validate(CoachingContent c)`, `Serialize(CoachingContent c) : string`, `FromStored(string json) : CoachingContent`
  - Test constant `TestContent.ValidJson` (summary `"Solid week."`)

- [ ] **Step 1: Write the test content and the failing tests**

`tests/WeeklySalesCoach.Tests/TestSupport/TestContent.cs`:

```csharp
namespace WeeklySalesCoach.Tests.TestSupport;

public static partial class TestContent
{
    public const string ValidJson = """
        {
          "summary": "Solid week.",
          "strengths": [
            { "title": "Closed Delgado", "detail": "Funded on schedule.", "evidence": "Delgado Family mortgage, $95,000, funded 09-17." }
          ],
          "improvements": [
            { "title": "Document follow-up", "detail": "Three files stalled.", "evidence": "Summit Auto Repair: 10 days in Documents Pending." }
          ],
          "actions": [
            { "action": "Call every Documents Pending client by Tuesday", "why": "Files are stalling.", "successSignal": "No file older than 5 days in Documents Pending." }
          ],
          "dataGaps": []
        }
        """;
}
```

`tests/WeeklySalesCoach.Tests/CoachingContentParserTests.cs`:

```csharp
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
    public void Rejects_more_than_three_strengths()
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
    public void Serialize_and_FromStored_round_trip()
    {
        var content = CoachingContentParser.Parse(TestContent.ValidJson);

        var restored = CoachingContentParser.FromStored(CoachingContentParser.Serialize(content));

        Assert.Equal(CoachingContentParser.Serialize(content), CoachingContentParser.Serialize(restored));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~CoachingContentParserTests"`
Expected: build FAILS: `The type or namespace name 'Coaching' does not exist`.

- [ ] **Step 3: Implement the model and the parser**

`src/WeeklySalesCoach/Services/Coaching/CoachingContent.cs`:

```csharp
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
```

`src/WeeklySalesCoach/Services/Coaching/CoachingContentParser.cs`:

```csharp
using System.Text.Json;

namespace WeeklySalesCoach.Services.Coaching;

public static class CoachingContentParser
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Parses raw AI output and enforces the coaching rules. Throws <see cref="InvalidCoachingContentException"/>.</summary>
    public static CoachingContent Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidCoachingContentException("The AI response was empty.");

        CoachingContent? content;
        try
        {
            content = JsonSerializer.Deserialize<CoachingContent>(StripCodeFences(raw), Json);
        }
        catch (JsonException ex)
        {
            throw new InvalidCoachingContentException("The AI response was not valid JSON.", ex);
        }

        if (content is null)
            throw new InvalidCoachingContentException("The AI response was empty.");

        Normalize(content);
        Validate(content);
        return content;
    }

    public static void Validate(CoachingContent content)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(content.Summary)) errors.Add("A summary is required.");
        CheckCount("strengths", content.Strengths?.Count ?? 0, errors);
        CheckCount("improvements", content.Improvements?.Count ?? 0, errors);
        CheckCount("actions", content.Actions?.Count ?? 0, errors);

        foreach (var item in (content.Strengths ?? []).Concat(content.Improvements ?? []))
        {
            if (string.IsNullOrWhiteSpace(item.Title)) errors.Add("Every strength and improvement needs a title.");
            if (string.IsNullOrWhiteSpace(item.Evidence)) errors.Add("Every strength and improvement must cite evidence.");
        }

        foreach (var action in content.Actions ?? [])
        {
            if (string.IsNullOrWhiteSpace(action.Action)) errors.Add("Every action needs a description.");
        }

        if (errors.Count > 0)
            throw new InvalidCoachingContentException(string.Join(" ", errors.Distinct()));
    }

    public static string Serialize(CoachingContent content) => JsonSerializer.Serialize(content, Json);

    /// <summary>Reads content that was already validated when it was saved.</summary>
    public static CoachingContent FromStored(string json) =>
        Normalize(JsonSerializer.Deserialize<CoachingContent>(json, Json) ?? new CoachingContent());

    private static void CheckCount(string name, int count, List<string> errors)
    {
        if (count is < 1 or > 3) errors.Add($"Expected 1-3 {name}, got {count}.");
    }

    private static CoachingContent Normalize(CoachingContent c)
    {
        c.Summary ??= "";
        c.Strengths ??= [];
        c.Improvements ??= [];
        c.Actions ??= [];
        c.DataGaps ??= [];
        return c;
    }

    private static string StripCodeFences(string raw)
    {
        var text = raw.Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal)) return text;

        var firstNewline = text.IndexOf('\n');
        var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
        return firstNewline >= 0 && lastFence > firstNewline
            ? text[(firstNewline + 1)..lastFence].Trim()
            : text;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~CoachingContentParserTests"`
Expected: PASS (10 tests).

- [ ] **Step 5: Checkpoint**

Run: `dotnet test`
Expected: all tests pass.

---

### Task 5: Coaching context builder and prompt

**Files:**
- Create: `src/WeeklySalesCoach/Services/Coaching/CoachingContext.cs`, `src/WeeklySalesCoach/Services/Coaching/Calc.cs`, `src/WeeklySalesCoach/Services/Coaching/CoachingContextBuilder.cs`, `src/WeeklySalesCoach/Services/AI/CoachingPrompt.cs`
- Modify: `tests/WeeklySalesCoach.Tests/TestSupport/TestContent.cs` (add `SampleContext`)
- Test: `tests/WeeklySalesCoach.Tests/CoachingContextBuilderTests.cs`, `tests/WeeklySalesCoach.Tests/CoachingPromptTests.cs`

**Interfaces:**
- Consumes: `AppDbContext`, entities, `DomainOptions` (Task 2)
- Produces:
  - Records (namespace `WeeklySalesCoach.Services.Coaching`):
    - `RepProfile(string Name, string Title, int TenureMonths, decimal WeeklyQuota, string PersonalGoal, FeedbackStyle FeedbackStyle)`
    - `MetricsSnapshot(double CallsMade, double CallsConnected, double EmailsSent, double EmailsReplied, double NewLeads, double ApplicationsSubmitted, double DealsWon, decimal WonVolume, double DealsLost, double AvgLeadResponseHours)`
    - `DealSnapshot(string Client, string Product, decimal Amount, string Stage, int DaysInStage, int DaysSinceLastContact, string Notes, string? LostReason)`
    - `CoachingContext(int RepId, int WeekId, DateOnly WeekStart, DateOnly WeekEnd, string DealTerm, string RepTerm, RepProfile Rep, MetricsSnapshot Current, MetricsSnapshot? Previous, MetricsSnapshot TeamAverage, double QuotaAttainmentPercent, IReadOnlyList<DealSnapshot> Deals, string? ManagerNote)`
  - `static Calc.Attainment(decimal won, decimal quota) : double`, `static Calc.DaysBetween(DateOnly from, DateOnly to) : int`
  - `CoachingContextBuilder(IDbContextFactory<AppDbContext>, IOptions<DomainOptions>)` with `Task<CoachingContext> BuildAsync(int repId, int weekId, CancellationToken ct = default)`. Throws `InvalidOperationException` if the rep, week or metrics are missing.
  - `CoachingPrompt.SystemInstructions : string`, `CoachingPrompt.BuildUserMessage(CoachingContext) : string`, `CoachingPrompt.ResponseSchema : object` (namespace `WeeklySalesCoach.Services.AI`)
  - Test helper `TestContent.SampleContext(string name = "Ana Torres") : CoachingContext`

- [ ] **Step 1: Write the failing tests**

Append to `tests/WeeklySalesCoach.Tests/TestSupport/TestContent.cs` (second part of the partial class, in the same file below the first):

```csharp
public static partial class TestContent
{
    public static WeeklySalesCoach.Services.Coaching.CoachingContext SampleContext(string name = "Ana Torres") => new(
        RepId: 1, WeekId: 1,
        WeekStart: new DateOnly(2026, 9, 14), WeekEnd: new DateOnly(2026, 9, 18),
        DealTerm: "Loan", RepTerm: "Loan Officer",
        Rep: new WeeklySalesCoach.Services.Coaching.RepProfile(name, "Loan Officer", 12, 100_000m, "Close larger loans", WeeklySalesCoach.Data.FeedbackStyle.Direct),
        Current: Snapshot(40), Previous: Snapshot(30), TeamAverage: Snapshot(35),
        QuotaAttainmentPercent: 80,
        Deals: [new WeeklySalesCoach.Services.Coaching.DealSnapshot("Hartwell Family", "Mortgage", 210_000m, "Funded", 2, 2, "Funded after follow-up.", null)],
        ManagerNote: null);

    private static WeeklySalesCoach.Services.Coaching.MetricsSnapshot Snapshot(double calls) =>
        new(calls, calls / 2, 50, 10, 5, 3, 1, 50_000m, 0, 4);
}
```

`tests/WeeklySalesCoach.Tests/CoachingContextBuilderTests.cs`:

```csharp
using Microsoft.Extensions.Options;
using WeeklySalesCoach.Configuration;
using WeeklySalesCoach.Services.Coaching;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class CoachingContextBuilderTests
{
    private static readonly DateOnly PreviousStart = new(2026, 9, 7);
    private static readonly DateOnly CurrentStart = new(2026, 9, 14);

    private static CoachingContextBuilder Builder(TestDb t) => new(t.Factory, Options.Create(new DomainOptions
    {
        DealTerm = "Loan", RepTerm = "Loan Officer", WonStage = "Funded", LostStage = "Lost",
    }));

    [Fact]
    public async Task Includes_current_previous_and_team_average()
    {
        using var t = new TestDb();
        int anaId, weekId;
        using (var db = t.CreateContext())
        {
            var prev = TestData.AddWeek(db, PreviousStart, isCurrent: false);
            var cur = TestData.AddWeek(db, CurrentStart, isCurrent: true);
            var ana = TestData.AddRep(db, "Ana Torres", quota: 200_000m);
            var carlos = TestData.AddRep(db, "Carlos Mendoza");
            TestData.AddMetrics(db, ana, prev, calls: 20);
            TestData.AddMetrics(db, ana, cur, calls: 40, wonVolume: 150_000m);
            TestData.AddMetrics(db, carlos, cur, calls: 60);
            db.SaveChanges();
            anaId = ana.Id; weekId = cur.Id;
        }

        var ctx = await Builder(t).BuildAsync(anaId, weekId);

        Assert.Equal("Ana Torres", ctx.Rep.Name);
        Assert.Equal(40, ctx.Current.CallsMade);
        Assert.Equal(20, ctx.Previous!.CallsMade);
        Assert.Equal(50, ctx.TeamAverage.CallsMade);
        Assert.Equal(75.0, ctx.QuotaAttainmentPercent);
        Assert.Equal("Loan", ctx.DealTerm);
        Assert.Equal(new DateOnly(2026, 9, 18), ctx.WeekEnd);
    }

    [Fact]
    public async Task Previous_is_null_when_there_is_no_previous_week()
    {
        using var t = new TestDb();
        int repId, weekId;
        using (var db = t.CreateContext())
        {
            var cur = TestData.AddWeek(db, CurrentStart, isCurrent: true);
            var rep = TestData.AddRep(db, "Sofía Herrera");
            TestData.AddMetrics(db, rep, cur);
            db.SaveChanges();
            repId = rep.Id; weekId = cur.Id;
        }

        var ctx = await Builder(t).BuildAsync(repId, weekId);

        Assert.Null(ctx.Previous);
    }

    [Fact]
    public async Task Quota_attainment_is_zero_when_quota_is_zero()
    {
        using var t = new TestDb();
        int repId, weekId;
        using (var db = t.CreateContext())
        {
            var cur = TestData.AddWeek(db, CurrentStart, isCurrent: true);
            var rep = TestData.AddRep(db, "Laura Bennett", quota: 0m);
            TestData.AddMetrics(db, rep, cur, wonVolume: 50_000m);
            db.SaveChanges();
            repId = rep.Id; weekId = cur.Id;
        }

        var ctx = await Builder(t).BuildAsync(repId, weekId);

        Assert.Equal(0, ctx.QuotaAttainmentPercent);
    }

    [Fact]
    public async Task Deals_show_days_relative_to_week_end_with_open_deals_first()
    {
        using var t = new TestDb();
        int repId, weekId;
        using (var db = t.CreateContext())
        {
            var cur = TestData.AddWeek(db, CurrentStart, isCurrent: true);
            var rep = TestData.AddRep(db, "Carlos Mendoza");
            TestData.AddMetrics(db, rep, cur);
            TestData.AddDeal(db, rep, "Big Funded", "Funded", 500_000m, new DateOnly(2026, 9, 17), new DateOnly(2026, 9, 17));
            TestData.AddDeal(db, rep, "Summit Auto Repair", "Documents Pending", 120_000m, new DateOnly(2026, 9, 8), new DateOnly(2026, 9, 9));
            db.SaveChanges();
            repId = rep.Id; weekId = cur.Id;
        }

        var ctx = await Builder(t).BuildAsync(repId, weekId);

        Assert.Equal("Summit Auto Repair", ctx.Deals[0].Client);
        Assert.Equal(10, ctx.Deals[0].DaysInStage);
        Assert.Equal(9, ctx.Deals[0].DaysSinceLastContact);
        Assert.Equal("Big Funded", ctx.Deals[1].Client);
    }

    [Fact]
    public async Task Includes_trimmed_manager_note()
    {
        using var t = new TestDb();
        int repId, weekId;
        using (var db = t.CreateContext())
        {
            var cur = TestData.AddWeek(db, CurrentStart, isCurrent: true);
            var rep = TestData.AddRep(db, "Miguel Ortega");
            TestData.AddMetrics(db, rep, cur);
            db.CoachingReports.Add(new WeeklySalesCoach.Data.CoachingReport { Rep = rep, Week = cur, ManagerNote = "  Out 2 days for training.  " });
            db.SaveChanges();
            repId = rep.Id; weekId = cur.Id;
        }

        var ctx = await Builder(t).BuildAsync(repId, weekId);

        Assert.Equal("Out 2 days for training.", ctx.ManagerNote);
    }

    [Fact]
    public async Task Throws_for_unknown_rep()
    {
        using var t = new TestDb();
        int weekId;
        using (var db = t.CreateContext())
        {
            var week = TestData.AddWeek(db, CurrentStart, isCurrent: true);
            db.SaveChanges();
            weekId = week.Id;
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => Builder(t).BuildAsync(999, weekId));
    }
}
```

`tests/WeeklySalesCoach.Tests/CoachingPromptTests.cs`:

```csharp
using Microsoft.Extensions.Options;
using WeeklySalesCoach.Configuration;
using WeeklySalesCoach.Services.AI;
using WeeklySalesCoach.Services.Coaching;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class CoachingPromptTests
{
    [Fact]
    public void System_instructions_contain_the_key_guardrails()
    {
        var s = CoachingPrompt.SystemInstructions;

        Assert.Contains("evidence", s);
        Assert.Contains("HR actions", s);
        Assert.Contains("by name", s);
        Assert.Contains("dataGaps", s);
        Assert.Contains("manager note", s);
    }

    [Fact]
    public void User_message_contains_this_reps_data_and_manager_note()
    {
        var ctx = TestContent.SampleContext() with { ManagerNote = "Out 2 days for training." };

        var message = CoachingPrompt.BuildUserMessage(ctx);

        Assert.Contains("Ana Torres", message);
        Assert.Contains("Hartwell Family", message);
        Assert.Contains("Out 2 days for training.", message);
        Assert.Contains("\"teamAverage\"", message);
    }

    [Fact]
    public async Task User_message_never_contains_other_reps_names()
    {
        using var t = new TestDb();
        int anaId, weekId;
        using (var db = t.CreateContext())
        {
            var cur = TestData.AddWeek(db, new DateOnly(2026, 9, 14), isCurrent: true);
            var ana = TestData.AddRep(db, "Ana Torres");
            var carlos = TestData.AddRep(db, "Carlos Mendoza");
            TestData.AddMetrics(db, ana, cur);
            TestData.AddMetrics(db, carlos, cur);
            TestData.AddDeal(db, carlos, "Carlos Client", "Lead", 10_000m, new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 15));
            db.SaveChanges();
            anaId = ana.Id; weekId = cur.Id;
        }
        var builder = new CoachingContextBuilder(t.Factory, Options.Create(new DomainOptions()));

        var message = CoachingPrompt.BuildUserMessage(await builder.BuildAsync(anaId, weekId));

        Assert.Contains("Ana Torres", message);
        Assert.DoesNotContain("Carlos", message);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~CoachingContextBuilderTests|FullyQualifiedName~CoachingPromptTests"`
Expected: build FAILS: `The type or namespace name 'CoachingContext' could not be found`.

- [ ] **Step 3: Implement the context records, `Calc`, the builder and the prompt**

`src/WeeklySalesCoach/Services/Coaching/CoachingContext.cs`:

```csharp
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
```

`src/WeeklySalesCoach/Services/Coaching/Calc.cs`:

```csharp
namespace WeeklySalesCoach.Services.Coaching;

public static class Calc
{
    /// <summary>Won volume as a percentage of quota, rounded to 1 decimal. 0 when there is no quota.</summary>
    public static double Attainment(decimal won, decimal quota) =>
        quota <= 0 ? 0 : Math.Round((double)(won / quota) * 100, 1);

    public static int DaysBetween(DateOnly from, DateOnly to) => Math.Max(0, to.DayNumber - from.DayNumber);
}
```

`src/WeeklySalesCoach/Services/Coaching/CoachingContextBuilder.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WeeklySalesCoach.Configuration;
using WeeklySalesCoach.Data;

namespace WeeklySalesCoach.Services.Coaching;

public class CoachingContextBuilder(IDbContextFactory<AppDbContext> dbFactory, IOptions<DomainOptions> domain)
{
    public async Task<CoachingContext> BuildAsync(int repId, int weekId, CancellationToken ct = default)
    {
        var terms = domain.Value;
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var week = await db.Weeks.AsNoTracking().SingleAsync(w => w.Id == weekId, ct);
        var rep = await db.Reps.AsNoTracking().SingleAsync(r => r.Id == repId, ct);
        var weekMetrics = await db.WeeklyMetrics.AsNoTracking().Where(m => m.WeekId == weekId).ToListAsync(ct);
        var current = weekMetrics.SingleOrDefault(m => m.RepId == repId)
            ?? throw new InvalidOperationException($"No metrics for rep {repId} in week {weekId}.");

        var previousWeek = await db.Weeks.AsNoTracking()
            .Where(w => w.EndDate < week.StartDate)
            .OrderByDescending(w => w.EndDate)
            .FirstOrDefaultAsync(ct);
        var previous = previousWeek is null
            ? null
            : await db.WeeklyMetrics.AsNoTracking().SingleOrDefaultAsync(m => m.RepId == repId && m.WeekId == previousWeek.Id, ct);

        var deals = await db.Deals.AsNoTracking().Where(d => d.RepId == repId).ToListAsync(ct);
        var note = await db.CoachingReports.AsNoTracking()
            .Where(r => r.RepId == repId && r.WeekId == weekId)
            .Select(r => r.ManagerNote)
            .SingleOrDefaultAsync(ct);

        return new CoachingContext(
            RepId: rep.Id,
            WeekId: week.Id,
            WeekStart: week.StartDate,
            WeekEnd: week.EndDate,
            DealTerm: terms.DealTerm,
            RepTerm: terms.RepTerm,
            Rep: new RepProfile(rep.Name, rep.Title, rep.TenureMonths, rep.WeeklyQuota, rep.PersonalGoal, rep.FeedbackStyle),
            Current: ToSnapshot(current),
            Previous: previous is null ? null : ToSnapshot(previous),
            TeamAverage: Average(weekMetrics),
            QuotaAttainmentPercent: Calc.Attainment(current.WonVolume, rep.WeeklyQuota),
            Deals: deals
                .OrderBy(d => d.Stage == terms.WonStage || d.Stage == terms.LostStage ? 1 : 0)
                .ThenByDescending(d => d.Amount)
                .Select(d => new DealSnapshot(
                    d.ClientName, d.Product, d.Amount, d.Stage,
                    Calc.DaysBetween(d.StageChangedOn, week.EndDate),
                    Calc.DaysBetween(d.LastContactOn, week.EndDate),
                    d.Notes, d.LostReason))
                .ToList(),
            ManagerNote: string.IsNullOrWhiteSpace(note) ? null : note.Trim());
    }

    private static MetricsSnapshot ToSnapshot(WeeklyMetrics m) => new(
        m.CallsMade, m.CallsConnected, m.EmailsSent, m.EmailsReplied, m.NewLeads,
        m.ApplicationsSubmitted, m.DealsWon, m.WonVolume, m.DealsLost, m.AvgLeadResponseHours);

    /// <summary>Team average, aggregate only: no individual names or rows leave this method.</summary>
    private static MetricsSnapshot Average(IReadOnlyCollection<WeeklyMetrics> all) => new(
        R(all.Average(m => m.CallsMade)),
        R(all.Average(m => m.CallsConnected)),
        R(all.Average(m => m.EmailsSent)),
        R(all.Average(m => m.EmailsReplied)),
        R(all.Average(m => m.NewLeads)),
        R(all.Average(m => m.ApplicationsSubmitted)),
        R(all.Average(m => m.DealsWon)),
        Math.Round(all.Average(m => m.WonVolume), 0),
        R(all.Average(m => m.DealsLost)),
        R(all.Average(m => m.AvgLeadResponseHours)));

    private static double R(double value) => Math.Round(value, 1);
}
```

`src/WeeklySalesCoach/Services/AI/CoachingPrompt.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using WeeklySalesCoach.Services.Coaching;

namespace WeeklySalesCoach.Services.AI;

public static class CoachingPrompt
{
    public const string SystemInstructions = """
        You are an experienced, fair and constructive sales coach. You draft weekly coaching feedback
        for one team member. A manager will review and edit your draft before the team member sees it.

        You MUST:
        1. Base every statement only on the data provided. Do not use outside knowledge about the person.
        2. Cite specific evidence (a named deal, a number or a date) in the "evidence" field of every strength and improvement.
        3. Return 1-3 strengths, 1-3 improvements and 1-3 concrete actions for next week.
        4. Always include at least one genuine strength, even in a weak week.
        5. Adapt your tone to the person's tenure and preferred feedback style ("Direct" = concise and straightforward; "Encouraging" = warm and motivating).
        6. Describe behaviors and results, never personality ("did not follow up", not "is disorganized").
        7. Treat the manager note as important context (for example, time out of the office) and do not penalize the person for it.
        8. Address the person directly as "you", in English.

        You MUST NOT:
        1. Invent data, or assume causes that are not stated in the data.
        2. Comment on personal matters such as health, private life, age, gender, family or religion.
        3. Recommend HR actions such as termination, discipline, compensation or promotion.
        4. Mention or compare against other team members by name. Comparing against the team average is allowed.
        5. Use humiliating, sarcastic or threatening language.
        6. Guess when data is missing. List what you could not assess in "dataGaps" instead.
        """;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string BuildUserMessage(CoachingContext c)
    {
        var payload = new
        {
            week = new { start = c.WeekStart, end = c.WeekEnd },
            terminology = new { deal = c.DealTerm, rep = c.RepTerm },
            person = c.Rep,
            quotaAttainmentPercent = c.QuotaAttainmentPercent,
            metrics = new { thisWeek = c.Current, lastWeek = c.Previous, teamAverage = c.TeamAverage },
            pipeline = c.Deals,
            managerNote = c.ManagerNote,
        };

        return $"""
            Draft this week's coaching feedback for the {c.RepTerm.ToLowerInvariant()} described below.
            Day counts are measured up to the last day of the week. "lastWeek" is null when there is no data for the previous week.

            DATA:
            {JsonSerializer.Serialize(payload, Json)}
            """;
    }

    /// <summary>Gemini response schema (OpenAPI subset) that forces the structured output.</summary>
    public static readonly object ResponseSchema = new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["summary"] = new { type = "STRING" },
            ["strengths"] = FeedbackItems(),
            ["improvements"] = FeedbackItems(),
            ["actions"] = new
            {
                type = "ARRAY",
                minItems = 1,
                maxItems = 3,
                items = new
                {
                    type = "OBJECT",
                    properties = new Dictionary<string, object>
                    {
                        ["action"] = new { type = "STRING" },
                        ["why"] = new { type = "STRING" },
                        ["successSignal"] = new { type = "STRING" },
                    },
                    required = new[] { "action", "why", "successSignal" },
                },
            },
            ["dataGaps"] = new { type = "ARRAY", items = new { type = "STRING" } },
        },
        required = new[] { "summary", "strengths", "improvements", "actions", "dataGaps" },
    };

    private static object FeedbackItems() => new
    {
        type = "ARRAY",
        minItems = 1,
        maxItems = 3,
        items = new
        {
            type = "OBJECT",
            properties = new Dictionary<string, object>
            {
                ["title"] = new { type = "STRING" },
                ["detail"] = new { type = "STRING" },
                ["evidence"] = new { type = "STRING" },
            },
            required = new[] { "title", "detail", "evidence" },
        },
    };
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~CoachingContextBuilderTests|FullyQualifiedName~CoachingPromptTests"`
Expected: PASS (9 tests).

- [ ] **Step 5: Checkpoint**

Run: `dotnet test`
Expected: all tests pass.

---

### Task 6: Feedback generators (interface, Gemini, demo) and error messages

**Files:**
- Create: `src/WeeklySalesCoach/Services/AI/IFeedbackGenerator.cs`, `GeminiFeedbackGenerator.cs`, `DemoFeedbackGenerator.cs`, `AiErrorMessages.cs` (all in `src/WeeklySalesCoach/Services/AI/`)
- Create: `src/WeeklySalesCoach/Data/Seed/demo-feedback.json` (hand-written first version; regenerated with live AI in Task 14)
- Modify: `tests/WeeklySalesCoach.Tests/WeeklySalesCoach.Tests.csproj` (link `demo-feedback.json`)
- Test: `tests/WeeklySalesCoach.Tests/GeminiFeedbackGeneratorTests.cs`, `DemoFeedbackGeneratorTests.cs`, `AiErrorMessagesTests.cs`

**Interfaces:**
- Consumes: `CoachingContext` (Task 5), `CoachingPrompt` (Task 5), `InvalidCoachingContentException` and `CoachingContentParser` (Task 4), `GeminiOptions` (Task 2), `SeedData` (Task 3, in tests)
- Produces (namespace `WeeklySalesCoach.Services.AI`):
  - `interface IFeedbackGenerator { bool IsLive { get; } string ModelName { get; } Task<string> GenerateAsync(CoachingContext context, CancellationToken ct = default); }`: returns the raw JSON text
  - `enum AiFailureKind { Timeout, RateLimited, Unavailable, NotConfigured }`
  - `class AiUnavailableException(AiFailureKind kind, string message, Exception? inner = null) : Exception` with `Kind`
  - `GeminiFeedbackGenerator(HttpClient http, IOptions<GeminiOptions> options, ILogger<GeminiFeedbackGenerator> logger)`
  - `DemoFeedbackGenerator(string filePath)`: keyed by `Rep.Name`
  - `static AiErrorMessages.For(Exception ex) : string`

- [ ] **Step 1: Write the demo feedback file**

`src/WeeklySalesCoach/Data/Seed/demo-feedback.json`:

```json
{
  "Ana Torres": {
    "summary": "A strong week: you funded $312K against a $250K quota, and your rate-lock follow-ups paid off. The one gap is response time on smaller inbound leads.",
    "strengths": [
      { "title": "Beat quota by 25%", "detail": "Three loans funded this week, up from two last week.", "evidence": "3 loans funded for $312,000 vs. a $250,000 quota (last week: 2 for $268,000)." },
      { "title": "Persistent follow-up closed the Hartwell mortgage", "detail": "Your two rate-lock follow-ups kept the file moving to funding.", "evidence": "Hartwell Family mortgage, $210,000, funded 09-16 after follow-ups on 09-12 and 09-15." }
    ],
    "improvements": [
      { "title": "Small leads wait too long", "detail": "Your average lead response time is well above the team average, and one small lead has not been contacted at all.", "evidence": "J. Okafor personal loan ($12,000): no contact in 8 days. Avg. response 9.5h vs. team avg 5.1h." }
    ],
    "actions": [
      { "action": "Contact J. Okafor by Monday noon", "why": "An 8-day-old lead is at risk of going to a competitor.", "successSignal": "Okafor moves to Application or is closed out with a reason." },
      { "action": "Block two 30-minute slots a day for new inbound leads", "why": "It brings your response time closer to the team average.", "successSignal": "Average lead response under 6 hours next week." }
    ],
    "dataGaps": []
  },
  "Carlos Mendoza": {
    "summary": "You put in the most outreach effort on the team, but applications are stalling at the documents stage, and that is holding back your funded volume.",
    "strengths": [
      { "title": "Highest outreach volume", "detail": "Your activity keeps the top of your pipeline full.", "evidence": "72 calls and 90 emails this week (team avg: 41.4 calls, 55 emails)." },
      { "title": "Fast first response", "detail": "New leads hear from you quickly.", "evidence": "Avg. lead response 3.1h vs. team avg 5.1h." }
    ],
    "improvements": [
      { "title": "Files stall in Documents Pending", "detail": "Three applications have been waiting on documents for a week or more with no recent contact.", "evidence": "Summit Auto Repair (10 days), K. Patel (8 days) and L. Moreno (7 days) in Documents Pending; last contact between 09-09 and 09-11." },
      { "title": "A deal lost after the document request", "detail": "The client stopped responding after documents were requested and there was no follow-up.", "evidence": "Greenway Landscaping ($60,000) lost 09-16: client went silent after the 09-09 document request." }
    ],
    "actions": [
      { "action": "Call Summit Auto Repair, K. Patel and L. Moreno by Tuesday with a checklist of exactly what is missing", "why": "A specific checklist makes it easy for clients to finish their files.", "successSignal": "At least 2 of the 3 files move to Underwriting next week." },
      { "action": "Move 20% of cold-call time to follow-ups on open applications", "why": "Your conversion is limited by files in progress, not by new leads.", "successSignal": "No file sits more than 5 days in Documents Pending without contact." }
    ],
    "dataGaps": []
  },
  "Sofía Herrera": {
    "summary": "What a week, Sofía: you funded your first loan and improved almost every activity number compared to last week. Keep this momentum going!",
    "strengths": [
      { "title": "Your first funded loan", "detail": "Walking the client through closing documents made a real difference.", "evidence": "A. Ramos refinance, $62,000, funded 09-18." },
      { "title": "Big jump in activity", "detail": "You are building strong habits early.", "evidence": "Calls up from 28 to 40, applications from 1 to 3, response time down from 6.5h to 4.0h." }
    ],
    "improvements": [
      { "title": "Build the pipeline behind your first win", "detail": "More early-stage leads will make your results steadier week to week.", "evidence": "6 new leads this week vs. team avg 7.4; D. Park is your only open lead." }
    ],
    "actions": [
      { "action": "Keep following up on Harbor Café's missing documents twice a week", "why": "Your follow-ups are already working; consistency gets the file to underwriting.", "successSignal": "Harbor Café moves to Underwriting." },
      { "action": "Ask A. Ramos for one referral", "why": "Happy first clients are the easiest source of new leads.", "successSignal": "At least one referral lead added next week." }
    ],
    "dataGaps": []
  },
  "Miguel Ortega": {
    "summary": "A shorter week because of your 2-day compliance training, and the numbers reflect that. You still funded the Whitaker mortgage; the priority now is moving your large files forward.",
    "strengths": [
      { "title": "Funded the Whitaker mortgage", "detail": "A clean close with a satisfied client.", "evidence": "Whitaker Family mortgage, $140,000, funded 09-15." },
      { "title": "Strong connect rate", "detail": "Your calls reach decision makers more often than average.", "evidence": "9 of 22 calls connected (41%) vs. team avg 14.6 of 41.4 (35%)." }
    ],
    "improvements": [
      { "title": "Large files waiting without contact", "detail": "Your two biggest open files have had no contact for almost a week.", "evidence": "Coastal Freight Co. SBA loan ($350,000) in Underwriting, last contact 09-11; S. Ivanova refinance ($190,000) Approved, last contact 09-12." }
    ],
    "actions": [
      { "action": "Schedule the S. Ivanova closing this week", "why": "An approved loan without a closing date can slip.", "successSignal": "Closing date confirmed with the client." },
      { "action": "Get an appraisal ETA for Coastal Freight Co. on Monday", "why": "It is your largest open file and it is blocked on the appraisal.", "successSignal": "Appraisal date confirmed and shared with the client." }
    ],
    "dataGaps": ["This week's activity is not comparable to last week's full week because of 2 days out for compliance training."]
  },
  "Laura Bennett": {
    "summary": "Another steady week with the best follow-up discipline on the team, plus a referral that could be the larger loan you've been aiming for.",
    "strengths": [
      { "title": "Fastest lead response on the team", "detail": "Every open file was touched before the week ended.", "evidence": "Avg. response 2.0h vs. team avg 5.1h; every open file contacted on 09-18." },
      { "title": "Highest email reply rate", "detail": "Clients engage with your messages.", "evidence": "22 replies from 60 emails (37%) vs. team avg 12.6 of 55 (23%)." }
    ],
    "improvements": [
      { "title": "Average loan size is below your goal", "detail": "Your funded loans are small relative to your goal of closing larger loans.", "evidence": "Funded N. Brooks ($33,000) and M. Diaz ($55,000): average $44,000; 58.7% of quota." }
    ],
    "actions": [
      { "action": "Prepare a full SBA proposal for Oakridge Pharmacy ($400,000) this week", "why": "It's a referral well above your usual size, which is exactly your goal.", "successSignal": "Oakridge moves from Lead to Application." },
      { "action": "Ask your two funded clients for referrals to business owners", "why": "Referrals brought you your largest lead so far.", "successSignal": "One new commercial lead next week." }
    ],
    "dataGaps": []
  }
}
```

In `tests/WeeklySalesCoach.Tests/WeeklySalesCoach.Tests.csproj`, add:

```xml
  <ItemGroup>
    <None Include="..\..\src\WeeklySalesCoach\Data\Seed\demo-feedback.json" Link="demo-feedback.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

- [ ] **Step 2: Write the failing tests**

`tests/WeeklySalesCoach.Tests/GeminiFeedbackGeneratorTests.cs`:

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WeeklySalesCoach.Configuration;
using WeeklySalesCoach.Services.AI;
using WeeklySalesCoach.Services.Coaching;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class GeminiFeedbackGeneratorTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return respond(request);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string GeminiBody(string text) => JsonSerializer.Serialize(new
    {
        candidates = new[] { new { content = new { parts = new[] { new { text } } } } },
    });

    private static GeminiFeedbackGenerator Create(StubHandler handler, string? apiKey = "test-key") => new(
        new HttpClient(handler),
        Options.Create(new GeminiOptions { ApiKey = apiKey, Model = "gemini-test" }),
        NullLogger<GeminiFeedbackGenerator>.Instance);

    [Fact]
    public async Task Returns_text_and_sends_key_in_header_with_schema()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, GeminiBody(TestContent.ValidJson)));

        var result = await Create(handler).GenerateAsync(TestContent.SampleContext());

        Assert.Equal(TestContent.ValidJson, result);
        Assert.Equal("test-key", handler.Request!.Headers.GetValues("x-goog-api-key").Single());
        Assert.Contains("models/gemini-test:generateContent", handler.Request.RequestUri!.ToString());
        Assert.DoesNotContain("test-key", handler.Request.RequestUri.ToString());
        Assert.Contains("\"responseMimeType\":\"application/json\"", handler.RequestBody);
        Assert.Contains("\"responseSchema\"", handler.RequestBody);
        Assert.Contains("Ana Torres", handler.RequestBody);
    }

    [Fact]
    public async Task Maps_429_to_rate_limited()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.TooManyRequests, "{}"));

        var ex = await Assert.ThrowsAsync<AiUnavailableException>(() => Create(handler).GenerateAsync(TestContent.SampleContext()));

        Assert.Equal(AiFailureKind.RateLimited, ex.Kind);
    }

    [Fact]
    public async Task Maps_server_errors_to_unavailable()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.InternalServerError, "{\"error\":\"boom\"}"));

        var ex = await Assert.ThrowsAsync<AiUnavailableException>(() => Create(handler).GenerateAsync(TestContent.SampleContext()));

        Assert.Equal(AiFailureKind.Unavailable, ex.Kind);
    }

    [Fact]
    public async Task Maps_timeouts_to_timeout()
    {
        var handler = new StubHandler(_ => throw new TaskCanceledException("timed out"));

        var ex = await Assert.ThrowsAsync<AiUnavailableException>(() => Create(handler).GenerateAsync(TestContent.SampleContext()));

        Assert.Equal(AiFailureKind.Timeout, ex.Kind);
    }

    [Fact]
    public async Task Response_without_candidates_is_invalid_content()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "{}"));

        await Assert.ThrowsAsync<InvalidCoachingContentException>(() => Create(handler).GenerateAsync(TestContent.SampleContext()));
    }

    [Fact]
    public async Task Missing_key_is_not_configured_and_makes_no_request()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, GeminiBody(TestContent.ValidJson)));

        var ex = await Assert.ThrowsAsync<AiUnavailableException>(() => Create(handler, apiKey: " ").GenerateAsync(TestContent.SampleContext()));

        Assert.Equal(AiFailureKind.NotConfigured, ex.Kind);
        Assert.Equal(0, handler.Calls);
    }
}
```

`tests/WeeklySalesCoach.Tests/DemoFeedbackGeneratorTests.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Services.AI;
using WeeklySalesCoach.Services.Coaching;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class DemoFeedbackGeneratorTests
{
    [Fact]
    public async Task Returns_pre_generated_json_for_a_known_rep()
    {
        var path = Path.Combine(Path.GetTempPath(), $"demo-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{ \"Ana Torres\": " + TestContent.ValidJson + " }");
        try
        {
            var generator = new DemoFeedbackGenerator(path);

            var raw = await generator.GenerateAsync(TestContent.SampleContext("Ana Torres"));

            Assert.False(generator.IsLive);
            Assert.Equal("Solid week.", CoachingContentParser.Parse(raw).Summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Unknown_rep_raises_unavailable()
    {
        var generator = new DemoFeedbackGenerator(Path.Combine(Path.GetTempPath(), "does-not-exist.json"));

        var ex = await Assert.ThrowsAsync<AiUnavailableException>(() => generator.GenerateAsync(TestContent.SampleContext("Nobody")));

        Assert.Equal(AiFailureKind.Unavailable, ex.Kind);
    }

    [Fact]
    public async Task Committed_demo_feedback_covers_every_seeded_rep()
    {
        using var t = new TestDb();
        await using (var db = t.CreateContext())
        {
            await SeedData.SeedAsync(db, new PasswordHasher<AppUser>());
        }
        await using var check = t.CreateContext();
        var names = await check.Reps.Select(r => r.Name).ToListAsync();
        var generator = new DemoFeedbackGenerator(Path.Combine(AppContext.BaseDirectory, "demo-feedback.json"));

        foreach (var name in names)
        {
            var raw = await generator.GenerateAsync(TestContent.SampleContext(name));
            var content = CoachingContentParser.Parse(raw);
            Assert.False(string.IsNullOrWhiteSpace(content.Summary), name);
        }
    }
}
```

`tests/WeeklySalesCoach.Tests/AiErrorMessagesTests.cs`:

```csharp
using WeeklySalesCoach.Services.AI;
using WeeklySalesCoach.Services.Coaching;

namespace WeeklySalesCoach.Tests;

public class AiErrorMessagesTests
{
    [Theory]
    [InlineData(AiFailureKind.Timeout, "didn't respond")]
    [InlineData(AiFailureKind.RateLimited, "usage limit")]
    [InlineData(AiFailureKind.NotConfigured, "Gemini API key")]
    [InlineData(AiFailureKind.Unavailable, "unavailable")]
    public void Maps_each_failure_kind_to_a_friendly_message(AiFailureKind kind, string expected)
    {
        Assert.Contains(expected, AiErrorMessages.For(new AiUnavailableException(kind, "internal detail")));
    }

    [Fact]
    public void Never_exposes_internal_details()
    {
        var message = AiErrorMessages.For(new AiUnavailableException(AiFailureKind.Unavailable, "secret stack info"));

        Assert.DoesNotContain("secret", message);
    }

    [Fact]
    public void Maps_invalid_content()
    {
        Assert.Contains("incomplete", AiErrorMessages.For(new InvalidCoachingContentException("x")));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~GeminiFeedbackGeneratorTests|FullyQualifiedName~DemoFeedbackGeneratorTests|FullyQualifiedName~AiErrorMessagesTests"`
Expected: build FAILS: `The type or namespace name 'GeminiFeedbackGenerator' could not be found`.

- [ ] **Step 4: Implement the interface, the generators and the messages**

`src/WeeklySalesCoach/Services/AI/IFeedbackGenerator.cs`:

```csharp
using WeeklySalesCoach.Services.Coaching;

namespace WeeklySalesCoach.Services.AI;

/// <summary>Produces raw coaching JSON for one rep. Swappable: Gemini, demo file, or a test fake.</summary>
public interface IFeedbackGenerator
{
    bool IsLive { get; }
    string ModelName { get; }
    Task<string> GenerateAsync(CoachingContext context, CancellationToken ct = default);
}

public enum AiFailureKind { Timeout, RateLimited, Unavailable, NotConfigured }

public class AiUnavailableException(AiFailureKind kind, string message, Exception? inner = null) : Exception(message, inner)
{
    public AiFailureKind Kind { get; } = kind;
}
```

`src/WeeklySalesCoach/Services/AI/GeminiFeedbackGenerator.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WeeklySalesCoach.Configuration;
using WeeklySalesCoach.Services.Coaching;

namespace WeeklySalesCoach.Services.AI;

public class GeminiFeedbackGenerator(HttpClient http, IOptions<GeminiOptions> options, ILogger<GeminiFeedbackGenerator> logger)
    : IFeedbackGenerator
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";

    public bool IsLive => true;
    public string ModelName => options.Value.Model;

    public async Task<string> GenerateAsync(CoachingContext context, CancellationToken ct = default)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
            throw new AiUnavailableException(AiFailureKind.NotConfigured, "Gemini API key is not configured.");

        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = CoachingPrompt.SystemInstructions } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = CoachingPrompt.BuildUserMessage(context) } } } },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = CoachingPrompt.ResponseSchema,
                temperature = 0.4,
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}{settings.Model}:generateContent")
        {
            Content = JsonContent.Create(body),
        };
        // The key travels in a header, never in the URL, so it can't leak into logs.
        request.Headers.Add("x-goog-api-key", settings.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new AiUnavailableException(AiFailureKind.Timeout, "Gemini did not respond in time.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new AiUnavailableException(AiFailureKind.Unavailable, "Could not reach Gemini.", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new AiUnavailableException(AiFailureKind.RateLimited, "Gemini rate limit reached.");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Gemini returned {Status}: {Body}", (int)response.StatusCode, error.Length > 500 ? error[..500] : error);
                throw new AiUnavailableException(AiFailureKind.Unavailable, $"Gemini returned {(int)response.StatusCode}.");
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            return ExtractText(doc.RootElement);
        }
    }

    private static string ExtractText(JsonElement root)
    {
        if (root.TryGetProperty("candidates", out var candidates)
            && candidates.ValueKind == JsonValueKind.Array
            && candidates.GetArrayLength() > 0
            && candidates[0].TryGetProperty("content", out var content)
            && content.TryGetProperty("parts", out var parts)
            && parts.ValueKind == JsonValueKind.Array)
        {
            var text = string.Concat(parts.EnumerateArray()
                .Select(p => p.TryGetProperty("text", out var t) ? t.GetString() : null));
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }

        throw new InvalidCoachingContentException("The AI response contained no content.");
    }
}
```

`src/WeeklySalesCoach/Services/AI/DemoFeedbackGenerator.cs`:

```csharp
using System.Text.Json;
using WeeklySalesCoach.Services.Coaching;

namespace WeeklySalesCoach.Services.AI;

/// <summary>Serves feedback pre-generated by the real Gemini integration, so the demo runs without an API key.</summary>
public class DemoFeedbackGenerator(string filePath) : IFeedbackGenerator
{
    private readonly Lazy<Dictionary<string, JsonElement>> _entries = new(() => Load(filePath));

    public bool IsLive => false;
    public string ModelName => "pre-generated demo";

    public Task<string> GenerateAsync(CoachingContext context, CancellationToken ct = default)
    {
        if (!_entries.Value.TryGetValue(context.Rep.Name, out var entry))
            throw new AiUnavailableException(AiFailureKind.Unavailable, $"No pre-generated demo feedback for {context.Rep.Name}.");

        return Task.FromResult(entry.GetRawText());
    }

    private static Dictionary<string, JsonElement> Load(string path)
    {
        if (!File.Exists(path)) return new(StringComparer.Ordinal);
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stream) ?? new(StringComparer.Ordinal);
    }
}
```

`src/WeeklySalesCoach/Services/AI/AiErrorMessages.cs`:

```csharp
using WeeklySalesCoach.Services.Coaching;

namespace WeeklySalesCoach.Services.AI;

/// <summary>User-facing text for AI failures. Never includes exception details.</summary>
public static class AiErrorMessages
{
    public static string For(Exception ex) => ex switch
    {
        AiUnavailableException { Kind: AiFailureKind.Timeout } => "The AI didn't respond in time. Please try again.",
        AiUnavailableException { Kind: AiFailureKind.RateLimited } => "AI usage limit reached. Please wait a minute and try again.",
        AiUnavailableException { Kind: AiFailureKind.NotConfigured } => "Live AI isn't configured. Add a Gemini API key to generate live (see README).",
        AiUnavailableException => "The AI service is unavailable right now. Please try again.",
        InvalidCoachingContentException => "The AI returned an incomplete report twice. Please try again.",
        _ => "Something went wrong. Please try again.",
    };
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~GeminiFeedbackGeneratorTests|FullyQualifiedName~DemoFeedbackGeneratorTests|FullyQualifiedName~AiErrorMessagesTests"`
Expected: PASS (15 tests).

- [ ] **Step 6: Checkpoint**

Run: `dotnet test`
Expected: all tests pass.

---

### Task 7: CoachingService (report lifecycle)

**Files:**
- Create: `src/WeeklySalesCoach/Services/Coaching/CoachingService.cs`
- Create: `tests/WeeklySalesCoach.Tests/TestSupport/FakeFeedbackGenerator.cs`
- Test: `tests/WeeklySalesCoach.Tests/CoachingServiceTests.cs`

**Interfaces:**
- Consumes: `CoachingContextBuilder` (Task 5), `IFeedbackGenerator`, `AiUnavailableException` (Task 6), `CoachingContentParser`, `InvalidCoachingContentException` (Task 4), entities (Task 2)
- Produces: `CoachingService(IDbContextFactory<AppDbContext>, CoachingContextBuilder, IFeedbackGenerator, TimeProvider, ILogger<CoachingService>)` with:
  - `const int ManagerNoteMaxLength = 1000`, `bool IsLiveAi`
  - `Task<Week> GetCurrentWeekAsync(CancellationToken ct = default)`
  - `Task<CoachingReport> GetOrCreateReportAsync(int repId, int weekId, CancellationToken ct = default)`
  - `Task SaveManagerNoteAsync(int repId, int weekId, string? note, CancellationToken ct = default)`: throws `ArgumentException` if > 1000 chars, `InvalidOperationException` if approved
  - `Task<CoachingReport> GenerateAsync(int repId, int weekId, CancellationToken ct = default)`: one retry on `InvalidCoachingContentException`; `AiUnavailableException` propagates; throws `InvalidOperationException` if approved
  - `Task SaveEditsAsync(int reportId, CoachingContent content, CancellationToken ct = default)`: Draft only; no-op when unchanged; sets `IsEdited`
  - `Task ApproveAsync(int reportId, int approvedByUserId, CancellationToken ct = default)`: Draft only
  - `Task<CoachingReport?> GetApprovedForRepAsync(int repId, int weekId, CancellationToken ct = default)`
  - `static CoachingContent? ReadContent(CoachingReport report)`
- Test helper: `FakeFeedbackGenerator(params Func<string>[] responses)` with `Calls` and `IsLive { init; }`

- [ ] **Step 1: Write the fake generator and the failing tests**

`tests/WeeklySalesCoach.Tests/TestSupport/FakeFeedbackGenerator.cs`:

```csharp
using WeeklySalesCoach.Services.AI;
using WeeklySalesCoach.Services.Coaching;

namespace WeeklySalesCoach.Tests.TestSupport;

/// <summary>Returns the queued responses in order. A response may throw to simulate failures.</summary>
public sealed class FakeFeedbackGenerator(params Func<string>[] responses) : IFeedbackGenerator
{
    private readonly Queue<Func<string>> _responses = new(responses);

    public int Calls { get; private set; }
    public bool IsLive { get; init; }
    public string ModelName => "fake-model";

    public Task<string> GenerateAsync(CoachingContext context, CancellationToken ct = default)
    {
        Calls++;
        return Task.FromResult(_responses.Dequeue()());
    }
}
```

`tests/WeeklySalesCoach.Tests/CoachingServiceTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WeeklySalesCoach.Configuration;
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Services.AI;
using WeeklySalesCoach.Services.Coaching;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class CoachingServiceTests
{
    private static (int RepA, int RepB, int WeekId) Seed(TestDb t)
    {
        using var db = t.CreateContext();
        var week = TestData.AddWeek(db, new DateOnly(2026, 9, 14), isCurrent: true);
        var a = TestData.AddRep(db, "Ana Torres");
        var b = TestData.AddRep(db, "Carlos Mendoza");
        TestData.AddMetrics(db, a, week);
        TestData.AddMetrics(db, b, week);
        db.SaveChanges();
        return (a.Id, b.Id, week.Id);
    }

    private static CoachingService Service(TestDb t, IFeedbackGenerator generator) => new(
        t.Factory,
        new CoachingContextBuilder(t.Factory, Options.Create(new DomainOptions())),
        generator,
        TimeProvider.System,
        NullLogger<CoachingService>.Instance);

    private static Func<string> Valid => () => TestContent.ValidJson;

    [Fact]
    public async Task Generate_saves_a_draft_with_source_and_model()
    {
        using var t = new TestDb();
        var (repA, _, weekId) = Seed(t);

        var report = await Service(t, new FakeFeedbackGenerator(Valid)).GenerateAsync(repA, weekId);

        Assert.Equal(ReportStatus.Draft, report.Status);
        Assert.Equal(ReportSource.Demo, report.Source);
        Assert.Equal("fake-model", report.ModelName);
        Assert.False(report.IsEdited);
        Assert.NotNull(report.GeneratedAtUtc);
        Assert.Equal("Solid week.", CoachingService.ReadContent(report)!.Summary);
    }

    [Fact]
    public async Task Generate_retries_once_on_invalid_content()
    {
        using var t = new TestDb();
        var (repA, _, weekId) = Seed(t);
        var generator = new FakeFeedbackGenerator(() => "not json", Valid);

        var report = await Service(t, generator).GenerateAsync(repA, weekId);

        Assert.Equal(2, generator.Calls);
        Assert.Equal(ReportStatus.Draft, report.Status);
    }

    [Fact]
    public async Task Generate_gives_up_after_two_invalid_responses_and_saves_nothing()
    {
        using var t = new TestDb();
        var (repA, _, weekId) = Seed(t);
        var generator = new FakeFeedbackGenerator(() => "not json", () => "{}");

        await Assert.ThrowsAsync<InvalidCoachingContentException>(() => Service(t, generator).GenerateAsync(repA, weekId));

        await using var db = t.CreateContext();
        var report = await db.CoachingReports.SingleAsync(r => r.RepId == repA);
        Assert.Equal(ReportStatus.NotGenerated, report.Status);
        Assert.Null(report.ContentJson);
    }

    [Fact]
    public async Task Generate_keeps_the_existing_draft_when_the_ai_is_unavailable()
    {
        using var t = new TestDb();
        var (repA, _, weekId) = Seed(t);
        var first = await Service(t, new FakeFeedbackGenerator(Valid)).GenerateAsync(repA, weekId);
        var failing = new FakeFeedbackGenerator(() => throw new AiUnavailableException(AiFailureKind.RateLimited, "429"));

        var ex = await Assert.ThrowsAsync<AiUnavailableException>(() => Service(t, failing).GenerateAsync(repA, weekId));

        Assert.Equal(AiFailureKind.RateLimited, ex.Kind);
        await using var db = t.CreateContext();
        var report = await db.CoachingReports.SingleAsync(r => r.RepId == repA);
        Assert.Equal(ReportStatus.Draft, report.Status);
        Assert.Equal(first.ContentJson, report.ContentJson);
    }

    [Fact]
    public async Task Drafts_are_not_visible_to_the_rep()
    {
        using var t = new TestDb();
        var (repA, _, weekId) = Seed(t);
        var service = Service(t, new FakeFeedbackGenerator(Valid));
        await service.GenerateAsync(repA, weekId);

        Assert.Null(await service.GetApprovedForRepAsync(repA, weekId));
    }

    [Fact]
    public async Task Approved_report_is_visible_only_to_its_own_rep()
    {
        using var t = new TestDb();
        var (repA, repB, weekId) = Seed(t);
        var service = Service(t, new FakeFeedbackGenerator(Valid, Valid));
        var reportA = await service.GenerateAsync(repA, weekId);
        await service.GenerateAsync(repB, weekId);

        await service.ApproveAsync(reportA.Id, approvedByUserId: 1);

        var visibleToA = await service.GetApprovedForRepAsync(repA, weekId);
        Assert.NotNull(visibleToA);
        Assert.Equal(repA, visibleToA.RepId);
        Assert.Equal(1, visibleToA.ApprovedByUserId);
        Assert.Null(await service.GetApprovedForRepAsync(repB, weekId));
    }

    [Fact]
    public async Task Approved_report_cannot_be_edited_regenerated_or_noted()
    {
        using var t = new TestDb();
        var (repA, _, weekId) = Seed(t);
        var service = Service(t, new FakeFeedbackGenerator(Valid, Valid));
        var report = await service.GenerateAsync(repA, weekId);
        await service.ApproveAsync(report.Id, approvedByUserId: 1);
        var content = CoachingService.ReadContent(report)!;
        content.Summary = "Changed after approval";

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveEditsAsync(report.Id, content));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateAsync(repA, weekId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveManagerNoteAsync(repA, weekId, "late note"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(report.Id, approvedByUserId: 1));

        var stored = await service.GetApprovedForRepAsync(repA, weekId);
        Assert.Equal("Solid week.", CoachingService.ReadContent(stored!)!.Summary);
    }

    [Fact]
    public async Task SaveEdits_marks_the_report_as_edited_only_when_content_changes()
    {
        using var t = new TestDb();
        var (repA, _, weekId) = Seed(t);
        var service = Service(t, new FakeFeedbackGenerator(Valid));
        var report = await service.GenerateAsync(repA, weekId);

        await service.SaveEditsAsync(report.Id, CoachingService.ReadContent(report)!);
        Assert.False((await service.GetOrCreateReportAsync(repA, weekId)).IsEdited);

        var edited = CoachingService.ReadContent(report)!;
        edited.Summary = "Edited by the manager.";
        await service.SaveEditsAsync(report.Id, edited);

        var stored = await service.GetOrCreateReportAsync(repA, weekId);
        Assert.True(stored.IsEdited);
        Assert.Equal("Edited by the manager.", CoachingService.ReadContent(stored)!.Summary);
    }

    [Fact]
    public async Task SaveEdits_rejects_content_without_evidence()
    {
        using var t = new TestDb();
        var (repA, _, weekId) = Seed(t);
        var service = Service(t, new FakeFeedbackGenerator(Valid));
        var report = await service.GenerateAsync(repA, weekId);
        var edited = CoachingService.ReadContent(report)!;
        edited.Strengths[0].Evidence = " ";

        await Assert.ThrowsAsync<InvalidCoachingContentException>(() => service.SaveEditsAsync(report.Id, edited));
    }

    [Fact]
    public async Task Manager_note_is_saved_trimmed_and_limited_in_length()
    {
        using var t = new TestDb();
        var (repA, _, weekId) = Seed(t);
        var service = Service(t, new FakeFeedbackGenerator());

        await service.SaveManagerNoteAsync(repA, weekId, "  Out 2 days.  ");
        Assert.Equal("Out 2 days.", (await service.GetOrCreateReportAsync(repA, weekId)).ManagerNote);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveManagerNoteAsync(repA, weekId, new string('x', CoachingService.ManagerNoteMaxLength + 1)));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~CoachingServiceTests"`
Expected: build FAILS: `The type or namespace name 'CoachingService' could not be found`.

- [ ] **Step 3: Implement `CoachingService`**

`src/WeeklySalesCoach/Services/Coaching/CoachingService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Services.AI;

namespace WeeklySalesCoach.Services.Coaching;

/// <summary>Report lifecycle: NotGenerated → Draft (AI) → edited by manager → Approved (visible to the rep).</summary>
public class CoachingService(
    IDbContextFactory<AppDbContext> dbFactory,
    CoachingContextBuilder contextBuilder,
    IFeedbackGenerator generator,
    TimeProvider time,
    ILogger<CoachingService> logger)
{
    public const int ManagerNoteMaxLength = 1000;

    public bool IsLiveAi => generator.IsLive;

    public async Task<Week> GetCurrentWeekAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Weeks.AsNoTracking().SingleAsync(w => w.IsCurrent, ct);
    }

    public async Task<CoachingReport> GetOrCreateReportAsync(int repId, int weekId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await GetOrCreateAsync(db, repId, weekId, ct);
    }

    public async Task SaveManagerNoteAsync(int repId, int weekId, string? note, CancellationToken ct = default)
    {
        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmed?.Length > ManagerNoteMaxLength)
            throw new ArgumentException($"The manager note must be {ManagerNoteMaxLength} characters or fewer.", nameof(note));

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var report = await GetOrCreateAsync(db, repId, weekId, ct);
        EnsureNotApproved(report);
        report.ManagerNote = trimmed;
        await db.SaveChangesAsync(ct);
    }

    public async Task<CoachingReport> GenerateAsync(int repId, int weekId, CancellationToken ct = default)
    {
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            EnsureNotApproved(await GetOrCreateAsync(db, repId, weekId, ct));
        }

        var context = await contextBuilder.BuildAsync(repId, weekId, ct);
        var content = await GenerateValidContentAsync(context, ct);

        await using var save = await dbFactory.CreateDbContextAsync(ct);
        var report = await save.CoachingReports.SingleAsync(r => r.RepId == repId && r.WeekId == weekId, ct);
        EnsureNotApproved(report);
        report.ContentJson = CoachingContentParser.Serialize(content);
        report.Status = ReportStatus.Draft;
        report.Source = generator.IsLive ? ReportSource.Live : ReportSource.Demo;
        report.ModelName = generator.ModelName;
        report.IsEdited = false;
        report.GeneratedAtUtc = time.GetUtcNow().UtcDateTime;
        await save.SaveChangesAsync(ct);
        return report;
    }

    public async Task SaveEditsAsync(int reportId, CoachingContent content, CancellationToken ct = default)
    {
        CoachingContentParser.Validate(content);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var report = await db.CoachingReports.SingleAsync(r => r.Id == reportId, ct);
        EnsureNotApproved(report);
        if (report.Status != ReportStatus.Draft)
            throw new InvalidOperationException("Generate a draft before editing.");

        var json = CoachingContentParser.Serialize(content);
        if (json == report.ContentJson) return;

        report.ContentJson = json;
        report.IsEdited = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task ApproveAsync(int reportId, int approvedByUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var report = await db.CoachingReports.SingleAsync(r => r.Id == reportId, ct);
        EnsureNotApproved(report);
        if (report.Status != ReportStatus.Draft)
            throw new InvalidOperationException("Only a draft can be approved.");

        report.Status = ReportStatus.Approved;
        report.ApprovedAtUtc = time.GetUtcNow().UtcDateTime;
        report.ApprovedByUserId = approvedByUserId;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>What a rep may see: only their own report, only once approved.</summary>
    public async Task<CoachingReport?> GetApprovedForRepAsync(int repId, int weekId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CoachingReports.AsNoTracking()
            .SingleOrDefaultAsync(r => r.RepId == repId && r.WeekId == weekId && r.Status == ReportStatus.Approved, ct);
    }

    public static CoachingContent? ReadContent(CoachingReport report) =>
        report.ContentJson is null ? null : CoachingContentParser.FromStored(report.ContentJson);

    private async Task<CoachingContent> GenerateValidContentAsync(CoachingContext context, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return CoachingContentParser.Parse(await generator.GenerateAsync(context, ct));
            }
            catch (InvalidCoachingContentException ex) when (attempt == 1)
            {
                logger.LogWarning(ex, "AI returned invalid content for rep {RepId}; retrying once.", context.RepId);
            }
        }
    }

    private static async Task<CoachingReport> GetOrCreateAsync(AppDbContext db, int repId, int weekId, CancellationToken ct)
    {
        var report = await db.CoachingReports.SingleOrDefaultAsync(r => r.RepId == repId && r.WeekId == weekId, ct);
        if (report is not null) return report;

        report = new CoachingReport { RepId = repId, WeekId = weekId, Status = ReportStatus.NotGenerated };
        db.CoachingReports.Add(report);
        await db.SaveChangesAsync(ct);
        return report;
    }

    private static void EnsureNotApproved(CoachingReport report)
    {
        if (report.Status == ReportStatus.Approved)
            throw new InvalidOperationException("This report is already approved and can no longer be changed.");
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~CoachingServiceTests"`
Expected: PASS (10 tests).

- [ ] **Step 5: Checkpoint**

Run: `dotnet test`
Expected: all tests pass.

---

### Task 8: Team dashboard service and formatting

**Files:**
- Create: `src/WeeklySalesCoach/Services/Coaching/TeamDashboardService.cs`, `src/WeeklySalesCoach/Services/Fmt.cs`
- Test: `tests/WeeklySalesCoach.Tests/TeamDashboardServiceTests.cs`, `tests/WeeklySalesCoach.Tests/FmtTests.cs`

**Interfaces:**
- Consumes: entities (Task 2), `Calc.Attainment` (Task 5)
- Produces:
  - `record TeamKpis(decimal WonVolume, decimal TeamQuota, int DealsWon, int Applications, double AvgLeadResponseHours)`
  - `record RepCard(int RepId, string Name, string Title, string Initials, decimal WonVolume, decimal Quota, double AttainmentPercent, ReportStatus Status, bool IsEdited)`
  - `record TeamDashboard(Week Week, TeamKpis Kpis, IReadOnlyList<RepCard> Reps)`
  - `TeamDashboardService(IDbContextFactory<AppDbContext>)` with `Task<TeamDashboard> GetAsync(int weekId, CancellationToken ct = default)` and `static string Initials(string name)`
  - `static Fmt.Money(decimal) : string`, `Fmt.CompactMoney(decimal) : string`, `Fmt.Percent(double) : string` (namespace `WeeklySalesCoach.Services`)

- [ ] **Step 1: Write the failing tests**

`tests/WeeklySalesCoach.Tests/TeamDashboardServiceTests.cs`:

```csharp
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Services.Coaching;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class TeamDashboardServiceTests
{
    [Fact]
    public async Task Computes_team_kpis_and_rep_cards()
    {
        using var t = new TestDb();
        int weekId;
        using (var db = t.CreateContext())
        {
            var week = TestData.AddWeek(db, new DateOnly(2026, 9, 14), isCurrent: true);
            var ana = TestData.AddRep(db, "Ana Torres", quota: 200_000m);
            var carlos = TestData.AddRep(db, "Carlos Mendoza", quota: 100_000m);
            TestData.AddMetrics(db, ana, week, wonVolume: 150_000m, dealsWon: 2, applications: 3, responseHours: 4.0);
            TestData.AddMetrics(db, carlos, week, wonVolume: 50_000m, dealsWon: 1, applications: 5, responseHours: 2.0);
            db.CoachingReports.Add(new CoachingReport { Rep = ana, Week = week, Status = ReportStatus.Draft, IsEdited = true });
            db.SaveChanges();
            weekId = week.Id;
        }

        var dashboard = await new TeamDashboardService(t.Factory).GetAsync(weekId);

        Assert.Equal(200_000m, dashboard.Kpis.WonVolume);
        Assert.Equal(300_000m, dashboard.Kpis.TeamQuota);
        Assert.Equal(3, dashboard.Kpis.DealsWon);
        Assert.Equal(8, dashboard.Kpis.Applications);
        Assert.Equal(3.0, dashboard.Kpis.AvgLeadResponseHours);

        var ana = dashboard.Reps[0];
        Assert.Equal("Ana Torres", ana.Name);
        Assert.Equal("AT", ana.Initials);
        Assert.Equal(75.0, ana.AttainmentPercent);
        Assert.Equal(ReportStatus.Draft, ana.Status);
        Assert.True(ana.IsEdited);
        Assert.Equal(ReportStatus.NotGenerated, dashboard.Reps[1].Status);
    }

    [Fact]
    public async Task Attainment_is_zero_for_zero_quota()
    {
        using var t = new TestDb();
        int weekId;
        using (var db = t.CreateContext())
        {
            var week = TestData.AddWeek(db, new DateOnly(2026, 9, 14), isCurrent: true);
            var rep = TestData.AddRep(db, "Laura Bennett", quota: 0m);
            TestData.AddMetrics(db, rep, week, wonVolume: 10_000m);
            db.SaveChanges();
            weekId = week.Id;
        }

        var dashboard = await new TeamDashboardService(t.Factory).GetAsync(weekId);

        Assert.Equal(0, dashboard.Reps[0].AttainmentPercent);
    }

    [Theory]
    [InlineData("Sofía Herrera", "SH")]
    [InlineData("Cher", "C")]
    [InlineData("Ana María Torres", "AM")]
    public void Initials_use_the_first_two_words(string name, string expected)
    {
        Assert.Equal(expected, TeamDashboardService.Initials(name));
    }
}
```

`tests/WeeklySalesCoach.Tests/FmtTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~TeamDashboardServiceTests|FullyQualifiedName~FmtTests"`
Expected: build FAILS: `The type or namespace name 'TeamDashboardService' could not be found`.

- [ ] **Step 3: Implement**

`src/WeeklySalesCoach/Services/Coaching/TeamDashboardService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using WeeklySalesCoach.Data;

namespace WeeklySalesCoach.Services.Coaching;

public record TeamKpis(decimal WonVolume, decimal TeamQuota, int DealsWon, int Applications, double AvgLeadResponseHours);

public record RepCard(int RepId, string Name, string Title, string Initials, decimal WonVolume, decimal Quota,
    double AttainmentPercent, ReportStatus Status, bool IsEdited);

public record TeamDashboard(Week Week, TeamKpis Kpis, IReadOnlyList<RepCard> Reps);

public class TeamDashboardService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<TeamDashboard> GetAsync(int weekId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var week = await db.Weeks.AsNoTracking().SingleAsync(w => w.Id == weekId, ct);
        var reps = await db.Reps.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);
        var metrics = await db.WeeklyMetrics.AsNoTracking().Where(m => m.WeekId == weekId).ToDictionaryAsync(m => m.RepId, ct);
        var reports = await db.CoachingReports.AsNoTracking().Where(r => r.WeekId == weekId).ToDictionaryAsync(r => r.RepId, ct);

        var cards = reps.Select(rep =>
        {
            metrics.TryGetValue(rep.Id, out var m);
            reports.TryGetValue(rep.Id, out var report);
            var won = m?.WonVolume ?? 0m;
            return new RepCard(rep.Id, rep.Name, rep.Title, Initials(rep.Name), won, rep.WeeklyQuota,
                Calc.Attainment(won, rep.WeeklyQuota), report?.Status ?? ReportStatus.NotGenerated, report?.IsEdited ?? false);
        }).ToList();

        var all = metrics.Values.ToList();
        var kpis = new TeamKpis(
            all.Sum(m => m.WonVolume),
            reps.Sum(r => r.WeeklyQuota),
            all.Sum(m => m.DealsWon),
            all.Sum(m => m.ApplicationsSubmitted),
            all.Count == 0 ? 0 : Math.Round(all.Average(m => m.AvgLeadResponseHours), 1));

        return new TeamDashboard(week, kpis, cards);
    }

    public static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));
}
```

`src/WeeklySalesCoach/Services/Fmt.cs`:

```csharp
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
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~TeamDashboardServiceTests|FullyQualifiedName~FmtTests"`
Expected: PASS (6 tests).

- [ ] **Step 5: Checkpoint**

Run: `dotnet test`
Expected: all tests pass.

---

### Task 9: App wiring and authentication

**Files:**
- Create: `src/WeeklySalesCoach/Auth/UserAuthService.cs`, `src/WeeklySalesCoach/Auth/ClaimsPrincipalExtensions.cs`, `src/WeeklySalesCoach/Auth/AuthEndpoints.cs`
- Replace: `src/WeeklySalesCoach/Program.cs`, `src/WeeklySalesCoach/appsettings.json`, `src/WeeklySalesCoach/Components/Routes.razor`, `src/WeeklySalesCoach/Components/_Imports.razor`, `src/WeeklySalesCoach/Components/Pages/Error.razor`
- Delete: `src/WeeklySalesCoach/Components/Pages/Home.razor` (`/` becomes a role-based redirect endpoint)
- Test: `tests/WeeklySalesCoach.Tests/UserAuthServiceTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 2–8
- Produces:
  - `UserAuthService(IDbContextFactory<AppDbContext>, IPasswordHasher<AppUser>)` with `const string RepIdClaim = "rep_id"`, `Task<AppUser?> ValidateCredentialsAsync(string? username, string? password, CancellationToken ct = default)`, `Task<AppUser?> FindByUsernameAsync(string? username, CancellationToken ct = default)`, `static ClaimsPrincipal CreatePrincipal(AppUser user)`
  - Extensions `ClaimsPrincipal.GetUserId() : int?`, `ClaimsPrincipal.GetRepId() : int?`
  - Endpoints `POST /auth/login` (form: `username`, `password`), `POST /auth/quick-login` (form: `username`), `POST /auth/logout`, `GET /` (redirects to `/login`, `/dashboard` or `/my-feedback`)
  - All services registered in DI: `CoachingContextBuilder`, `CoachingService`, `TeamDashboardService`, `UserAuthService` (scoped), `IFeedbackGenerator` (Gemini if a key is configured, otherwise Demo), `TimeProvider.System`, `IPasswordHasher<AppUser>`

- [ ] **Step 1: Write the failing test**

`tests/WeeklySalesCoach.Tests/UserAuthServiceTests.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using WeeklySalesCoach.Auth;
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class UserAuthServiceTests
{
    private static async Task<(TestDb Db, UserAuthService Auth)> SeededAsync()
    {
        var t = new TestDb();
        var hasher = new PasswordHasher<AppUser>();
        await using (var db = t.CreateContext())
        {
            await SeedData.SeedAsync(db, hasher);
        }
        return (t, new UserAuthService(t.Factory, hasher));
    }

    [Fact]
    public async Task Validates_credentials()
    {
        var (t, auth) = await SeededAsync();
        using var owned = t;

        Assert.NotNull(await auth.ValidateCredentialsAsync("ana", SeedData.DemoPassword));
        Assert.NotNull(await auth.ValidateCredentialsAsync("  ANA ", SeedData.DemoPassword));
        Assert.Null(await auth.ValidateCredentialsAsync("ana", "wrong-password"));
        Assert.Null(await auth.ValidateCredentialsAsync("nobody", SeedData.DemoPassword));
        Assert.Null(await auth.ValidateCredentialsAsync("", ""));
    }

    [Fact]
    public async Task Principal_carries_role_user_id_and_rep_id()
    {
        var (t, auth) = await SeededAsync();
        using var owned = t;
        var ana = (await auth.FindByUsernameAsync("ana"))!;
        var roberto = (await auth.FindByUsernameAsync("roberto"))!;

        var rep = UserAuthService.CreatePrincipal(ana);
        var manager = UserAuthService.CreatePrincipal(roberto);

        Assert.True(rep.IsInRole("Rep"));
        Assert.Equal("Ana Torres", rep.Identity!.Name);
        Assert.Equal(ana.Id, rep.GetUserId());
        Assert.Equal(ana.RepId, rep.GetRepId());
        Assert.True(manager.IsInRole("Manager"));
        Assert.Null(manager.GetRepId());
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~UserAuthServiceTests"`
Expected: build FAILS: `The type or namespace name 'Auth' does not exist`.

- [ ] **Step 3: Implement auth services and endpoints**

`src/WeeklySalesCoach/Auth/UserAuthService.cs`:

```csharp
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WeeklySalesCoach.Data;

namespace WeeklySalesCoach.Auth;

public class UserAuthService(IDbContextFactory<AppDbContext> dbFactory, IPasswordHasher<AppUser> hasher)
{
    public const string RepIdClaim = "rep_id";

    public async Task<AppUser?> ValidateCredentialsAsync(string? username, string? password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) return null;
        var user = await FindByUsernameAsync(username, ct);
        if (user is null) return null;
        return hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed ? null : user;
    }

    public async Task<AppUser?> FindByUsernameAsync(string? username, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;
        var normalized = username.Trim().ToLowerInvariant();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Username == normalized, ct);
    }

    public static ClaimsPrincipal CreatePrincipal(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString()),
        };
        if (user.RepId is int repId)
            claims.Add(new Claim(RepIdClaim, repId.ToString(CultureInfo.InvariantCulture)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
```

`src/WeeklySalesCoach/Auth/ClaimsPrincipalExtensions.cs`:

```csharp
using System.Globalization;
using System.Security.Claims;

namespace WeeklySalesCoach.Auth;

public static class ClaimsPrincipalExtensions
{
    public static int? GetUserId(this ClaimsPrincipal user) => ParseInt(user.FindFirstValue(ClaimTypes.NameIdentifier));

    /// <summary>The signed-in rep's id. Comes from the auth cookie, never from the URL.</summary>
    public static int? GetRepId(this ClaimsPrincipal user) => ParseInt(user.FindFirstValue(UserAuthService.RepIdClaim));

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;
}
```

`src/WeeklySalesCoach/Auth/AuthEndpoints.cs`:

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WeeklySalesCoach.Configuration;
using WeeklySalesCoach.Data;

namespace WeeklySalesCoach.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapGet("/", (HttpContext http) =>
        {
            if (http.User.Identity?.IsAuthenticated != true) return Results.LocalRedirect("/login");
            return Results.LocalRedirect(http.User.IsInRole(nameof(UserRole.Manager)) ? "/dashboard" : "/my-feedback");
        });

        var auth = app.MapGroup("/auth");

        // Form posts from the static login page; antiforgery is validated by UseAntiforgery().
        auth.MapPost("/login", async ([FromForm] string? username, [FromForm] string? password, UserAuthService users, HttpContext http) =>
        {
            var user = await users.ValidateCredentialsAsync(username, password);
            if (user is null) return Results.LocalRedirect("/login?error=1");
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, UserAuthService.CreatePrincipal(user));
            return Results.LocalRedirect("/");
        });

        // Demo-only one-click sign-in. Disabled with Demo:EnableQuickLogin=false.
        auth.MapPost("/quick-login", async ([FromForm] string? username, UserAuthService users, IOptions<DemoOptions> demo, HttpContext http) =>
        {
            if (!demo.Value.EnableQuickLogin) return Results.NotFound();
            var user = await users.FindByUsernameAsync(username);
            if (user is null) return Results.LocalRedirect("/login?error=1");
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, UserAuthService.CreatePrincipal(user));
            return Results.LocalRedirect("/");
        });

        auth.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect("/login");
        });
    }
}
```

- [ ] **Step 4: Replace `Program.cs`, `appsettings.json` and the Razor plumbing**

`src/WeeklySalesCoach/Program.cs`:

```csharp
using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WeeklySalesCoach.Auth;
using WeeklySalesCoach.Components;
using WeeklySalesCoach.Configuration;
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Services.AI;
using WeeklySalesCoach.Services.Coaching;

var enUs = CultureInfo.GetCultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = enUs;
CultureInfo.DefaultThreadCurrentUICulture = enUs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DomainOptions>(builder.Configuration.GetSection(DomainOptions.Section));
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.Section));
builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection(DemoOptions.Section));

builder.Services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

// AI: live Gemini when a key is configured, otherwise the pre-generated demo feedback.
builder.Services.AddHttpClient<GeminiFeedbackGenerator>((sp, client) =>
    client.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<IOptions<GeminiOptions>>().Value.TimeoutSeconds));
builder.Services.AddSingleton(sp => new DemoFeedbackGenerator(
    Path.Combine(builder.Environment.ContentRootPath, sp.GetRequiredService<IOptions<DemoOptions>>().Value.DemoFeedbackPath)));
builder.Services.AddScoped<IFeedbackGenerator>(sp =>
    sp.GetRequiredService<IOptions<GeminiOptions>>().Value.IsConfigured
        ? sp.GetRequiredService<GeminiFeedbackGenerator>()
        : sp.GetRequiredService<DemoFeedbackGenerator>());

builder.Services.AddScoped<CoachingContextBuilder>();
builder.Services.AddScoped<CoachingService>();
builder.Services.AddScoped<TeamDashboardService>();
builder.Services.AddScoped<UserAuthService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/login";
        o.AccessDeniedPath = "/login?denied=1";
        o.Cookie.Name = "wsc.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapAuthEndpoints();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
```

`src/WeeklySalesCoach/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "Default": "Data Source=weeklysalescoach.db"
  },
  "Domain": {
    "DealTerm": "Loan",
    "RepTerm": "Loan Officer",
    "WonStage": "Funded",
    "LostStage": "Lost"
  },
  "Gemini": {
    "ApiKey": "",
    "Model": "gemini-2.5-flash",
    "TimeoutSeconds": 30
  },
  "Demo": {
    "EnableQuickLogin": true,
    "DemoFeedbackPath": "Data/Seed/demo-feedback.json"
  }
}
```

`src/WeeklySalesCoach/Components/Routes.razor`:

```razor
<Router AppAssembly="typeof(Program).Assembly">
    <Found Context="routeData">
        <AuthorizeRouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)">
            <NotAuthorized>
                <section class="hero">
                    <h1>No access</h1>
                    <p class="lead">You don't have access to this page. <a href="/login">Sign in</a> with another account.</p>
                </section>
            </NotAuthorized>
        </AuthorizeRouteView>
        <FocusOnNavigate RouteData="routeData" Selector="h1" />
    </Found>
</Router>
```

`src/WeeklySalesCoach/Components/_Imports.razor`:

```razor
@using System.Globalization
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Authorization
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using static Microsoft.AspNetCore.Components.Web.RenderMode
@using Microsoft.AspNetCore.Components.Web.Virtualization
@using Microsoft.EntityFrameworkCore
@using Microsoft.Extensions.Options
@using Microsoft.JSInterop
@using WeeklySalesCoach
@using WeeklySalesCoach.Auth
@using WeeklySalesCoach.Components
@using WeeklySalesCoach.Components.Layout
@using WeeklySalesCoach.Configuration
@using WeeklySalesCoach.Data
@using WeeklySalesCoach.Services
@using WeeklySalesCoach.Services.AI
@using WeeklySalesCoach.Services.Coaching
```

`src/WeeklySalesCoach/Components/Pages/Error.razor`:

```razor
@page "/Error"
@attribute [AllowAnonymous]

<PageTitle>Error · Weekly Sales Coach</PageTitle>

<section class="hero">
    <h1>Something went wrong</h1>
    <p class="lead">Please go back and try again.</p>
    <a class="btn btn-primary" href="/">Back to start</a>
</section>
```

```bash
rm src/WeeklySalesCoach/Components/Pages/Home.razor
```

- [ ] **Step 5: Run the tests and the build**

Run: `dotnet test --filter "FullyQualifiedName~UserAuthServiceTests"`
Expected: PASS (2 tests).

Run: `dotnet build`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 6: Checkpoint**

Run: `dotnet test`
Expected: all tests pass.

---

### Task 10: Visual style, layout, shared components and login page

**Files:**
- Replace: `src/WeeklySalesCoach/wwwroot/app.css`, `src/WeeklySalesCoach/Components/Layout/MainLayout.razor`
- Delete: `src/WeeklySalesCoach/Components/Layout/MainLayout.razor.css`
- Modify: `src/WeeklySalesCoach/Components/App.razor` (head only), `src/WeeklySalesCoach/Components/_Imports.razor` (add one line)
- Create: `src/WeeklySalesCoach/Components/Shared/PillBadge.razor`, `StatCard.razor`, `StatusChip.razor`, `DemoBanner.razor`, `src/WeeklySalesCoach/Components/Pages/Login.razor`

**Interfaces:**
- Consumes: `/auth/*` endpoints and `UserAuthService` (Task 9), `DemoOptions`, `GeminiOptions`, `DomainOptions` (Task 2)
- Produces: components `<PillBadge Text>`, `<StatCard Value Label>`, `<StatusChip Status IsEdited>`, `<DemoBanner />`; CSS classes used by later pages: `hero`, `hero--left`, `lead`, `accent`, `pill`, `stats`, `stat-value`, `stat-label`, `section-mint`, `card`, `card-grid`, `rep-card`, `avatar`, `progress`, `progress-bar`, `chip-*`, `btn`, `btn-primary`, `btn-outline`, `btn-sm`, `btn-block`, `link-btn`, `banner`, `form-error`, `form-info`, `field`, `review-grid`, `evidence-panel`, `review-panel`, `panel-head`, `panel-actions`, `section-label`, `edit-item`, `edit-title`, `edit-evidence`, `confirm`, `note`, `data-table`, `deal-notes`, `warn`, `feedback`, `feedback-summary`, `feedback-list`, `list-strengths`, `list-improvements`, `list-actions`, `evidence`, `feedback-page`, `empty-state`, `muted`, `small`, `back-link`, `hint`, `hero-actions`

- [ ] **Step 1: Write the stylesheet**

`src/WeeklySalesCoach/wwwroot/app.css`:

```css
:root {
  --ink: #1F2937;
  --green: #3FA45B;
  --green-dark: #2F8A4A;
  --green-50: #EAF6EE;
  --green-200: #CFE8D7;
  --bg: #F7F8F8;
  --mint: #E4F0EE;
  --muted: #5B6B7A;
  --card: #FFFFFF;
  --border: #E5E7EB;
  --amber: #B7791F;
  --amber-50: #FDF6E7;
  --red: #B42318;
  --red-50: #FEF3F2;
  --radius: 8px;
}

* { box-sizing: border-box; }
html, body { margin: 0; }
body {
  font-family: "Manrope", system-ui, -apple-system, "Segoe UI", sans-serif;
  background: var(--bg);
  color: var(--ink);
  line-height: 1.5;
  -webkit-font-smoothing: antialiased;
}
a { color: inherit; }
h1, h2, h3 { margin: 0; letter-spacing: -0.02em; }
h1 { font-size: clamp(2rem, 4.5vw, 3.4rem); line-height: 1.1; font-weight: 800; }
h2 { font-size: 1.35rem; font-weight: 700; }
h3 { font-size: 1.05rem; font-weight: 700; }
.accent { color: var(--green); }
.muted { color: var(--muted); }
.small { font-size: .85rem; }

/* Top bar */
.topbar { position: sticky; top: 0; z-index: 10; background: rgba(247, 248, 248, .92); border-bottom: 1px solid var(--border); backdrop-filter: blur(6px); }
.topbar-inner { max-width: 1200px; margin: 0 auto; padding: 14px 24px; display: flex; align-items: center; justify-content: space-between; gap: 16px; }
.brand { display: inline-flex; align-items: center; gap: 10px; font-weight: 800; font-size: 1.2rem; letter-spacing: -0.03em; text-decoration: none; }
.brand-mark { width: 30px; height: 30px; border-radius: 8px; background: var(--ink); color: #fff; display: grid; place-items: center; font-size: .9rem; }
.topbar-nav { display: flex; align-items: center; gap: 16px; }
.topbar-nav form { margin: 0; }
.topbar-user { font-weight: 600; text-align: right; }
.topbar-user small { display: block; font-weight: 500; font-size: .75rem; color: var(--muted); }

.page { max-width: 1200px; margin: 0 auto; padding: 24px 24px 64px; }
.footer { text-align: center; color: var(--muted); font-size: .85rem; padding: 24px; border-top: 1px solid var(--border); }

/* Hero */
.hero { display: flex; flex-direction: column; align-items: center; gap: 18px; text-align: center; padding: 48px 0 32px; }
.hero--left { align-items: flex-start; text-align: left; padding-top: 16px; }
.lead { margin: 0; max-width: 720px; font-size: 1.2rem; color: var(--muted); }
.hero-actions { display: flex; flex-wrap: wrap; justify-content: center; gap: 12px; }
.hint { margin: 0; font-size: .85rem; color: var(--muted); }
.back-link { color: var(--muted); font-weight: 600; font-size: .9rem; text-decoration: none; }

/* Pill badge */
.pill { display: inline-flex; align-items: center; gap: 8px; padding: 6px 16px; border-radius: 999px; background: var(--green-50); border: 1px solid var(--green-200); color: var(--green-dark); font-weight: 600; font-size: .9rem; }
.pill-dot { width: 6px; height: 6px; border-radius: 50%; background: var(--green); }

/* Buttons */
.btn { display: inline-flex; align-items: center; justify-content: center; gap: 8px; padding: 12px 20px; border: 1px solid transparent; border-radius: 4px; font: inherit; font-weight: 600; text-decoration: none; cursor: pointer; transition: background .15s, border-color .15s; }
.btn:disabled { opacity: .55; cursor: not-allowed; }
.btn-primary { background: var(--ink); color: #fff; }
.btn-primary:hover:not(:disabled) { background: #111827; }
.btn-outline { background: #fff; color: var(--ink); border-color: var(--border); }
.btn-outline:hover:not(:disabled) { border-color: var(--ink); }
.btn-sm { padding: 7px 12px; font-size: .9rem; }
.btn-block { width: 100%; justify-content: space-between; }
.link-btn { padding: 0; border: none; background: none; font: inherit; font-size: .85rem; color: var(--muted); text-decoration: underline; cursor: pointer; align-self: flex-start; }
.link-btn:disabled { opacity: .4; cursor: not-allowed; }

/* Stats row */
.stats { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; padding: 8px 0 40px; text-align: center; }
.stat-value { font-size: clamp(2rem, 4vw, 3rem); font-weight: 800; line-height: 1.1; letter-spacing: -0.03em; color: var(--green); }
.stat-label { color: var(--muted); }

/* Cards */
.section-mint { background: var(--mint); border-radius: 12px; padding: 40px 32px; }
.section-mint > h2 { text-align: center; font-size: 1.8rem; margin-bottom: 24px; }
.card { background: var(--card); border: 1px solid var(--border); border-radius: var(--radius); padding: 24px; box-shadow: 0 1px 2px rgba(16, 24, 40, .04); }
.card-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(210px, 1fr)); gap: 20px; }
.rep-card { display: flex; flex-direction: column; gap: 6px; text-decoration: none; transition: border-color .15s, transform .15s; }
.rep-card:hover { border-color: var(--green); transform: translateY(-2px); }
.rep-card p { margin: 0; }
.rep-card-head { display: flex; align-items: center; justify-content: space-between; margin-bottom: 8px; }
.rep-card-stat { font-size: .9rem; color: var(--muted); }
.rep-card-stat strong { color: var(--ink); }
.avatar { width: 40px; height: 40px; border-radius: 50%; display: grid; place-items: center; background: var(--green-50); color: var(--green-dark); font-weight: 700; }
.progress { height: 6px; margin-top: 8px; border-radius: 999px; background: #EEF1F3; overflow: hidden; }
.progress-bar { height: 100%; background: var(--green); }

/* Status chips */
.chip { display: inline-block; padding: 3px 10px; border-radius: 999px; font-size: .75rem; font-weight: 600; white-space: nowrap; }
.chip-green { background: var(--green-50); color: var(--green-dark); }
.chip-amber { background: var(--amber-50); color: var(--amber); }
.chip-gray { background: #F2F4F7; color: var(--muted); }

/* Messages */
.banner { margin-bottom: 8px; padding: 10px 16px; border: 1px solid #F5DFB3; border-radius: var(--radius); background: var(--amber-50); color: #7A4E0F; font-size: .9rem; }
.form-error { margin: 0; padding: 10px 14px; border-radius: var(--radius); background: var(--red-50); color: var(--red); }
.form-info { margin: 0; padding: 10px 14px; border-radius: var(--radius); background: var(--green-50); color: var(--green-dark); }

/* Forms */
.login-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; max-width: 880px; margin: 0 auto; }
.form-card, .demo-card { display: flex; flex-direction: column; gap: 14px; }
.demo-card form { margin: 0; }
.demo-disclaimer { margin: 0; padding: 8px 12px; border-radius: var(--radius); background: var(--amber-50); color: var(--amber); font-size: .85rem; font-weight: 600; }
.field, .form-card label { display: flex; flex-direction: column; gap: 6px; font-size: .95rem; font-weight: 600; }
input, textarea { width: 100%; padding: 10px 12px; border: 1px solid var(--border); border-radius: 6px; background: #fff; font: inherit; font-weight: 400; color: var(--ink); }
input:focus, textarea:focus { outline: 2px solid var(--green-200); border-color: var(--green); }
textarea { resize: vertical; }

/* Review page */
.review-grid { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: 24px; align-items: start; }
.evidence-panel, .review-panel { display: flex; flex-direction: column; gap: 14px; }
.panel-head { display: flex; align-items: center; justify-content: space-between; }
.panel-actions { display: flex; justify-content: flex-end; gap: 12px; padding-top: 16px; border-top: 1px solid var(--border); }
.section-label { margin-top: 8px; font-size: .8rem; text-transform: uppercase; letter-spacing: .06em; color: var(--muted); }
.edit-item { display: flex; flex-direction: column; gap: 6px; padding: 12px; border: 1px solid var(--border); border-radius: var(--radius); background: #FBFCFC; }
.edit-title { font-weight: 700; }
.edit-evidence { font-size: .88rem; color: var(--muted); }
.confirm { padding: 12px; border-radius: var(--radius); background: var(--amber-50); }
.confirm p { margin: 0 0 8px; }
.note { margin: 0; padding: 10px 14px; border-radius: var(--radius); background: var(--mint); }

.data-table { width: 100%; border-collapse: collapse; font-size: .88rem; }
.data-table th { padding: 8px 6px; border-bottom: 1px solid var(--border); text-align: left; font-weight: 600; color: var(--muted); }
.data-table td { padding: 8px 6px; border-bottom: 1px solid #F0F2F4; vertical-align: top; }
.deal-notes { display: block; font-size: .8rem; color: var(--muted); }
.warn { color: var(--red); font-weight: 700; }

/* Feedback view */
.feedback-page { max-width: 760px; margin: 0 auto; }
.feedback { display: flex; flex-direction: column; gap: 18px; }
.feedback-summary { margin: 0; font-size: 1.1rem; }
.feedback-list { display: flex; flex-direction: column; gap: 12px; margin: 0; padding: 0; list-style: none; }
.feedback-list li { position: relative; padding-left: 30px; }
.feedback-list li::before { position: absolute; left: 0; top: 1px; width: 20px; height: 20px; border-radius: 50%; display: grid; place-items: center; font-size: .75rem; font-weight: 800; }
.list-strengths li::before { content: "✓"; background: var(--green-50); color: var(--green-dark); }
.list-improvements li::before { content: "↗"; background: var(--amber-50); color: var(--amber); }
.list-actions li::before { content: "→"; background: #EEF1F3; color: var(--ink); }
.feedback-list p { margin: 2px 0 0; }
.evidence { font-size: .85rem; color: var(--muted); }
.empty-state { padding: 48px 24px; text-align: center; }

#blazor-error-ui { display: none; position: fixed; left: 0; right: 0; bottom: 0; z-index: 1000; padding: 12px 24px; background: var(--red-50); color: var(--red); }
#blazor-error-ui .dismiss { position: absolute; right: 16px; cursor: pointer; }

@media (max-width: 900px) {
  .stats { grid-template-columns: repeat(2, 1fr); }
  .review-grid, .login-grid { grid-template-columns: 1fr; }
}

@media (max-width: 520px) {
  .page { padding: 16px 16px 48px; }
  .topbar-inner { padding: 12px 16px; }
  .section-mint { padding: 24px 16px; }
  .stats { grid-template-columns: 1fr; }
  .data-table { display: block; overflow-x: auto; }
}
```

- [ ] **Step 2: Add the font to `App.razor`, and the layout and shared components**

In `src/WeeklySalesCoach/Components/App.razor`, inside `<head>` and **before** the existing `app.css` link, add:

```html
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Manrope:wght@400;500;600;700;800&display=swap" />
```

Leave the rest of `App.razor` (`<ReconnectModal />`, `<Routes />`, `blazor.web.js`) unchanged.

Append to `src/WeeklySalesCoach/Components/_Imports.razor`:

```razor
@using WeeklySalesCoach.Components.Shared
```

`src/WeeklySalesCoach/Components/Layout/MainLayout.razor` (replace the whole file):

```razor
@inherits LayoutComponentBase
@inject IOptions<DomainOptions> Domain

<header class="topbar">
    <div class="topbar-inner">
        <a class="brand" href="/"><span class="brand-mark">W</span><span>Weekly Sales <span class="accent">Coach</span></span></a>
        <AuthorizeView>
            <Authorized>
                <nav class="topbar-nav">
                    <span class="topbar-user">
                        @context.User.Identity?.Name
                        <small>@(context.User.IsInRole(nameof(UserRole.Manager)) ? "Manager" : Domain.Value.RepTerm)</small>
                    </span>
                    <form method="post" action="/auth/logout">
                        <AntiforgeryToken />
                        <button class="btn btn-outline btn-sm" type="submit">Sign out</button>
                    </form>
                </nav>
            </Authorized>
        </AuthorizeView>
    </div>
</header>

<main class="page">
    @Body
</main>

<footer class="footer">Weekly Sales Coach · AI drafts, managers decide · All demo data is fictional.</footer>

<div id="blazor-error-ui" data-nosnippet>
    An unhandled error has occurred.
    <a href="." class="reload">Reload</a>
    <span class="dismiss">🗙</span>
</div>
```

```bash
rm src/WeeklySalesCoach/Components/Layout/MainLayout.razor.css
```

`src/WeeklySalesCoach/Components/Shared/PillBadge.razor`:

```razor
<span class="pill"><span class="pill-dot"></span>@Text</span>

@code {
    [Parameter, EditorRequired] public string Text { get; set; } = "";
}
```

`src/WeeklySalesCoach/Components/Shared/StatCard.razor`:

```razor
<div class="stat">
    <div class="stat-value">@Value</div>
    <div class="stat-label">@Label</div>
</div>

@code {
    [Parameter, EditorRequired] public string Value { get; set; } = "";
    [Parameter, EditorRequired] public string Label { get; set; } = "";
}
```

`src/WeeklySalesCoach/Components/Shared/StatusChip.razor`:

```razor
<span class="chip @Css">@Label</span>

@code {
    [Parameter] public ReportStatus Status { get; set; }
    [Parameter] public bool IsEdited { get; set; }

    private string Label => Status switch
    {
        ReportStatus.Approved => "Approved",
        ReportStatus.Draft => IsEdited ? "Draft · edited" : "Draft ready",
        _ => "Not generated",
    };

    private string Css => Status switch
    {
        ReportStatus.Approved => "chip-green",
        ReportStatus.Draft => "chip-amber",
        _ => "chip-gray",
    };
}
```

`src/WeeklySalesCoach/Components/Shared/DemoBanner.razor`:

```razor
@inject IOptions<GeminiOptions> Gemini

@if (!Gemini.Value.IsConfigured)
{
    <div class="banner" role="status">
        <strong>Demo mode:</strong> showing pre-generated AI feedback. Configure a Gemini API key to generate live (see README).
    </div>
}
```

- [ ] **Step 3: Write the login page**

`src/WeeklySalesCoach/Components/Pages/Login.razor`:

```razor
@page "/login"
@attribute [AllowAnonymous]
@inject IOptions<DemoOptions> Demo
@inject IDbContextFactory<AppDbContext> DbFactory

<PageTitle>Sign in · Weekly Sales Coach</PageTitle>

<section class="hero">
    <PillBadge Text="AI-assisted sales coaching" />
    <h1>Weekly feedback your team<br /><span class="accent">can actually act on</span></h1>
    <p class="lead">AI drafts evidence-based coaching for every rep. Managers review, edit and approve before anyone sees it.</p>
</section>

<div class="login-grid">
    <form method="post" action="/auth/login" class="card form-card">
        <AntiforgeryToken />
        <h2>Sign in</h2>
        @if (Error == 1)
        {
            <p class="form-error">Invalid username or password.</p>
        }
        @if (Denied == 1)
        {
            <p class="form-error">This account doesn't have access to that page.</p>
        }
        <label>Username <input name="username" autocomplete="username" required /></label>
        <label>Password <input name="password" type="password" autocomplete="current-password" required /></label>
        <button class="btn btn-primary" type="submit">Sign in</button>
    </form>

    @if (Demo.Value.EnableQuickLogin)
    {
        <div class="card demo-card">
            <h2>Try the demo</h2>
            <p class="demo-disclaimer">Demo accounts — for demonstration purposes only.</p>
            @foreach (var account in accounts)
            {
                <form method="post" action="/auth/quick-login">
                    <AntiforgeryToken />
                    <input type="hidden" name="username" value="@account.Username" />
                    <button class="btn btn-outline btn-block" type="submit">
                        <span>Sign in as <strong>@account.DisplayName</strong></span>
                        <span class="muted small">@account.Label</span>
                    </button>
                </form>
            }
        </div>
    }
</div>

@code {
    private sealed record DemoAccount(string Username, string DisplayName, string Label);

    [SupplyParameterFromQuery(Name = "error")] public int? Error { get; set; }
    [SupplyParameterFromQuery(Name = "denied")] public int? Denied { get; set; }

    private List<DemoAccount> accounts = [];

    protected override async Task OnInitializedAsync()
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        var users = await db.Users.AsNoTracking().Include(u => u.Rep)
            .OrderBy(u => u.Role).ThenBy(u => u.DisplayName)
            .ToListAsync();
        accounts = users
            .Select(u => new DemoAccount(u.Username, u.DisplayName, u.Role == UserRole.Manager ? "Manager" : u.Rep?.Title ?? ""))
            .ToList();
    }
}
```

- [ ] **Step 4: Verify in the browser**

Run (background): `dotnet run --project src/WeeklySalesCoach --urls http://localhost:5080`
Then in the built-in browser:
1. Open `http://localhost:5080/`. Expected: redirected to `/login`; Manrope font; centered hero with green pill badge and a green second line; two cards (sign-in form and demo accounts with the amber disclaimer "Demo accounts — for demonstration purposes only.").
2. Sign in with username `ana`, password `wrong`. Expected: "Invalid username or password."
3. Click "Sign in as Roberto Díaz". Expected: redirected to `/dashboard` (a "not found" page is expected until Task 11), with the top bar showing "Roberto Díaz / Manager" and a Sign out button.
4. Click "Sign out". Expected: back on `/login`.
5. Resize to 375 px wide. Expected: cards stack in a single column with no horizontal scroll.

Stop the app afterwards.

- [ ] **Step 5: Checkpoint**

Run: `dotnet test`
Expected: all tests pass.

---

### Task 11: Team dashboard page

**Files:**
- Create: `src/WeeklySalesCoach/Components/Pages/Dashboard.razor`

**Interfaces:**
- Consumes: `TeamDashboardService.GetAsync`, `CoachingService.GetCurrentWeekAsync` / `GenerateAsync` (Tasks 7–8), `AiErrorMessages.For` (Task 6), `Fmt`, `Calc`, shared components (Task 10)
- Produces: route `/dashboard` (Manager only, InteractiveServer), with links to `/review/{RepId}`

- [ ] **Step 1: Write the page**

`src/WeeklySalesCoach/Components/Pages/Dashboard.razor`:

```razor
@page "/dashboard"
@attribute [Authorize(Roles = "Manager")]
@rendermode InteractiveServer
@inject TeamDashboardService DashboardService
@inject CoachingService Coaching
@inject IOptions<DomainOptions> Domain

<PageTitle>Team · Weekly Sales Coach</PageTitle>

<DemoBanner />

@if (dashboard is null)
{
    <p class="muted">Loading…</p>
}
else
{
    var k = dashboard.Kpis;
    var terms = Domain.Value;

    <section class="hero">
        <PillBadge Text="@($"Week of {dashboard.Week.StartDate:MMM d} – {dashboard.Week.EndDate:MMM d, yyyy}")" />
        <h1>Your team's week,<br /><span class="accent">coached by AI, approved by you</span></h1>
        <p class="lead">Generate a draft for each @terms.RepTerm.ToLowerInvariant(), check the evidence, edit anything, then share it.</p>
        <div class="hero-actions">
            <button class="btn btn-primary" disabled="@busy" @onclick="GenerateTeamAsync">
                @(busy ? progress : "Generate feedback for whole team")
            </button>
        </div>
        <p class="hint">Skips reports that are already approved or that you edited by hand.</p>
        @if (error is not null)
        {
            <p class="form-error">@error</p>
        }
    </section>

    <section class="stats">
        <StatCard Value="@Fmt.CompactMoney(k.WonVolume)" Label="@($"{terms.WonStage} volume · {Fmt.Percent(Calc.Attainment(k.WonVolume, k.TeamQuota))} of team quota")" />
        <StatCard Value="@k.DealsWon.ToString(CultureInfo.InvariantCulture)" Label="@($"{terms.DealTerm}s {terms.WonStage.ToLowerInvariant()}")" />
        <StatCard Value="@k.Applications.ToString(CultureInfo.InvariantCulture)" Label="Applications submitted" />
        <StatCard Value="@($"{k.AvgLeadResponseHours:0.#}h")" Label="Avg. lead response time" />
    </section>

    <section class="section-mint">
        <h2>@(terms.RepTerm)s</h2>
        <div class="card-grid">
            @foreach (var r in dashboard.Reps)
            {
                <a class="card rep-card" href="@($"/review/{r.RepId}")">
                    <div class="rep-card-head">
                        <span class="avatar">@r.Initials</span>
                        <StatusChip Status="r.Status" IsEdited="r.IsEdited" />
                    </div>
                    <h3>@r.Name</h3>
                    <p class="muted">@r.Title</p>
                    <div class="progress"><div class="progress-bar" style="@($"width:{BarWidth(r.AttainmentPercent)}")"></div></div>
                    <p class="rep-card-stat"><strong>@Fmt.CompactMoney(r.WonVolume)</strong> of @Fmt.CompactMoney(r.Quota) · @Fmt.Percent(r.AttainmentPercent)</p>
                </a>
            }
        </div>
    </section>
}

@code {
    private TeamDashboard? dashboard;
    private bool busy;
    private string progress = "";
    private string? error;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        var week = await Coaching.GetCurrentWeekAsync();
        dashboard = await DashboardService.GetAsync(week.Id);
    }

    private async Task GenerateTeamAsync()
    {
        if (dashboard is null || busy) return;
        busy = true;
        error = null;
        var weekId = dashboard.Week.Id;
        var targets = dashboard.Reps.Where(r => r.Status != ReportStatus.Approved && !r.IsEdited).ToList();
        try
        {
            // One at a time: keeps us well within free-tier rate limits and saves each report as it completes.
            for (var i = 0; i < targets.Count; i++)
            {
                progress = $"Generating {i + 1} of {targets.Count}: {targets[i].Name}…";
                StateHasChanged();
                await Coaching.GenerateAsync(targets[i].RepId, weekId);
                await LoadAsync();
            }
        }
        catch (Exception ex) when (ex is AiUnavailableException or InvalidCoachingContentException)
        {
            error = AiErrorMessages.For(ex) + " Reports generated so far were saved.";
            await LoadAsync();
        }
        finally
        {
            busy = false;
            progress = "";
        }
    }

    private static string BarWidth(double percent) =>
        Math.Clamp(percent, 0, 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
}
```

- [ ] **Step 2: Verify in the browser**

Delete any existing local DB so the seed is fresh: `rm -f src/WeeklySalesCoach/weeklysalescoach.db`
Run (background): `dotnet run --project src/WeeklySalesCoach --urls http://localhost:5080` (no API key configured).
1. Sign in as Roberto (quick login). Expected at `/dashboard`: the amber demo-mode banner; pill "Week of Sep 14 – Sep 18, 2026"; stats `$697K` / "Funded volume · 84% of team quota" (697,000 / 830,000), `8` "Loans funded", `22` "Applications submitted", `5.1h`; a mint section "Loan Officers" with 5 cards (Ana 124.8%, Carlos 63.3%, Laura 58.7%, Miguel 70%, Sofía 77.5%), all "Not generated".
2. Click "Generate feedback for whole team". Expected: the button shows "Generating 1 of 5: …" through 5, then all cards show "Draft ready".
3. Sign out, sign in as Ana (quick login), then open `/dashboard` directly. Expected: redirected to `/login?denied=1…` with the "doesn't have access" message.

Stop the app afterwards.

- [ ] **Step 3: Checkpoint**

Run: `dotnet test`
Expected: all tests pass.

---

### Task 12: Review page (evidence, editing, approval)

**Files:**
- Create: `src/WeeklySalesCoach/Components/Shared/EvidencePanel.razor`, `src/WeeklySalesCoach/Components/Shared/FeedbackItemsEditor.razor`, `src/WeeklySalesCoach/Components/Shared/FeedbackView.razor`, `src/WeeklySalesCoach/Components/Pages/Review.razor`

**Interfaces:**
- Consumes: `CoachingContextBuilder.BuildAsync`, `CoachingService` (all methods), `CoachingContentParser.Serialize`, `AiErrorMessages.For`, `ClaimsPrincipal.GetUserId()`, `Fmt`, shared components
- Produces: route `/review/{RepId:int}` (Manager only, InteractiveServer); components `<EvidencePanel Context DealTerm>`, `<FeedbackItemsEditor Heading Items Disabled>`, `<FeedbackView Content>` (FeedbackView is reused in Task 13)

- [ ] **Step 1: Write the shared components**

`src/WeeklySalesCoach/Components/Shared/EvidencePanel.razor`:

```razor
<section class="card evidence-panel">
    <h2>This week's evidence</h2>
    <p class="muted small">
        Exactly what the AI sees. Quota attainment:
        <strong>@Fmt.Percent(Context.QuotaAttainmentPercent)</strong> of @Fmt.Money(Context.Rep.WeeklyQuota).
    </p>

    <table class="data-table">
        <thead><tr><th>Metric</th><th>This week</th><th>Last week</th><th>Team avg</th></tr></thead>
        <tbody>
            @foreach (var row in Rows())
            {
                <tr><td>@row.Label</td><td>@row.Current</td><td>@row.Previous</td><td>@row.Team</td></tr>
            }
        </tbody>
    </table>

    <h3 class="section-label">Pipeline</h3>
    <table class="data-table">
        <thead><tr><th>Client</th><th>Amount</th><th>Stage</th><th>In stage</th><th>Last contact</th></tr></thead>
        <tbody>
            @foreach (var d in Context.Deals)
            {
                <tr>
                    <td>
                        <strong>@d.Client</strong> <span class="muted">· @d.Product</span>
                        <span class="deal-notes">@d.Notes@(d.LostReason is null ? "" : $" Lost: {d.LostReason}")</span>
                    </td>
                    <td>@Fmt.Money(d.Amount)</td>
                    <td>@d.Stage</td>
                    <td>@d.DaysInStage d</td>
                    <td class="@(d.DaysSinceLastContact > 5 ? "warn" : "")">@d.DaysSinceLastContact d ago</td>
                </tr>
            }
        </tbody>
    </table>

    @if (Context.ManagerNote is not null)
    {
        <p class="note"><strong>Manager note:</strong> @Context.ManagerNote</p>
    }
</section>

@code {
    [Parameter, EditorRequired] public CoachingContext Context { get; set; } = default!;
    [Parameter] public string DealTerm { get; set; } = "Deal";

    private IEnumerable<(string Label, string Current, string Previous, string Team)> Rows()
    {
        var c = Context.Current;
        var p = Context.Previous;
        var t = Context.TeamAverage;
        const string none = "—";

        yield return ("Calls made / connected", $"{c.CallsMade:0} / {c.CallsConnected:0}", p is null ? none : $"{p.CallsMade:0} / {p.CallsConnected:0}", $"{t.CallsMade:0.#} / {t.CallsConnected:0.#}");
        yield return ("Emails sent / replied", $"{c.EmailsSent:0} / {c.EmailsReplied:0}", p is null ? none : $"{p.EmailsSent:0} / {p.EmailsReplied:0}", $"{t.EmailsSent:0.#} / {t.EmailsReplied:0.#}");
        yield return ("New leads", $"{c.NewLeads:0}", p is null ? none : $"{p.NewLeads:0}", $"{t.NewLeads:0.#}");
        yield return ("Applications", $"{c.ApplicationsSubmitted:0}", p is null ? none : $"{p.ApplicationsSubmitted:0}", $"{t.ApplicationsSubmitted:0.#}");
        yield return ($"{DealTerm}s won / lost", $"{c.DealsWon:0} / {c.DealsLost:0}", p is null ? none : $"{p.DealsWon:0} / {p.DealsLost:0}", $"{t.DealsWon:0.#} / {t.DealsLost:0.#}");
        yield return ("Won volume", Fmt.Money(c.WonVolume), p is null ? none : Fmt.Money(p.WonVolume), Fmt.Money(t.WonVolume));
        yield return ("Avg. lead response", $"{c.AvgLeadResponseHours:0.#}h", p is null ? none : $"{p.AvgLeadResponseHours:0.#}h", $"{t.AvgLeadResponseHours:0.#}h");
    }
}
```

`src/WeeklySalesCoach/Components/Shared/FeedbackItemsEditor.razor`:

```razor
<h3 class="section-label">@Heading</h3>
@foreach (var item in Items)
{
    <div class="edit-item" @key="item">
        <input class="edit-title" @bind="item.Title" aria-label="@($"{Heading} title")" disabled="@Disabled" />
        <textarea rows="2" @bind="item.Detail" aria-label="@($"{Heading} detail")" disabled="@Disabled"></textarea>
        <input class="edit-evidence" @bind="item.Evidence" aria-label="@($"{Heading} evidence")" disabled="@Disabled" />
        <button type="button" class="link-btn" disabled="@(Disabled || Items.Count <= 1)" @onclick="() => Items.Remove(item)">Remove</button>
    </div>
}

@code {
    [Parameter, EditorRequired] public string Heading { get; set; } = "";
    [Parameter, EditorRequired] public List<FeedbackItem> Items { get; set; } = [];
    [Parameter] public bool Disabled { get; set; }
}
```

`src/WeeklySalesCoach/Components/Shared/FeedbackView.razor`:

```razor
<div class="feedback">
    <p class="feedback-summary">@Content.Summary</p>

    <h3 class="section-label">What went well</h3>
    <ul class="feedback-list list-strengths">
        @foreach (var item in Content.Strengths)
        {
            <li><strong>@item.Title</strong><p>@item.Detail</p><p class="evidence">Evidence: @item.Evidence</p></li>
        }
    </ul>

    <h3 class="section-label">Where to improve</h3>
    <ul class="feedback-list list-improvements">
        @foreach (var item in Content.Improvements)
        {
            <li><strong>@item.Title</strong><p>@item.Detail</p><p class="evidence">Evidence: @item.Evidence</p></li>
        }
    </ul>

    <h3 class="section-label">Your focus for next week</h3>
    <ul class="feedback-list list-actions">
        @foreach (var action in Content.Actions)
        {
            <li>
                <strong>@action.Action</strong>
                <p>@action.Why</p>
                @if (!string.IsNullOrWhiteSpace(action.SuccessSignal))
                {
                    <p class="evidence">Success looks like: @action.SuccessSignal</p>
                }
            </li>
        }
    </ul>

    @if (Content.DataGaps.Count > 0)
    {
        <h3 class="section-label">Not enough data to assess</h3>
        <ul class="muted small">
            @foreach (var gap in Content.DataGaps)
            {
                <li>@gap</li>
            }
        </ul>
    }
</div>

@code {
    [Parameter, EditorRequired] public CoachingContent Content { get; set; } = default!;
}
```

- [ ] **Step 2: Write the review page**

`src/WeeklySalesCoach/Components/Pages/Review.razor`:

```razor
@page "/review/{RepId:int}"
@attribute [Authorize(Roles = "Manager")]
@rendermode InteractiveServer
@inject CoachingService Coaching
@inject CoachingContextBuilder ContextBuilder
@inject IOptions<DomainOptions> Domain
@inject AuthenticationStateProvider AuthState

<PageTitle>Review · Weekly Sales Coach</PageTitle>

<DemoBanner />
<a class="back-link" href="/dashboard">← Back to team</a>

@if (loadError is not null)
{
    <p class="form-error">@loadError</p>
}
else if (context is null || report is null)
{
    <p class="muted">Loading…</p>
}
else
{
    <section class="hero hero--left">
        <PillBadge Text="@($"{context.Rep.Title} · {Tenure(context.Rep.TenureMonths)}")" />
        <h1>@context.Rep.Name<br /><span class="accent">week of @context.WeekStart.ToString("MMM d", CultureInfo.InvariantCulture)</span></h1>
        <p class="lead">Goal: @context.Rep.PersonalGoal · Prefers @context.Rep.FeedbackStyle.ToString().ToLowerInvariant() feedback</p>
    </section>

    <div class="review-grid">
        <EvidencePanel Context="context" DealTerm="@Domain.Value.DealTerm" />

        <section class="card review-panel">
            <div class="panel-head">
                <h2>Coaching draft</h2>
                <StatusChip Status="report.Status" IsEdited="report.IsEdited" />
            </div>

            @if (error is not null)
            {
                <p class="form-error">@error</p>
            }
            @if (info is not null)
            {
                <p class="form-info">@info</p>
            }

            @if (report.Status == ReportStatus.Approved && content is not null)
            {
                <FeedbackView Content="content" />
                <p class="muted small">Approved @report.ApprovedAtUtc?.ToLocalTime().ToString("MMM d, h:mm tt", CultureInfo.InvariantCulture) · visible to @context.Rep.Name</p>
            }
            else
            {
                <label class="field">
                    Manager note <span class="muted small">Optional context for the AI, e.g. time off or a special assignment.</span>
                    <textarea rows="3" maxlength="@CoachingService.ManagerNoteMaxLength" @bind="managerNote" disabled="@busy"></textarea>
                </label>

                @if (confirmRegenerate)
                {
                    <div class="confirm">
                        <p>This will replace your edits. Continue?</p>
                        <button class="btn btn-primary btn-sm" disabled="@busy" @onclick="() => GenerateAsync(confirmed: true)">Yes, regenerate</button>
                        <button class="btn btn-outline btn-sm" @onclick="() => confirmRegenerate = false">Cancel</button>
                    </div>
                }
                else
                {
                    <button class="btn @(content is null ? "btn-primary" : "btn-outline")" disabled="@busy" @onclick="() => GenerateAsync(confirmed: false)">
                        @(busy ? "Working…" : content is null ? "Generate draft" : "Save note & regenerate")
                    </button>
                }

                @if (content is not null)
                {
                    <label class="field">Summary<textarea rows="3" @bind="content.Summary" disabled="@busy"></textarea></label>
                    <FeedbackItemsEditor Heading="Strengths" Items="content.Strengths" Disabled="busy" />
                    <FeedbackItemsEditor Heading="Areas to improve" Items="content.Improvements" Disabled="busy" />

                    <h3 class="section-label">Actions for next week</h3>
                    @foreach (var action in content.Actions)
                    {
                        <div class="edit-item" @key="action">
                            <input class="edit-title" @bind="action.Action" aria-label="Action" disabled="@busy" />
                            <textarea rows="2" @bind="action.Why" aria-label="Why" disabled="@busy"></textarea>
                            <input class="edit-evidence" @bind="action.SuccessSignal" aria-label="Success signal" disabled="@busy" />
                            <button type="button" class="link-btn" disabled="@(busy || content.Actions.Count <= 1)" @onclick="() => content.Actions.Remove(action)">Remove</button>
                        </div>
                    }

                    <p class="muted small">Draft source: @(report.Source == ReportSource.Live ? $"live AI ({report.ModelName})" : "pre-generated demo")</p>

                    <div class="panel-actions">
                        <button class="btn btn-outline" disabled="@busy" @onclick="SaveEditsAsync">Save edits</button>
                        <button class="btn btn-primary" disabled="@busy" @onclick="ApproveAsync">Approve &amp; share</button>
                    </div>
                }
            }
        </section>
    </div>
}

@code {
    [Parameter] public int RepId { get; set; }

    private CoachingContext? context;
    private CoachingReport? report;
    private CoachingContent? content;
    private string? managerNote;
    private bool busy;
    private bool confirmRegenerate;
    private string? error;
    private string? info;
    private string? loadError;

    private bool HasUnsavedEdits =>
        content is not null && report?.ContentJson is not null && CoachingContentParser.Serialize(content) != report.ContentJson;

    protected override async Task OnParametersSetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        try
        {
            var week = await Coaching.GetCurrentWeekAsync();
            // Build the context first: it throws for an unknown rep before we create any report row.
            context = await ContextBuilder.BuildAsync(RepId, week.Id);
            report = await Coaching.GetOrCreateReportAsync(RepId, week.Id);
            content = CoachingService.ReadContent(report);
            managerNote = report.ManagerNote;
        }
        catch (InvalidOperationException)
        {
            loadError = "This team member could not be found.";
        }
    }

    private async Task GenerateAsync(bool confirmed)
    {
        if (report is null) return;
        if ((report.IsEdited || HasUnsavedEdits) && !confirmed)
        {
            confirmRegenerate = true;
            return;
        }

        confirmRegenerate = false;
        busy = true;
        error = info = null;
        try
        {
            await Coaching.SaveManagerNoteAsync(RepId, report.WeekId, managerNote);
            await Coaching.GenerateAsync(RepId, report.WeekId);
            await LoadAsync();
            info = "Draft ready. Review it, edit anything you like, then approve.";
        }
        catch (Exception ex) when (ex is AiUnavailableException or InvalidCoachingContentException)
        {
            error = AiErrorMessages.For(ex);
            await LoadAsync();
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
        }
        finally
        {
            busy = false;
        }
    }

    private async Task SaveEditsAsync()
    {
        if (report is null || content is null) return;
        busy = true;
        error = info = null;
        try
        {
            await Coaching.SaveEditsAsync(report.Id, content);
            await LoadAsync();
            info = "Edits saved.";
        }
        catch (Exception ex) when (ex is InvalidCoachingContentException or InvalidOperationException)
        {
            error = ex.Message;
        }
        finally
        {
            busy = false;
        }
    }

    private async Task ApproveAsync()
    {
        if (report is null || content is null || context is null) return;
        busy = true;
        error = info = null;
        try
        {
            var user = (await AuthState.GetAuthenticationStateAsync()).User;
            var userId = user.GetUserId() ?? throw new InvalidOperationException("You are not signed in.");
            await Coaching.SaveEditsAsync(report.Id, content); // no-op when nothing changed
            await Coaching.ApproveAsync(report.Id, userId);
            await LoadAsync();
            info = $"Approved. {context.Rep.Name} can now see this feedback.";
        }
        catch (Exception ex) when (ex is InvalidCoachingContentException or InvalidOperationException)
        {
            error = ex.Message;
        }
        finally
        {
            busy = false;
        }
    }

    private static string Tenure(int months) =>
        months < 12 ? $"{months} month{(months == 1 ? "" : "s")}" : $"{months / 12} yr{(months / 12 == 1 ? "" : "s")}";
}
```

- [ ] **Step 3: Verify in the browser**

`rm -f src/WeeklySalesCoach/weeklysalescoach.db`, then run (background): `dotnet run --project src/WeeklySalesCoach --urls http://localhost:5080`.
1. Sign in as Roberto → click Carlos's card. Expected `/review/{id}`: pill "Loan Officer · 1 yr"; left panel with the metrics table (This week 72 / 21 calls, Last week 65 / 19, Team avg 41.4 / 14.6), a pipeline table where Summit Auto Repair shows "10 d" in stage and "9 d ago" in red; right panel "Not generated" with a "Generate draft" button.
2. Click "Generate draft". Expected: an editable draft (summary, strengths, improvements, actions) mentioning Documents Pending; chip "Draft ready"; source "pre-generated demo".
3. Edit the summary text and click "Save edits". Expected: "Edits saved." and chip "Draft · edited".
4. Click "Save note & regenerate". Expected: the "This will replace your edits. Continue?" confirmation. Click Cancel.
5. Click "Approve & share". Expected: a read-only view with "Approved … · visible to Carlos Mendoza", chip "Approved".
6. Open Miguel's review. Expected: the manager note "Out 2 days this week for compliance training." is pre-filled and shown in the evidence panel.
7. Open `/review/999`. Expected: "This team member could not be found."

Stop the app afterwards.

- [ ] **Step 4: Checkpoint**

Run: `dotnet test`
Expected: all tests pass.

---

### Task 13: Rep's "My feedback" page

**Files:**
- Create: `src/WeeklySalesCoach/Components/Pages/MyFeedback.razor`

**Interfaces:**
- Consumes: `CoachingService.GetCurrentWeekAsync` / `GetApprovedForRepAsync` / `ReadContent`, `ClaimsPrincipal.GetRepId()`, `FeedbackView`, `PillBadge`
- Produces: route `/my-feedback` (Rep only, static SSR)

- [ ] **Step 1: Write the page**

`src/WeeklySalesCoach/Components/Pages/MyFeedback.razor`:

```razor
@page "/my-feedback"
@attribute [Authorize(Roles = "Rep")]
@inject CoachingService Coaching

<PageTitle>My feedback · Weekly Sales Coach</PageTitle>

@if (week is not null)
{
    <section class="hero">
        <PillBadge Text="@($"Week of {week.StartDate:MMM d} – {week.EndDate:MMM d, yyyy}")" />
        <h1>Your week,<br /><span class="accent">@firstName</span></h1>
        <p class="lead">Coaching feedback reviewed and shared by your manager.</p>
    </section>

    <section class="feedback-page">
        @if (content is null)
        {
            <div class="card empty-state">
                <h2>Nothing here yet</h2>
                <p class="muted">Your manager hasn't shared this week's feedback yet.</p>
            </div>
        }
        else
        {
            <div class="card"><FeedbackView Content="content" /></div>
        }
    </section>
}

@code {
    [CascadingParameter] private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    private Week? week;
    private CoachingContent? content;
    private string firstName = "";

    protected override async Task OnInitializedAsync()
    {
        var user = (await AuthStateTask).User;
        firstName = (user.Identity?.Name ?? "").Split(' ')[0];
        week = await Coaching.GetCurrentWeekAsync();

        // The rep id comes from the auth cookie, so a rep can only ever load their own approved report.
        if (user.GetRepId() is int repId)
        {
            var report = await Coaching.GetApprovedForRepAsync(repId, week.Id);
            content = report is null ? null : CoachingService.ReadContent(report);
        }
    }
}
```

- [ ] **Step 2: Verify in the browser**

Using the DB from Task 12 (Carlos approved), run (background): `dotnet run --project src/WeeklySalesCoach --urls http://localhost:5080`.
1. Sign in as Carlos (quick login). Expected: redirected to `/my-feedback`; hero "Your week, Carlos"; the approved feedback, including the manager's edited summary from Task 12.
2. Sign out, sign in as Ana. Expected: "Nothing here yet — Your manager hasn't shared this week's feedback yet." (her report is not approved).
3. As Ana, open `/review/1`. Expected: access denied (redirect to login with the "doesn't have access" message).
4. Sign in as Roberto, open `/my-feedback`. Expected: access denied (managers are not reps).

Stop the app afterwards.

- [ ] **Step 3: Checkpoint**

Run: `dotnet test`
Expected: all tests pass.

---

### Task 14: Live AI setup and real pre-generated demo feedback

**Files:**
- Create: `src/WeeklySalesCoach/Services/AI/DemoFeedbackExporter.cs`
- Modify: `src/WeeklySalesCoach/Program.cs` (argument filtering, DI registration, export block)
- Regenerate: `src/WeeklySalesCoach/Data/Seed/demo-feedback.json`
- Test: `tests/WeeklySalesCoach.Tests/DemoFeedbackExporterTests.cs`

**Interfaces:**
- Consumes: `CoachingContextBuilder`, `IFeedbackGenerator`, `GeminiFeedbackGenerator`, `CoachingContentParser`, `DemoFeedbackGenerator`
- Produces: `DemoFeedbackExporter(IDbContextFactory<AppDbContext>, CoachingContextBuilder, ILogger<DemoFeedbackExporter>)` with `Task<int> ExportAsync(IFeedbackGenerator generator, string outputPath, CancellationToken ct = default)`; CLI flag `--export-demo-feedback`

- [ ] **Step 1: Write the failing test**

`tests/WeeklySalesCoach.Tests/DemoFeedbackExporterTests.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WeeklySalesCoach.Configuration;
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Services.AI;
using WeeklySalesCoach.Services.Coaching;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class DemoFeedbackExporterTests
{
    [Fact]
    public async Task Exports_every_rep_in_a_format_the_demo_generator_reads()
    {
        using var t = new TestDb();
        await using (var db = t.CreateContext())
        {
            await SeedData.SeedAsync(db, new PasswordHasher<AppUser>());
        }
        var builder = new CoachingContextBuilder(t.Factory, Options.Create(new DomainOptions()));
        var generator = new FakeFeedbackGenerator(Enumerable.Repeat<Func<string>>(() => TestContent.ValidJson, 5).ToArray());
        var exporter = new DemoFeedbackExporter(t.Factory, builder, NullLogger<DemoFeedbackExporter>.Instance);
        var path = Path.Combine(Path.GetTempPath(), $"demo-{Guid.NewGuid():N}.json");
        try
        {
            var count = await exporter.ExportAsync(generator, path);

            Assert.Equal(5, count);
            var raw = await new DemoFeedbackGenerator(path).GenerateAsync(TestContent.SampleContext("Sofía Herrera"));
            Assert.Equal("Solid week.", CoachingContentParser.Parse(raw).Summary);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~DemoFeedbackExporterTests"`
Expected: build FAILS: `The type or namespace name 'DemoFeedbackExporter' could not be found`.

- [ ] **Step 3: Implement the exporter and wire the CLI flag**

`src/WeeklySalesCoach/Services/AI/DemoFeedbackExporter.cs`:

```csharp
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Services.Coaching;

namespace WeeklySalesCoach.Services.AI;

/// <summary>Dev-only: runs the real AI for every rep and saves the results as the demo-mode feedback file.</summary>
public class DemoFeedbackExporter(
    IDbContextFactory<AppDbContext> dbFactory,
    CoachingContextBuilder contextBuilder,
    ILogger<DemoFeedbackExporter> logger)
{
    public async Task<int> ExportAsync(IFeedbackGenerator generator, string outputPath, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var week = await db.Weeks.AsNoTracking().SingleAsync(w => w.IsCurrent, ct);
        var reps = await db.Reps.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);

        var result = new SortedDictionary<string, CoachingContent>(StringComparer.Ordinal);
        foreach (var rep in reps)
        {
            var context = await contextBuilder.BuildAsync(rep.Id, week.Id, ct);
            CoachingContent content;
            try
            {
                content = CoachingContentParser.Parse(await generator.GenerateAsync(context, ct));
            }
            catch (InvalidCoachingContentException)
            {
                content = CoachingContentParser.Parse(await generator.GenerateAsync(context, ct));
            }
            result[rep.Name] = content;
            logger.LogInformation("Generated demo feedback for {Rep}", rep.Name);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        await File.WriteAllTextAsync(outputPath, json, ct);
        return result.Count;
    }
}
```

In `src/WeeklySalesCoach/Program.cs`:

Replace `var builder = WebApplication.CreateBuilder(args);` with:

```csharp
const string ExportDemoFlag = "--export-demo-feedback";
var exportDemo = args.Contains(ExportDemoFlag);
var builder = WebApplication.CreateBuilder(args.Where(a => a != ExportDemoFlag).ToArray());
```

After `builder.Services.AddScoped<UserAuthService>();` add:

```csharp
builder.Services.AddScoped<DemoFeedbackExporter>();
```

After `await DatabaseInitializer.InitializeAsync(app.Services);` add:

```csharp
if (exportDemo)
{
    await using var scope = app.Services.CreateAsyncScope();
    if (!scope.ServiceProvider.GetRequiredService<IOptions<GeminiOptions>>().Value.IsConfigured)
    {
        Console.Error.WriteLine("Gemini:ApiKey is not configured. See README → Live AI.");
        return;
    }

    var path = Path.Combine(app.Environment.ContentRootPath,
        scope.ServiceProvider.GetRequiredService<IOptions<DemoOptions>>().Value.DemoFeedbackPath);
    var count = await scope.ServiceProvider.GetRequiredService<DemoFeedbackExporter>()
        .ExportAsync(scope.ServiceProvider.GetRequiredService<GeminiFeedbackGenerator>(), path);
    Console.WriteLine($"Wrote live AI demo feedback for {count} reps to {path}");
    return;
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~DemoFeedbackExporterTests"`
Expected: PASS (1 test).

- [ ] **Step 5: Configure the API key (the human does this)**

The agent runs:

```bash
dotnet user-secrets init --project src/WeeklySalesCoach
```

Then **ask the author** to:
1. Create a key at https://aistudio.google.com/apikey.
2. Confirm the current Flash model id in AI Studio. If it is not `gemini-2.5-flash`, update `Gemini:Model` in `appsettings.json`.
3. Run this command themselves (the agent must never type or see the key):

```bash
dotnet user-secrets set "Gemini:ApiKey" "PASTE_YOUR_KEY_HERE" --project src/WeeklySalesCoach
```

- [ ] **Step 6: Regenerate the demo feedback with live AI and review it**

Run: `dotnet run --project src/WeeklySalesCoach -- --export-demo-feedback`
Expected: `Wrote live AI demo feedback for 5 reps to …/Data/Seed/demo-feedback.json`.

Read the new `demo-feedback.json` and check it against the guardrails. For each rep:
- The evidence cites real seed facts.
- No other rep is named.
- There are no HR recommendations.
- Miguel's feedback reflects the training note.
- Laura's feedback addresses "Close larger loans".
- Sofía's tone is encouraging.

If an entry violates a rule, adjust `CoachingPrompt.SystemInstructions` and re-run this step. Show the author the result.

- [ ] **Step 7: Verify live generation in the app**

`rm -f src/WeeklySalesCoach/weeklysalescoach.db`, then run (background): `dotnet run --project src/WeeklySalesCoach --urls http://localhost:5080`.
Sign in as Roberto → Laura → "Generate draft". Expected: no demo banner; the draft source shows "live AI (<model>)"; the content references Oakridge Pharmacy and her goal.

Stop the app afterwards.

- [ ] **Step 8: Checkpoint**

Run: `dotnet test`
Expected: all tests pass, including `Committed_demo_feedback_covers_every_seeded_rep` against the regenerated file.

---

### Task 15: README, screenshots and final verification

**Files:**
- Create: `README.md`, `docs/screenshots/login.png`, `docs/screenshots/dashboard.png`, `docs/screenshots/review.png`, `docs/screenshots/my-feedback.png`

**Interfaces:**
- Consumes: the finished app
- Produces: the submission documentation

- [ ] **Step 1: Capture screenshots**

Remove the API key from the running environment only if you want demo-mode shots; otherwise keep live mode. `rm -f src/WeeklySalesCoach/weeklysalescoach.db`, then run (background): `dotnet run --project src/WeeklySalesCoach --urls http://localhost:5080`.

Use the Playwright browser tools (`browser_navigate`, `browser_click`, `browser_take_screenshot` with `filename`) at a 1440×900 viewport to save:
1. `docs/screenshots/login.png`: the `/login` page.
2. `docs/screenshots/dashboard.png`: `/dashboard` as Roberto after "Generate feedback for whole team".
3. `docs/screenshots/review.png`: Carlos's review with an edited draft.
4. `docs/screenshots/my-feedback.png`: `/my-feedback` as Carlos after approval.

Stop the app afterwards.

- [ ] **Step 2: Write the README**

`README.md`:

````markdown
# Weekly Sales Coach

**AI drafts weekly, evidence-based coaching for every sales rep. The manager reviews, edits and approves it before the rep sees it.**

Built as a take-home exercise for nuDesk. The demo models a loan-origination team (loan officers), because nuDesk works in financial services. The engine itself is industry-agnostic.

![Team dashboard](docs/screenshots/dashboard.png)

## The problem

Reps rarely get useful feedback. When they do, it comes late (quarterly reviews), it's vague ("keep it up") and it's subjective. Managers know weekly coaching works, but writing specific feedback for every rep every week takes hours, so it doesn't happen.

## What it does

1. **The manager opens the team dashboard** and sees KPIs and one card per rep.
2. **"Generate feedback"**: for each rep, the AI reads that rep's week (activity metrics, the previous week, the team average, every deal in their pipeline, and an optional manager note) and drafts coaching feedback:
   - what went well
   - where to improve
   - 1–3 concrete actions for next week

   **Every point cites evidence** ("Summit Auto Repair: 10 days in Documents Pending").
3. **The manager reviews** the draft next to the exact evidence the AI saw, edits anything, adds context (e.g. "out 2 days for training") and regenerates if needed.
4. **Approve & share**: only then can the rep sign in and see it.

Each rep gets a different message because each one gets different data, trends, goals and preferred tone. The demo team shows this:

| Rep | Situation | What the coach picks up |
|---|---|---|
| Ana | Top performer, 125% of quota | Recognizes the wins; flags small leads waiting 8+ days |
| Carlos | Most activity, low conversion | Applications stuck in *Documents Pending* |
| Sofía | New hire, improving | Encouraging tone; celebrates her first funded loan |
| Miguel | Numbers dipped | Manager note: out 2 days for training, so he isn't penalized |
| Laura | Steady, small loans | Her personal goal: close larger loans |

![Review screen](docs/screenshots/review.png)

## Quick start (no setup needed)

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
cd src/WeeklySalesCoach
dotnet run
```

Open the URL printed in the console. On the login page, use the **demo quick-login buttons** (demo accounts, for demonstration only; the password for all of them is `demo123`):

| Account | Role |
|---|---|
| `roberto` | Sales Manager |
| `ana`, `carlos`, `sofia`, `miguel`, `laura` | Loan Officers |

Without an API key, the app runs in **demo mode**: it serves feedback that was **pre-generated by the same Gemini integration**, and a banner says so. The whole flow (generate → edit → approve → rep view) works the same way.

## Live AI (optional)

1. Get a free key at [Google AI Studio](https://aistudio.google.com/apikey).
2. Store it outside the repo:

   ```bash
   dotnet user-secrets set "Gemini:ApiKey" "YOUR_KEY" --project src/WeeklySalesCoach
   ```

   Or set the environment variable `Gemini__ApiKey`.
3. Run the app again. The banner disappears and drafts are generated live.

To refresh the demo file with live output: `dotnet run --project src/WeeklySalesCoach -- --export-demo-feedback`.

## How the AI is used, and kept in check

```
rep's week (metrics, previous week, team average, deals, manager note)
        │  only this rep's data + aggregate team average — no other names, no sensitive attributes
        ▼
system instructions (coach role + must / must-not rules)
        ▼
Gemini (Flash) in structured-output mode with a required JSON schema
        ▼
validation: 1–3 items per section, every point has evidence → one retry if invalid
        ▼
Draft → manager edits → Approve → visible to the rep
```

**The AI must** base every statement on the data, cite evidence, balance strengths and improvements, adapt its tone to tenure and preference, talk about behaviors (not personality) and respect the manager's note.

**The AI must not** invent data, comment on personal matters, recommend HR actions (firing, discipline, pay), name other reps, or guess. When data is missing, it says so in `dataGaps`.

## Responsible-AI choices

- **Coaching, not scoring.** There are no rankings or grades of people.
- **Human in the loop.** Nothing reaches a rep without manager approval.
- **Privacy by design.** No sensitive attributes are collected. The AI never sees other reps' data.
- **Transparency.** The manager sees exactly the evidence the AI saw, and every claim has a source.
- **Secrets stay server-side.** Blazor *Interactive Server* keeps the API key on the server. It is never in the repo, the browser or the logs.

## Architecture and trade-offs

- **.NET 10 Blazor Web App.** Pages are static SSR by default; the manager screens are *Interactive Server*.
- **EF Core + SQLite.** A single file, created and seeded on first run, so there is nothing to install. In production this would be PostgreSQL or SQL Server with migrations.
- **`IFeedbackGenerator`.** Gemini, the demo file and test fakes are interchangeable, so switching to Claude or OpenAI means adding one class.
- **Cookie auth with Manager/Rep roles.** Authorization is enforced on the server: reps only ever query their own *approved* report.
- **Resilience.** Timeouts, rate limits (HTTP 429, which every AI provider uses) and invalid output all produce friendly messages; existing drafts are never lost.
- **Industry-agnostic.** Terms and stages ("Loan", "Funded") live in `appsettings.json` → `Domain`. Changing industries means changing the config and the seed.

```
src/WeeklySalesCoach/
  Data/                 entities, DbContext, seed, demo-feedback.json
  Services/Coaching/    context builder, report lifecycle, dashboard, parser/validator
  Services/AI/          prompt, Gemini client, demo generator, error messages
  Auth/                 cookie auth, demo quick-login
  Components/           Blazor pages and shared components
tests/WeeklySalesCoach.Tests/   xUnit (AI mocked)
```

## Tests

```bash
dotnet test
```

The tests cover:
- context building (trends, team average, zero quota, missing previous week)
- AI input privacy (no other reps' names)
- output validation (code fences, missing evidence, item limits)
- the retry logic
- the report lifecycle and authorization rules
- Gemini error mapping (429, timeout, 5xx)
- demo-file coverage

## Next steps

- Multiple weeks and trend history per rep
- CRM integration (HubSpot/Salesforce) instead of seed data
- Deliver approved feedback by email or Slack
- Rep acknowledgement and self-reflection on the feedback
- Production hardening: PostgreSQL, migrations, SSO, audit log

![Rep view](docs/screenshots/my-feedback.png)
````

- [ ] **Step 3: Final verification**

Run each command and confirm the expected result:

1. `dotnet build`: 0 errors, 0 warnings introduced by our code.
2. `dotnet test`: all tests pass. Report the count.
3. `grep -rn "AIza" --include=*.cs --include=*.json --include=*.md --include=*.razor .`: **no matches** (Google API keys start with `AIza`).
4. `grep -rn "\"ApiKey\": \"[^\"]" src/`: no matches (the committed `ApiKey` is empty).
5. Scripted demo flow on a fresh DB, in demo mode (temporarily run with `Gemini__ApiKey=` set to empty in the environment):
   `rm -f src/WeeklySalesCoach/weeklysalescoach.db && cd src/WeeklySalesCoach && Gemini__ApiKey= dotnet run --urls http://localhost:5080`
   - Roberto → Generate team → all drafts ready
   - Ana → edit a line → approve
   - Sign in as Ana → sees her edited feedback
   - Carlos → sees "Nothing here yet"
   - Compare the Ana and Carlos drafts: the content is clearly different
6. Confirm `.gitignore` contains `*.db`, `bin/`, `obj/`.

- [ ] **Step 4: Checkpoint and hand-off**

Report to the author:
- the test count
- the screenshot paths
- that the repo is ready for their `git init` + single commit + push (the agent does **not** run git)
- the pending item from the spec: optional online deployment with the key stored server-side
