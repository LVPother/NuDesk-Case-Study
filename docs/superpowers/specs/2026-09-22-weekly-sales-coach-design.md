# Weekly Sales Coach — Design Spec

- **Date:** 2026-09-22
- **Status:** Draft — pending author review
- **Context:** Take-home exercise for nuDesk. The brief asks for a simple, functional tool, automation or dashboard, built with any AI, for a Business Operations or Sales use case. It is evaluated on problem approach, thinking and building, not perfection.
- **Deadline:** Thursday 2026-09-24, 11:00 AM local time.
- **Deliverables:** GitHub repo, a short explanation (README), and optionally a demo video or screenshots.

---

## 1. Problem

Sales reps get little feedback on their work. What they do get usually comes late (quarterly or annual reviews), is vague ("keep it up", "improve follow-up") and is subjective. Managers know weekly coaching works, but writing specific, evidence-based feedback for every rep every week takes too long, so it doesn't happen.

## 2. Solution

**Weekly Sales Coach** reads each rep's activity for the week and drafts personalized coaching feedback with AI: strengths, areas to improve and 1–3 concrete actions for next week. **Every point cites evidence from the data.**

The AI is a **drafting assistant, not a judge**. A manager reviews, edits and approves each draft before the rep sees it (human in the loop). The tool coaches; it does not score, rank or make HR decisions.

### Goals
- Feedback is **personalized**: each rep gets a different message grounded in their own data, their trend and their personal goal.
- Feedback is **evidence-based**: specific deals, numbers and dates, not generic phrases.
- **Human control**: nothing reaches a rep without manager approval.
- **Responsible by design**: no sensitive personal data collected, clear rules for what the AI must and must not say.
- **Zero-friction demo**: a reviewer can clone, run and see the full flow with no configuration.

### Non-goals (out of scope, listed in the README as next steps)
- Real user registration, password reset or SSO.
- Sending real emails or notifications.
- Browsing multiple historical weeks (the demo covers one current week plus the previous week for trends).
- CSV upload or live CRM integration.
- Numeric scoring or ranking of reps.

## 3. Users and roles

| Role | Can do |
|---|---|
| **Manager** | See the team dashboard, generate or regenerate drafts, add a manager note, edit drafts, approve feedback |
| **Rep** | See only **their own approved** feedback |

## 4. Domain: generic engine, FinServ demo data

The engine is domain-agnostic: reps, a staged pipeline, weekly activity metrics and deals. The **demo dataset** models a **loan origination team (loan officers)**, because nuDesk works only in financial services. Changing industries means changing the seed data and the stage/label configuration, not the code.

**Demo pipeline stages:** `Lead` → `Application` → `Documents Pending` → `Underwriting` → `Approved` → `Funded` (won) / `Lost`.

Stage names and user-facing terms (e.g. "Loan" vs. "Deal", "Loan Officer" vs. "Sales Rep") come from a domain configuration in the seed/settings.

## 5. Data model

All entities are persisted with EF Core in SQLite.

| Entity | Fields (main) |
|---|---|
| **User** | Id, Username, PasswordHash, Role (`Manager` / `Rep`), RepId (nullable, set for reps) |
| **Rep** | Id, Name, Title, TenureMonths, WeeklyQuota (funded volume, $), PersonalGoal, FeedbackStyle (`Direct` / `Encouraging`) |
| **Week** | Id, StartDate, EndDate, IsCurrent |
| **WeeklyMetrics** | RepId, WeekId, CallsMade, CallsConnected, EmailsSent, EmailsReplied, NewLeads, ApplicationsSubmitted, DealsWon, WonVolume, DealsLost, AvgLeadResponseHours |
| **Deal** | Id, RepId, ClientName (fictional), Product (e.g. Mortgage, SBA Loan), Amount, Stage, CreatedAt, StageChangedAt, LastContactAt, Notes, LostReason (nullable) |
| **CoachingReport** | Id, RepId, WeekId, ManagerNote, Status (`NotGenerated` / `Draft` / `Approved`), Content (strengths, improvements, actions — stored as JSON), Source (`Live` / `Demo`), IsEdited, ModelName, GeneratedAt, ApprovedAt, ApprovedByUserId |

- The **team average** is computed, not stored.
- **Not collected, by design:** age, gender, marital status, health, religion or any other sensitive attribute. If the AI never receives it, it cannot use it.

## 6. Demo team (seed data)

Two weeks of data: the current week (Mon 2026-09-14 → Fri 2026-09-18) and the previous week (2026-09-07 → 2026-09-11). Each profile is designed to exercise a different coaching capability.

