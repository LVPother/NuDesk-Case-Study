# Weekly Sales Coach — developer guide

Technical companion to [README.md](README.md) (which explains the product in business terms). This document covers setup, configuration, architecture, the AI pipeline, the data model, security decisions, tests, and a file-by-file map of the code.

## Stack

| Layer | Choice | Why |
|---|---|---|
| Runtime | .NET 10 (`net10.0`) | Latest LTS-track SDK; the author's daily stack |
| Web | Blazor Web App, static SSR by default, `InteractiveServer` per page | Interactive pages need it (buttons, live progress); the login page stays plain HTML forms |
| Data | EF Core 10 + SQLite, `EnsureCreated` (no migrations) | Zero install for reviewers; schema is recreated from the model |
| Auth | ASP.NET Core cookie authentication, `PasswordHasher<AppUser>`, roles `Manager` / `Rep` | Server-side authorization; reps can only ever read their own approved report |
| AI | Anthropic C# SDK (`Anthropic` NuGet) with structured outputs; Gemini REST as an alternative; a demo generator reading a JSON file | One interface, three interchangeable implementations |
| Tests | xUnit, in-memory SQLite, fake generator | 91 tests, AI is never called from tests |

## Setup

```bash
dotnet --version          # needs 10.x
dotnet test               # 91 tests
cd src/WeeklySalesCoach
dotnet run                # prints the URL; the DB is created and seeded on first run
```

The SQLite file `weeklysalescoach.db` is created next to the project and is gitignored. **Any change to the entity model requires deleting that file** (there are no migrations; `EnsureCreated` does not alter existing tables).

### Live AI

Store the key outside the repo, then run again. The demo banner disappears and drafts are generated live.

```bash
dotnet user-secrets set "Claude:ApiKey" "YOUR_KEY" --project src/WeeklySalesCoach
# or the environment variable Claude__ApiKey
# Gemini: "Gemini:ApiKey" / Gemini__ApiKey
```

### Configuration (`appsettings.json`)

| Section | Keys | Notes |
|---|---|---|
| `ConnectionStrings:Default` | SQLite path | Relative to the project directory (`RunWorkingDirectory` is set in the csproj) |
| `Domain` | `DealTerm`, `RepTerm`, `WonStage`, `LostStage` | Industry vocabulary used in the UI and the prompt. Change these plus the seed to switch industries |
| `Ai` | `Provider` (`Claude` \| `Gemini`) | Preferred provider. `AiProviderSelector` falls back to whichever has a key, then to Demo |
| `Claude` | `ApiKey`, `Model` (default `claude-haiku-4-5`), `TimeoutSeconds` | `claude-opus-5` gives richer drafts at roughly 5x the cost |
| `Gemini` | `ApiKey`, `Model` (`gemini-3.6-flash`), `TimeoutSeconds` | REST `generateContent` with a response schema |
| `Demo` | `EnableQuickLogin`, `DemoFeedbackPath` | Quick-login buttons on the login page; the pre-generated feedback file |

### Refreshing the demo feedback file

```bash
dotnet run --project src/WeeklySalesCoach -- --export-demo-feedback
```

Runs the configured live generator for every seeded rep and writes `Data/Seed/demo-feedback.json`. It is resumable (`demo-feedback.json.partial`) and retries transient failures (429, 5xx, timeouts, invalid output).

## Architecture

```
Browser ──(Blazor Server circuit / plain form posts)──► ASP.NET Core
                                                          │
   Components/Pages (Razor)  ──►  Services/Coaching  ──►  IFeedbackGenerator ──► Claude | Gemini | demo file
                                    │       │
                                    │       └──► Services/Notifications (email outbox)
                                    │
                                    └──► EF Core (AppDbContext) ──► SQLite
```

Three rules keep it simple:

1. **Pages never talk to the AI or the DbContext directly.** They call a service (`CoachingService`, `TeamDashboardService`, `CoachingContextBuilder`, `FeedbackNotificationService`, `FeedbackCsvService`).
2. **Services use `IDbContextFactory<AppDbContext>`**, creating a short-lived context per operation. This avoids the classic Blazor Server pitfall of a long-lived scoped DbContext shared by a whole circuit.
3. **The AI is behind one interface** (`IFeedbackGenerator`). Provider selection happens once in `Program.cs`; nothing else knows which provider is active.

### Report lifecycle

```
NotGenerated ──Generate──► Draft ──SaveEdits──► Draft (IsEdited) ──Approve──► Approved ──Reopen──► Draft (IsEdited)
```

