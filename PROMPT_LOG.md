# Prompt Log

A record of the AI prompts used to build this project. Each entry has the prompt I sent, what came back, why I sent it, and what I kept, changed, or rejected.

Prompts are grouped by the thread of work they belong to rather than listed one per message, so a follow-up question and the answer that settled it appear together. Entries still marked TODO are awaiting my own notes.

---

## 1. Project kickoff: stack choices and working agreement

**Prompt**

> I'm building a take-home assessment: a self-hosted API plus a frontend that consumes Yahoo Finance intraday data (15m interval, 1 month range) and returns daily averages of lows and highs plus total volume.
> Context on me: Primarily program in Java, new to C#, and I've written plain JavaScript and CSS but I'm relatively new to React. Favor readability, and explain C# and React idioms by comparison to Spring and plain JS where that helps. Graded on production quality, SOLID, and maintainability. Assume requirements will grow. Don't write everything at once. We will collaborate one piece at a time. Ask me questions when a design decision has real tradeoffs.
> The assessment allows for C# .NET 8+ or Node.js/TypeScript for the backend, and React, Angular, or Vue for the frontend. View the assessment PDF for details. My choices and why:
> C# / .NET 8 over Node/TypeScript. My main programming language is Java but have experience in C, ARM Assembly and C++. I'm a few weeks into a Software Engineering course which is primarily building my react, Hibernate, Spring Boot, Vite, SQL, and apache skills.
> React over Angular or Vue. React is what my coursework covers and what I have some exposure to, and Vite is the fastest path to a running dev server. TypeScript for the same reason I picked C#: static typing is what I'm used to.
> xUnit for tests, as the standard .NET choice and the closest thing to JUnit I know.
> Push back on these choices if you find a wiser combination.
> Log my choices and my reasoning as the first entry in PROMPT_LOG.md/
> After each significant step, log prompts to the prompt log and ask me for reasoning; why I sent that prompt, what I kept or rejected from your output, and why. Write my answers into PROMPT_LOG.md to my own wording. [...]

**Stack choices and my reasoning**

| Layer | Choice | Reasoning |
|---|---|---|
| Backend | C# / .NET 8 | My main programming language is Java but have experience in C, ARM Assembly and C++. I'm a few weeks into a Software Engineering course which is primarily building my react, Hibernate, Spring Boot, Vite, SQL, and apache skills. |
| Frontend | React + TypeScript (Vite) | React is what my coursework covers and what I have some exposure to, and Vite is the fastest path to a running dev server. TypeScript for the same reason I picked C#: static typing is what I'm used to. |
| Backend tests | xUnit | The standard .NET choice and the closest thing to JUnit I know. |

**Why I sent this prompt**

Provide context for AI Collaborator to reduce future complications.

**What I kept, changed, or rejected**

Will be accepting .NET 10 for longevity.

Rejected both NSubstitute and Moq in favour of hand-written fakes (entry 6). Based on my own experience: fewer dependencies, more readable, and less time spent learning a new API in a tight project window.

Took the xUnit v3 advice (4.0.1) and used plain `Assert` throughout rather than adding an assertion library.

TODO: decide on Vitest + React Testing Library for the frontend.

---

## 2. Backend architecture: project layout and endpoint style

**Prompts**

> (Answering a question about how to split the backend into projects:) Api + Core + Infrastructure
>
> (Answering a question about Minimal APIs vs Controllers:) Provide detailed comparisons between the two options, in light of my familarity.
>
> A corporation that deals with heavy data sets in it's applications would prioitize controllers.
>
> I will download the sdk. And pick controllers as the choice.

**What the AI produced**

Chose the three-project layout — Core holds models, aggregation logic and the provider interface; Infrastructure holds the Yahoo client implementing it; Api holds controllers and wiring — so the compiler, not a convention, enforces that domain logic cannot reach for HTTP.

On endpoint style it compared Minimal APIs and Controllers across familiarity, testability, cross-cutting concerns, validation and reviewer expectations, recommending Minimal APIs. When I argued that data-heavy corporate systems favour controllers, it pushed back on the *reasoning* while accepting the conclusion: minimal APIs are in fact marginally faster under load because they skip the action-invoker and filter pipeline, so payload size is not the deciding axis. What genuinely favours controllers is endpoint count, content negotiation (XML/CSV via output formatters, which minimal APIs do not support out of the box), mature filter and versioning conventions, and consistency with an existing codebase.

**Why I sent these prompts**

My pushback was to see a cold, machined argument pushed back against my claim, to validate my assumption.

While low risk in this case, agent-driven downloads have been linked to malicious downloads; as such, I chose to download the SDK myself to avoid potentially malicious content being downloaded.