| Loan Officer | Profile | What the AI should pick up |
|---|---|---|
| **Ana** — Senior, 4 yrs | Top performer, above quota | Recognize concrete wins; subtle improvement: small-loan leads wait too long for her first response |
| **Carlos** — 1 yr | High activity, low conversion | Many calls, but applications stall in `Documents Pending` with no follow-up |
| **Sofía** — New, 2 months | Improving | Encouraging tone; recognize week-over-week growth; concrete next steps |
| **Miguel** — Veteran | Performance dipped | Manager note: *"Out 2 days this week for compliance training."* The AI must factor this in and not penalize him |
| **Laura** — 2 yrs | Steady, excellent follow-up, low volume | Personal goal: *"Close larger loans."* Feedback focuses on that goal |

Plus one manager: **Roberto — Sales Manager**.

All names, clients and figures are fictional.

## 7. AI design

### 7.1 Provider
- **Google Gemini** (Flash model family, free tier via Google AI Studio). The model name is set in `appsettings.json`; the default is the current Flash model available in AI Studio at implementation time.
- The provider sits behind an `IFeedbackGenerator` interface so it can be swapped (e.g. Claude, OpenAI) or mocked in tests.

### 7.2 Input: the coaching context
`CoachingContextBuilder` assembles, for **one** rep:
- Profile: name, title, tenure, weekly quota, personal goal, feedback style.
- Metrics: current week, previous week and **team average** (aggregate only).
- Deals: stage, amount, product, days since last contact, days in current stage, notes, lost reason.
- Manager note (if any).

It **never** includes other reps' names or individual data, or any sensitive attribute.

### 7.3 System instructions (fixed rules)

**The AI must:**
1. Act as an experienced, fair and constructive sales coach.
2. Base every statement only on the provided data.
3. Cite specific evidence (deal, number, date) for each point.
4. Follow the fixed structure: strengths → improvements → 1–3 actions for next week.
5. Always include at least one genuine strength, even in a weak week.
6. Adapt tone to tenure and preferred feedback style.
7. Talk about behaviors, not personality ("didn't follow up", not "is disorganized").
8. Take the manager note into account as context.

**The AI must not:**
1. Invent data or assume causes that aren't in the data.
2. Comment on personal matters (health, private life, age, gender, etc.).
3. Recommend HR actions (termination, discipline, compensation).
4. Mention other reps by name (comparing against the team average is allowed).
5. Use humiliating, sarcastic or threatening language.
6. Guess when data is missing: it must state that the data is insufficient.

### 7.4 Output: structured JSON
Gemini is called in structured-output mode with a required response schema:

```
summary        string              — 1–2 sentence overview of the week
strengths[]    1–3 items           — { title, detail, evidence }
improvements[] 1–3 items           — { title, detail, evidence }
actions[]      1–3 items           — { action, why, successSignal }
dataGaps[]     0+ strings          — what couldn't be assessed and why
```

### 7.5 Validation
A response is valid only if it parses against the schema, item counts are within range, and every strength/improvement has non-empty `evidence`. On failure: **one automatic retry**, then a user-facing error. Invalid content is never saved.

## 8. Architecture

**One Blazor Web App project** (.NET 10, **Interactive Server** render mode) plus one test project.

Interactive Server keeps all code, including the Gemini API key, on the server. The key never reaches the browser, which would not be true with WebAssembly.

```
WeeklySalesCoach.sln
├─ src/WeeklySalesCoach/
│  ├─ Data/                 DbContext, entities, seed (incl. pre-generated demo feedback)
│  ├─ Services/Coaching/    CoachingContextBuilder, CoachingService (generate/edit/approve), validation
│  ├─ Services/AI/          IFeedbackGenerator, GeminiFeedbackGenerator, DemoFeedbackGenerator, prompt
│  ├─ Auth/                 cookie auth setup, demo quick-login
│  └─ Components/Pages/     Login, Dashboard, Review, MyFeedback
└─ tests/WeeklySalesCoach.Tests/   xUnit
```

- **Database:** SQLite (a single file, nothing to install), created and seeded on first run. In production this would move to PostgreSQL or SQL Server.
- **Configuration:** the Gemini API key comes from `dotnet user-secrets` or an environment variable, **never committed**.

### 8.1 Generation flow

```
Manager clicks "Generate" (or "Regenerate" after adding a manager note)
  → CoachingContextBuilder builds the rep's context
  → IFeedbackGenerator
       key configured   → GeminiFeedbackGenerator (live)
       no key           → DemoFeedbackGenerator (pre-generated content from seed)
  → validate (retry once on failure)
  → save CoachingReport as Draft (Source = Live | Demo)
  → Manager edits (IsEdited = true) → Approve (Status = Approved, ApprovedAt, ApprovedBy)
  → Rep can now see it
```

- **"Generate for whole team"** processes reps **sequentially** with a progress indicator. Reports already generated are kept if a later one fails.
- **Regenerating** a draft the manager has edited asks for confirmation first ("This will replace your edits").