- `GenerateAsync` builds the context, calls the generator, parses and validates, retries once on invalid output, and stores the JSON with source (`Live`/`Demo`), model name and timestamp.
- `GenerateTeamAsync` runs one rep at a time, skips approved or hand-edited reports, keeps going when one rep's output fails validation, and stops early on rate limits or configuration errors (`TeamGenerationResult`).
- `ApproveAsync` freezes the text and records who approved it and when. Only then does `GetApprovedForRepAsync` return it to the rep.
- `ReopenAsync` returns an approved report to `Draft`, marks it edited (so a team run won't overwrite text the rep already saw) and clears the approval fields. Re-approval queues a second, "Updated" email.

### AI pipeline

```
CoachingContextBuilder.BuildAsync(repId, weekId)
   → CoachingContext: rep profile, current + previous week metrics, team average (aggregate only), deals, manager note
CoachingPrompt.SystemInstructions + BuildUserMessage(context)
   → provider call with a required JSON schema (Claude: structured outputs; Gemini: responseSchema)
CoachingContentParser.Parse(raw)
   → strips code fences, repairs double-escaped JSON, trims lists to 3, requires evidence on every item
   → InvalidCoachingContentException → CoachingService retries once
```

Guardrails live in two places: the **system prompt** (must cite data; must not invent, name other reps, discuss personal matters or recommend HR actions) and the **context builder** (the AI is never given other reps' rows, only an aggregate average; `CoachingContextBuilderTests` asserts no other rep's name appears in the input).

Error mapping: `AiUnavailableException(AiFailureKind)` distinguishes `Timeout`, `RateLimited` (HTTP 429), `Unavailable` (5xx / network), `NotConfigured` and `InvalidRequest` (4xx, non-retryable). `AiErrorMessages.For(ex)` turns them into the sentences the UI shows.

### Data model

| Entity | Purpose |
|---|---|
| `AppUser` | Login (username, hashed password, role, optional `RepId`) |
| `Rep` | Profile: title, tenure, weekly quota, personal goal, feedback style, email |
| `Week` | Monday–Friday range; exactly one has `IsCurrent = true` |
| `WeeklyMetrics` | Activity numbers per rep per week |
| `Deal` | Pipeline row: client, product, amount, stage, days in stage, last contact, notes |
| `CoachingReport` | One per rep per week: status, content JSON, source, model, edited flag, approval fields, manager note |
| `FeedbackEmail` | Outbox: one row per approval (to, subject, body, provider, status Queued/Sent/Failed) |

Content is stored as JSON (`CoachingContent`: summary, strengths, improvements, actions, dataGaps) rather than as relational rows, because it is written and read as a unit and its shape belongs to the AI schema.

### Security decisions

- Cookie auth, `HttpOnly`, `SameSite=Lax`, 8-hour sliding expiration.
- Every page declares `[Authorize(Roles = ...)]`; export endpoints use `RequireRole`. The rep id comes from a claim in the cookie, never from the URL.
- Static login page uses `<AntiforgeryToken />`; a stale-tab antiforgery failure is caught by middleware and redirected to `/login?expired=1` instead of a raw 400.
- A signed-in user who hits a page they can't access sees a "No access" card (`/access-denied` → `/login?denied=1`); anonymous users go to the login page.
- API keys live in user-secrets or environment variables and only ever exist on the server (Blazor Server keeps all service calls server-side).

### Email delivery

`FeedbackNotificationService.QueueApprovedReportAsync` composes a plain-text email (`FeedbackEmailComposer`, deterministic: the body is exactly the approved text) and stores it. Delivery goes through `IEmailSender`; the demo registers `NoProviderEmailSender`, so rows stay `Queued` and the review page shows a preview. To send for real, implement `IEmailSender` (SMTP via MailKit, SendGrid, Microsoft Graph…) and swap the registration in `Program.cs`.

### CSV export

`GET /export/feedback.csv` (Manager: every report) and `GET /export/my-feedback.csv` (Rep: own approved reports). `FeedbackCsvExporter.Build` writes UTF-8 with BOM, CRLF and quoted cells so the file opens cleanly in Excel and Google Sheets; one row per rep and week.

## Tests

```bash
dotnet test
```

| Area | File | What it proves |
|---|---|---|
| Context | `CoachingContextBuilderTests` | Trends, team average, zero quota, missing previous week, **no other rep's name in the AI input** |
| Prompt | `CoachingPromptTests` | System rules present, schema shape, user message contains the evidence |
| Parsing | `CoachingContentParserTests` | Code fences, escaped JSON, missing evidence, list limits |
| Lifecycle | `CoachingServiceTests` | Generate/retry, edit, approve, reopen, rep visibility, team run skip/continue/stop rules |
| Dashboard | `TeamDashboardServiceTests` | KPIs and per-rep cards |
| Providers | `ClaudeFeedbackGeneratorTests`, `GeminiFeedbackGeneratorTests`, `AiErrorMessagesTests` | Request shape, error mapping (429, timeout, 5xx, 4xx) |
| Demo | `DemoFeedbackGeneratorTests`, `DemoFeedbackExporterTests`, `SeedDataTests` | Every seeded rep has demo feedback; exporter resumes and retries |
| Email | `FeedbackNotificationTests` | Queue, send, fail, once per approval, "Updated" on re-approval |
| Export | `FeedbackCsvExporterTests` | Header, quoting, rep-only filter |
| Auth | `UserAuthServiceTests`, `AppDbContextTests` | Password check, claims, unique username |

`TestSupport/` holds `TestDb` (in-memory SQLite kept open for the test's lifetime), `TestData` (builders for weeks, reps, metrics, deals), `TestContent` (a valid AI JSON sample) and `FakeFeedbackGenerator` (scripted responses, call counter). UI components are not unit-tested (no bUnit); they were verified in the browser.

## Code map

### `src/WeeklySalesCoach/` (application)

| Path | Responsibility |
|---|---|
| `Program.cs` | Composition root: options, DbContext factory, generators + provider selection, services, cookie auth, antiforgery middleware, endpoints, `--export-demo-feedback` mode |
| `appsettings.json` | All configuration (see table above) |
| `WeeklySalesCoach.csproj` | `net10.0`, packages `Microsoft.EntityFrameworkCore.Sqlite`, `Anthropic`; `UserSecretsId`; `RunWorkingDirectory` |
| **Configuration/** | |
| `AppOptions.cs` | `DomainOptions`, `AiOptions`, `ClaudeOptions`, `GeminiOptions`, `DemoOptions` (typed views of `appsettings.json`) |
| **Data/** | |
| `Entities.cs` | Enums and entity classes listed under *Data model* |
| `AppDbContext.cs` | `DbSet`s, enum-to-string conversions, indexes |
| `DatabaseInitializer.cs` | `EnsureCreated`, seeds on first run, pre-approves Laura's current week and queues her email |
| `SeedData.cs` | Five reps, two weeks, metrics, deals, users, Miguel's manager note, Laura's previous-week approved report |
| `Seed/demo-feedback.json` | Pre-generated feedback per rep (produced by the exporter with the live model) |
| **Services/Coaching/** | |
| `CoachingContext.cs` | Records that describe one rep's week for the AI (`CoachingContext`, `RepProfile`, `MetricsSnapshot`, `DealSnapshot`) |
| `CoachingContextBuilder.cs` | Loads DB rows and computes trends, quota attainment and the aggregate team average |
| `CoachingContent.cs` | The AI's output shape (`CoachingContent`, `FeedbackItem`, `ActionItem`) and `InvalidCoachingContentException` |
| `CoachingContentParser.cs` | Parse, repair, normalize, validate and serialize content |
| `CoachingService.cs` | Report lifecycle (generate, team run, edits, approve, reopen, history, rep visibility) |
| `TeamDashboardService.cs` | KPIs and rep cards for the dashboard |
| `Calc.cs` | Small numeric helpers (attainment %, day differences) |
| `../Fmt.cs` | Display formatting (money, compact money, percent) in `en-US` |
| **Services/AI/** | |
| `IFeedbackGenerator.cs` | The provider interface plus `AiFailureKind` / `AiUnavailableException` |
| `CoachingPrompt.cs` | System instructions, user message builder, Gemini response schema, Claude JSON schema |
| `ClaudeFeedbackGenerator.cs` | Anthropic SDK call with structured outputs; maps SDK exceptions to `AiFailureKind` |
| `GeminiFeedbackGenerator.cs` | REST call with `responseSchema`; same error mapping |
| `DemoFeedbackGenerator.cs` | Reads `demo-feedback.json`; `IsLive = false` drives the demo banner |
| `AiProviderSelector.cs` | Picks Claude / Gemini / Demo from preference and configured keys |
| `AiErrorMessages.cs` | Human-readable messages per failure kind |
| `DemoFeedbackExporter.cs` | CLI mode that regenerates the demo file with retries and resume |
| **Services/Notifications/** | |
| `IEmailSender.cs` | `EmailMessage`, `EmailSendResult`, the sender interface, `NoProviderEmailSender` |
| `FeedbackEmailComposer.cs` | Approved content → plain-text email (first or "Updated" version) |
| `FeedbackNotificationService.cs` | Outbox: one email per approval, send if a provider exists, latest email lookup |
| **Services/Export/** | |
| `FeedbackCsvExporter.cs` | `FeedbackCsvRow`, CSV writer, `FeedbackCsvService` (manager / rep queries) |
| `ExportEndpoints.cs` | The two authorized CSV endpoints |
| **Auth/** | |
| `UserAuthService.cs` | Credential check, lookup for quick-login, `ClaimsPrincipal` creation (user id, role, rep id) |
| `AuthEndpoints.cs` | `GET /` (role-based home), `/access-denied`, `POST /auth/login`, `/auth/quick-login`, `/auth/logout` |
| `ClaimsPrincipalExtensions.cs` | `GetUserId()`, `GetRepId()` |
| **Components/** | |
| `App.razor` | HTML shell: Manrope font, favicon, `app.css`, Blazor script |
| `Routes.razor` | Router with `AuthorizeRouteView`, "No access" fallback, `NotFound` page, focus on `h1` after navigation |
| `_Imports.razor` | Shared `@using`s |
| `Layout/MainLayout.razor` | Top bar (brand, Team / My week link, user + role, sign out), page container, footer |
| `Layout/ReconnectModal.razor(.css)` | Template's reconnect dialog for the Blazor circuit |
| `Pages/Login.razor` | Static SSR login: password form, demo quick-login buttons, error/denied/expired messages, "No access" card for signed-in users |
| `Pages/Dashboard.razor` | Manager home: KPIs, team generation with live progress, rep cards with next-step cue, CSV link |
| `Pages/Review.razor` | Manager: evidence panel, draft summary, manager note, generate/regenerate with confirm, approved state with email preview, revise, history |
| `Pages/FeedbackEditor.razor` | Manager: full-width draft editor with large type, save edits, approve, unsaved-edits guard |
| `Pages/MyFeedback.razor` | Rep home: ready/not-ready notice, quota bar, metric bars, pipeline, past weeks, CSV link |
| `Pages/MyFeedbackView.razor` | Rep: approved feedback in reading mode with week selector |
| `Pages/Error.razor`, `Pages/NotFound.razor` | Error and 404 pages |
| `Shared/DemoBanner.razor` | "Demo mode" banner when the generator isn't live |
| `Shared/EvidencePanel.razor` | Metrics table (this week / last week / team) and pipeline table |
| `Shared/FeedbackView.razor` | Read-only rendering of `CoachingContent` (normal or `Large`) |
| `Shared/FeedbackItemsEditor.razor` | Editable list of `FeedbackItem`s (title, detail, evidence, remove) |
| `Shared/MetricBars.razor` | CSS bar chart: this week vs last week with team-average marker |
| `Shared/StatCard.razor`, `PillBadge.razor`, `StatusChip.razor`, `Toast.razor` | Small UI pieces |
| `wwwroot/app.css` | Design tokens (nuDesk-inspired palette), layout (max 1680px), components, large-type feedback, responsive rules |

### `tests/WeeklySalesCoach.Tests/`

See the *Tests* table. `WeeklySalesCoach.Tests.csproj` links `demo-feedback.json` so `SeedDataTests` can check coverage.

### `docs/`

`docs/screenshots/` (images used by the READMEs) and `docs/superpowers/` (the design spec and the implementation plan the project was built from).

## Known limitations and deferred items

- No migrations: schema changes mean deleting the local DB.
- `Review.razor` creates the report row on first GET (`GetOrCreateReportAsync`), which is convenient but means a visit has a side effect.
- The editor can remove items but not add new ones (the AI provides 1–3; the parser caps at 3).
- Logout is a form post without a confirmation; `ReturnUrl` after login is ignored (users land on their role's home).
- `GeminiFeedbackGenerator` uses a typed `HttpClient` registered scoped; fine for this app, would be a singleton-with-factory in production.
- No bUnit tests for components.
- Reopening a *past* week's approved report hides it from the rep until re-approved (versioning would avoid the gap; deliberately out of scope).

## Next steps

- CRM integration (HubSpot / Salesforce) replacing the seed
- Real email provider behind `IEmailSender`, plus a Slack notifier
- Optional AI pass to polish the outgoing email's tone and length
- Rep acknowledgement and self-reflection on the feedback
- Production hardening: PostgreSQL, migrations, SSO, audit log, report versioning