**What I kept, changed, or rejected**

I believe controllers likely favor the programming parameters that best serve Prometheus' clientele.

---

## 3. From upstream research to the Core layer

**Prompts**

> (AI-initiated, under the working agreement: verify the upstream API's real behaviour before designing types around it.)
>
> generate boilerplate code
>
> Before writing code, propose a solution structure using clean architecture with a provider abstraction, typed HttpClient, and xUnit tests. Explain how you'll handle null, reexplain grouping by the exchange's timezone, and decimal rounding to 4 places.
>
> We'll need to design it to ensure it Queries intraday data from the last month, Groups results by day, and Returns JSON in this format and precision listed on the assessment PDF so trailing zeros must be kept.
>
> Your explanations should be more concise, human readable, and provide examples from my known skill set that may not be as obivous to me (java examples, etc)
>
> I am not familiar with the terminology NSubstitute and hand-written fakes. Elaborate.
>
> Hand-written.

**What the AI produced**

Four findings from live calls against Yahoo, each tied to a design consequence:

1. Omitting the `User-Agent` header returns `HTTP 429`, not `403`, so the header must be configured once on the typed `HttpClient`.
2. An unknown symbol returns `HTTP 404` with `{"chart":{"result":null,"error":{"code":"Not Found","description":"No data found, symbol may be delisted"}}}`, which maps cleanly to our own 404.
3. The payload is parallel arrays (`timestamp[]`, `indicators.quote[0].low/high/volume`), so the adapter must zip them into bars at the boundary. Yahoo emits `null` inside those arrays.
4. Grouping by UTC date is wrong for exchanges whose session crosses UTC midnight. Air New Zealand's five trading days become six UTC "days", two of them partial. Grouping by `meta.exchangeTimezoneName` is correct everywhere and costs nothing for US symbols.

Then the solution itself: five projects with the reference graph `Api -> Core, Infrastructure` and `Infrastructure -> Core`, Core referencing nothing. Template samples deleted, `Directory.Build.props` (shared target framework, nullable reference types, warnings-as-errors) and `Directory.Packages.props` (Central Package Management) added, and `public partial class Program;` exposed so integration tests can boot the real host.

The design review that followed set out the layer map, the `IIntradayDataProvider` port, the typed `HttpClient` registration, and a four-layer null policy: nullable reference types, nullable DTOs, incomplete bars dropped at the mapper, and `200 []` for a valid symbol with no data. The trailing-zeros requirement was tested rather than assumed: `Math.Round(40.29m, 4)` still serialises as `40.29`, because rounding only reduces a decimal's scale. A `JsonConverter<decimal>` formatting with `F4` is what produces `40.2900`, written raw so it stays a JSON number. It also established that the UTC fallback must be visible to the frontend through `X-Grouping-Timezone` headers rather than an envelope, since the PDF fixes the body as a bare array.

Core then landed: `Symbol`, `IntradayBar`, `ExchangeTimeZone`, `IntradaySeries`, `DailySummary`, `DailySummaryCalculator`, and 23 passing tests. Three things worth recording:

- **xUnit v2 → v3 (4.0.1).** On the .NET 10 SDK, VSTest is no longer supported for Microsoft Testing Platform projects, so `dotnet test` failed until the solution opted in via `global.json`.
- **`tests/.editorconfig`** disables CA1707 for test projects only, so test names keep underscores without failing the warnings-as-errors build.
- **The daylight-saving test failed, and the test was wrong, not the code.** The AI's expected dates were miscalculated. Verified against the runtime: `2026-11-01T04:30Z` in New York is `00:30 on 1 Nov` (still EDT, −4), while a fixed −5 offset would give `23:30 on 31 Oct`. Rewritten, it now demonstrates the exact trap it was written for.

**Why I sent these prompts**

We came to a general mental model and structure for the assessment. I believe it was time to begin development and approach the AI collaboration as a system designer and reviewer. Given the three-day window, this appeared to be the assessment's intended approach.

**What I kept, changed, or rejected**

Hand-written fakes over NSubstitute.

TODO: the rest — in particular the reading of "this format and precision" as requiring trailing zeros, which is why the decimal converter exists.


## 4. Repository layout, and the backend skeleton

**Prompts**

> Restructure the folder scheme to hold a "frontend" folder and a "backend" folder
>
> Is the folder a .net project? it appears it was not created properly.
>
> Generate skeleton for the project based on our designs.

**What the AI produced**

Moved the solution to `StockApp/backend/` with `StockApp/frontend/` alongside, leaving `Technical-Documents/` in place. The "is this a .NET project" question turned out to be about `.slnx`, .NET 10's new XML solution format, which older tooling does not recognise — this became the decisive finding in entry 6.

The skeleton then filled in both remaining layers: `YahooFinanceOptions` validated at startup, internal wire DTOs, `YahooChartMapper`, the typed-client `YahooFinanceIntradayProvider` with HTTP failures translated into domain exceptions, one `AddYahooFinanceMarketData()` registration, plus the controller, response contract, decimal converter, exception handler and CORS. Verified end to end against live Yahoo: 22 days for TSLA with correct trailing zeros, 404 for an unknown symbol, 400 for a malformed one, and `Pacific/Auckland` grouping for a New Zealand listing.

Two decisions the analyzers forced, both kept: source-generated logging via `[LoggerMessage]` partial methods rather than direct `logger.LogWarning` calls (CA1848), and `HttpClient.Timeout` left unbounded so the resilience pipeline owns timing rather than cancelling across all retry attempts at once.

**Why I sent these prompts**

The folder scheme should be human-readable at a glance. The request to examine the .NET project structure was to ensure the folder was set up properly before proceeding.

**What I kept, changed, or rejected**

This was a consequence of requesting boilerplate code early, before verification, and should have been done beforehand.

---

## 5. Targeting .NET 8 for reviewer compatibility

**Prompts**

> A reviewer may use an older version of .Net. I've decided to reread the requirement as compatibility with 8+ rather than a selection from anything 8 and higher. Rebuild the skeleton to hit .Net 8.0
>
> SDK downloaded. Rebuild and explain the testing differences of MTP and VSTest.
>
> How does this compare to written JUnit tests.

**What the AI produced**

Retargeted to `net8.0` and, importantly, pinned `LangVersion` to 12.0 and `AnalysisLevel` to `8.0-recommended` — without those, a newer SDK would happily compile C# 13 or 14 syntax into a `net8.0` project that then fails on a reviewer's .NET 8 SDK. Framework packages moved to their 8.x lines, and `Microsoft.AspNetCore.OpenApi` was swapped for Swashbuckle, since `AddOpenApi()` is a .NET 9+ API.

Installing the .NET 8 SDK then exposed a real problem that reasoning alone had missed: **SDK 8 cannot read `.slnx` at all**, failing with `MSBUILD : error MSB1003`. The solution was converted to the classic `.sln` format. After that, both SDKs build clean and run all tests — SDK 8 through VSTest, SDK 10 through Microsoft Testing Platform, from one source tree.

The explanations covered MTP versus VSTest (test projects as executables that run their own tests, versus a host process loading adapters; "zero tests ran" being an error under MTP but a pass under VSTest) and xUnit versus JUnit (constructor as `@BeforeEach`, `IClassFixture` in place of `@BeforeAll`, `[Theory]`/`[InlineData]` for `@ParameterizedTest`, and xUnit running test classes in parallel by default where JUnit does not).

**Why I sent these prompts**

I reexamined the requirements. My initial assumption was that .NET 8+ allowed for any version that is 8 or higher to be used. I combined this rationale with the project's goal of being maintainable and modern, which would most likely want the latest long-term supported version. Later, I decided it was more likely, and less consequential, to read the requirement as compatibility with .NET 8+, as this would make the statement inclusive.

**What I kept, changed, or rejected**

TODO

---

## 6. Packages and one type per file

**Prompts**

> Why are interfaces nested within classes?
>
> Yes create packages and have interfaces be their own file.

**What the AI produced**

Clarified that nothing was nested: C#, unlike Java, does not tie file names to type names, so several top-level types were sharing a file. Reorganised into folder-based packages — `Models`, `Abstractions`, `Services`, `Exceptions` in Core, and `Contracts` under the Yahoo adapter — with one type per file throughout and namespaces matching folder paths.

Enforcement rather than habit: `.editorconfig` now sets `dotnet_diagnostic.IDE0130.severity = warning`, which with warnings-as-errors fails the build if a namespace stops matching its folder. This restores the guarantee Java's compiler gives for free.

**Why I sent these prompts**

Improve human readability.

**What I kept, changed, or rejected**

TODO

---

## 7. Scope, cadence and reporting

**Prompts**

> Generate a progress report with time estimates.
>
> Remember this is a MVP. We will not be overbuilding the assessment. The goal is reach the bare minimum target but structuring the project to allow for future growth.
>
> Ok let's take things one step at a time. Let's priotize a complete backend skeleton to commit.
>
> Ok perfect, from now on let's work on each file one by one or comprehensively if needed. leave the committing to me. Generate new time estimate and progress report.

**What the AI produced**

The first estimate came in at 10–12 hours and included caching, Docker, CI and extra frontend polish. After the MVP correction it was cut to ~6 hours covering only what the PDF asks for, with the extras demoted to a "known extension points" note in the README. The structural affordances already paid for — the layer split, the provider port, the `BarInterval`/`Lookback` enums instead of hardcoded query strings — stayed, on the grounds that they are affordances rather than features.

The backend was then committed in five messages recording rationale rather than content, and a clone to a fresh directory verified it builds and tests clean with no build artefacts tracked. From this point the AI works a file at a time and does not commit.

**Why I sent these prompts**

Return AI-Agent back to project scope. Generate progress reports to aid in pacing. Refine AI-Agent process to allow for a more structured review process.

**What I kept, changed, or rejected**

TODO

---

## 8. Comprehensive tests, and surviving upstream API changes

**Prompts**

> Generate comprehensive tests with full line coverage. Account for exceptions and null values.
>
> The backend must gracefully handle api changes. While, api changes are against standard practice. The program should contain a mechanism to route the frontend to an error page.
>
> Yes, proceed with those additions and provide a diff view of your changes.
>
> Proceed. Provide a brief for each test in this chat.

**What the AI produced**

81 new tests across two suites, taking the total from 23 to 104: `StockApp.Infrastructure.Tests` (request shape, every failure translation, every null path, time-zone fallback, and a captured Air New Zealand payload as an offline fixture) and `StockApp.Api.Tests` (the PDF's exact JSON bytes, grouping headers, and every error code).

For graceful handling of upstream change, five stable codes — `INVALID_SYMBOL`, `SYMBOL_NOT_FOUND`, `UPSTREAM_RATE_LIMITED`, `UPSTREAM_UNAVAILABLE`, `UPSTREAM_CONTRACT` — are now published on every failure response, so a client branches on an identifier rather than on prose. The last two are deliberately distinct despite both being 502s: one is worth retrying, the other means the data source changed and retrying will not help.

**Three bugs the tests found, none of which reading had caught:**

1. **`NotSupportedException` escaped the provider.** `ReadFromJsonAsync` throws it — not `JsonException` — when a body cannot be decoded, so an undecodable upstream response would have produced a 500 rather than a graceful 502. This was exactly the failure mode the requirement targeted.
2. **The 400 was served as `application/json`**, not `application/problem+json`, because a class-level `[Produces]` attribute silently overrides each result's own content type.
3. **The integration tests could not run on .NET 10.** `TestHost` ships as a package pinned at 8.0.x, while the solution-wide `RollForward` ran the tests on the .NET 10 runtime, whose `System.Text.Json` needs `PipeWriter.UnflushedBytes` that the older writer lacks. Roll-forward now applies to the API project alone.

**Why I sent these prompts**

Hedging against poorly generated tests, from experience with AI-driven tests and their shortfalls.

**What I kept, changed, or rejected**

TODO

---

## 9. Rounding policy for financial values

**Prompts**

> We should be careful on how the system rounds data for financial items. Research standard practices and comeback with a brief report. Cite your sources and the expectations of clients. My rationale would be a cut off rather than a rounding. See if the market supports this thinking.
>
> For the example the assessment provides retrieve raw data and see how they rounded it their own samples. We'll follow this.
>
> Yes default to the current convention, but allow the user to switch the mode.

**What the AI produced**

Research against primary sources rather than assertion. EU Regulation 1103/97 states that conversion rates "shall not be rounded or truncated" and that amounts exactly half-way "shall be rounded up"; Regulation DD's APY examples (`5.644% → 5.64%`, `5.645% → 5.65%`) are round-half-up. Truncation is standard where the reduced value *is* the transacted quantity — snapping to a tick or lot, or paying the amount actually computed — which a derived daily average is not. Measured on our own data, truncation would change about half the published values and bias every one of them low by roughly 5×10⁻⁵.

The attempt to reverse-engineer the PDF's sample showed it cannot be done: TSLA first traded on 2010-06-29, seventeen months after the sample's date of 2009-01-30, and Yahoo limits 15-minute history to the last 60 days, refusing the request outright. The sample is an illustration of shape and precision, not a worked example, so it encodes no rounding policy to copy.

Precision therefore became configuration: `PricePrecision:DecimalPlaces` (4) and `PricePrecision:Rounding` (`AwayFromZero`), validated at startup, with `ToZero` available as genuine truncation — verified to be a directed mode affecting every value, not only midpoints. Six tests cover the default, both alternative strategies, varying decimal places, and startup failure on an impossible setting.

**Why I sent these prompts**

Researching best practices to ensure the delivered product is satisfactory.

**What I kept, changed, or rejected**

TODO

---
## Manual changes made outside of AI

TODO — describe any edits made by hand, and why.