### 8.2 Demo mode (no API key)
- The seed includes **pre-generated feedback** for each rep, produced by the real Gemini integration during development and exported with a dev-only command-line flag (`--export-demo-feedback`).
- Without a key, the flow is identical (generate → edit → approve → rep view). A visible banner says: *"Demo mode: showing pre-generated AI feedback. Configure a Gemini API key to generate live."*
- **Revisit later:** optionally deploy the app online with the author's key stored server-side (the author wants to come back to this topic).

## 9. Screens

1. **Login:** username/password form, plus **quick-login buttons** ("Sign in as Manager", "Sign in as Ana (Loan Officer)", …) under a clear disclaimer: *"Demo accounts — for demonstration purposes only."* Quick login is enabled by a config flag (`Demo:EnableQuickLogin`, on by default).
2. **Team Dashboard (Manager):** KPI stat row (funded volume vs. team quota, loans funded, applications, average lead response time); a grid of rep cards (initials, title, quota attainment, report status: *Not generated / Draft ready / Approved*); a "Generate feedback for whole team" button.
3. **Review (Manager, per rep):**
   - Left: the week's evidence (metrics vs. previous week and team average, deal list with stages and days since last contact).
   - Right: the AI draft by section, **editable** (edit text, remove items).
   - Manager note field + "Regenerate", and "Approve & share".
   - Approved reports are read-only.
4. **My Feedback (Rep):** the rep's approved report in a clean, friendly layout. If nothing is approved yet: *"Your manager hasn't shared this week's feedback yet."*

**UI language:** English.

## 10. Visual style

The style follows nuDesk's website so the tool feels aligned with the interviewer's brand. **Only the style is adopted, not their logo or name.** The app has its own name, and the README states it was built as a take-home for nuDesk.

| Token | Value (approx.) | Use |
|---|---|---|
| Ink / primary | `#1F2937` | Headings, primary buttons, main text |
| Accent green | `#3FA45B` | Highlighted words, big stat numbers, badges, status |
| Background | `#F7F8F8` | Page background |
| Mint section | `#E4F0EE` | Alternate section background |
| Muted text | `#5B6B7A` | Secondary text |
| Card | `#FFFFFF` + subtle border | Cards, panels |

- **Typography:** Manrope (Google Fonts), large bold headings.
- **Patterns:** pill badge (light green background, green text, dot) above section titles; two-line headings with the second line in green; a stat row with large green numbers; a white card grid with thin line icons; dark solid primary buttons plus outlined secondary buttons; generous whitespace.

## 11. Security and authorization
- Cookie authentication; passwords hashed with ASP.NET Core's `PasswordHasher`.
- Manager pages require the `Manager` role.
- Rep queries are always filtered **server-side** by the signed-in user's `RepId` and `Status = Approved`. Changing the URL cannot expose other reps' data or drafts.
- The API key is never in the repo, never sent to the browser, and never shown in errors.

## 12. Error handling

| Situation | Behavior |
|---|---|
| No API key | Demo generator + banner; "Regenerate" explains how to enable live AI |
| Gemini timeout (~30 s) or network error | Friendly "The AI didn't respond, please try again"; the existing draft is kept |
| Rate limit (HTTP 429; applies to any AI provider) | "AI usage limit reached, please wait a minute"; completed reports are kept |
| Invalid JSON / missing evidence | One automatic retry, then an error; nothing broken is saved |
| Rep accesses another rep's data | Access denied (server-side) |
| Regenerate over manager edits | Confirmation dialog |

Technical details are logged; users never see stack traces or secrets.

## 13. Testing

**Automated (xUnit, AI mocked through `IFeedbackGenerator`, SQLite in-memory):**
1. Context builder: metrics, week-over-week trends and team average are correct; the manager note is included.
2. Context privacy: no other rep names or sensitive fields in the AI input.
3. Response validation: valid JSON is accepted; invalid JSON or missing evidence is rejected and triggers the retry.
4. Status flow: Draft → Approved; reps can't see drafts.
5. Authorization: a rep only gets their own approved reports.
6. Demo mode: no key → demo generator is used, no errors.

**Manual:** a scripted run-through of the demo flow (also used to record the video). Dashboard → generate team → review Ana → edit a line → approve → sign in as Ana → compare with Carlos's feedback to show the personalization.

## 14. Deliverables
- **Repo** with a one-command run: `dotnet run` (the database is seeded automatically).
- **README (English):** problem, solution, how it works (flow diagram), AI design and guardrails, responsible-AI choices, how to run (with and without an API key), design decisions and trade-offs (SQLite vs. Postgres, Interactive Server, human-in-the-loop, demo mode), next steps, and a note that it was built for the nuDesk take-home.
- **Screenshots** and, optionally, a short demo video showing live generation.

## 15. Open items
- Revisit online deployment with the key stored server-side (deferred by the author).
- Final app name/logo (working name: "Weekly Sales Coach").
