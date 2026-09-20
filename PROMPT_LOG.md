# Prompt Log

A record of the AI prompts used to build this project. Each entry has the prompt I sent, why I sent it, and what I kept, changed, or rejected from the output.

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

Will be accepting .Net 10 for longevity.

TODO: rule on the rest of the pushback (xUnit v3, NSubstitute over Moq, plain `Assert`/Shouldly over FluentAssertions, Vitest + React Testing Library on the frontend).

---

## 2. Backend architecture: project layout and endpoint style

**Prompt**

> (Answering a question about how to split the backend into projects:) Api + Core + Infrastructure
>
> (Answering a question about Minimal APIs vs Controllers:) Provide detailed comparisons between the two options, in light of my familarity.

**What the AI produced**

Chose the three-project layout (Core holds models, aggregation logic and the `IStockDataProvider` interface; Infrastructure holds the Yahoo client that implements it; Api holds controllers and wiring) so the compiler, not a convention, enforces that domain logic cannot reach for HTTP. Then a comparison of Minimal APIs vs Controllers across familiarity, testability, cross-cutting concerns, validation and reviewer expectations, with a recommendation of Minimal APIs and an explicit case for when Controllers are the better trade.

**Why I sent this prompt**

TODO

**What I kept, changed, or rejected**

TODO

---

## 3. Endpoint style decision: controllers

**Prompt**

> A corporation that deals with heavy data sets in it's applications would prioitize controllers.
>
> I will download the sdk. And pick controllers as the choice.

**What the AI produced**

Pushed back on the reasoning rather than the conclusion: minimal APIs are in fact marginally *faster* under load because they skip the action-invoker and filter pipeline, so payload size is not the deciding axis. What actually favours controllers in a corporate setting is endpoint count, content negotiation (XML/CSV via output formatters, which minimal APIs do not do out of the box), mature filter and versioning conventions, and consistency with an existing codebase. Accepted controllers as a defensible choice on those grounds.

**Why I sent this prompt**

TODO

**What I kept, changed, or rejected**

TODO

---

## 4. Investigating the Yahoo Finance response before writing code

**Prompt**

> (Implicit in the working agreement: verify the upstream API's real behaviour before designing types around it.)

**What the AI produced**

Four findings from live calls, each tied to a design consequence:

1. Omitting the `User-Agent` header returns `HTTP 429`, not `403`, so the header must be configured once on the typed `HttpClient`.
2. An unknown symbol returns `HTTP 404` with `{"chart":{"result":null,"error":{"code":"Not Found","description":"No data found, symbol may be delisted"}}}`, which maps cleanly to our own 404.
3. The payload is parallel arrays (`timestamp[]`, `indicators.quote[0].low/high/volume`), so Infrastructure must zip them into bar objects at the boundary. Yahoo can emit `null` inside those arrays.
4. Grouping by UTC date is incorrect for exchanges whose session crosses UTC midnight. Air New Zealand's five trading days become six UTC "days", two of them partial. Grouping by `meta.exchangeTimezoneName` gives the right answer everywhere, at no cost for US symbols.

**Why I sent this prompt**

TODO

**What I kept, changed, or rejected**

TODO

---

## 5. Scaffolding the solution

**Prompt**

> generate boilerplate code

**What the AI produced**

Created the solution and five projects under `StockApp/` with the reference graph `Api -> Core, Infrastructure` and `Infrastructure -> Core`; Core references nothing. Deleted the template's `WeatherForecast` sample, `Class1.cs` placeholders and stub tests. Added `Directory.Build.props` (shared target framework, nullable reference types, implicit usings, warnings-as-errors) and `Directory.Packages.props` (Central Package Management, one version per package for the whole solution). Trimmed `Program.cs` to controllers plus OpenAPI, and added `public partial class Program;` so the integration test project can boot the real host. Verified: clean build with 0 warnings, host starts and serves `/openapi/v1.json`.

**Why I sent this prompt**

TODO

**What I kept, changed, or rejected**

TODO

---

## 6. Design review before implementation

**Prompt**

> Before writing code, propose a solution structure using clean architecture with a provider abstraction, typed HttpClient, and xUnit tests. Explain how you'll handle null, reexplain grouping by the exchange's timezone, and decimal rounding to 4 places.
>
> (Follow-ups:) We'll need to design it to ensure it Queries intraday data from the last month, Groups results by day, and Returns JSON in this format and precision listed on the assessment PDF so trailing zeros must be kept. / Your explanations should be more concise, human readable, and provide examples from my known skill set. / I am not familiar with the terminology NSubstitute and hand-written fakes. Elaborate. / Hand-written.

**What the AI produced**

A layer map (Core / Infrastructure / Api), the `IIntradayDataProvider` port, the typed `HttpClient` registration, a four-layer null policy (nullable reference types, nullable DTOs, drop incomplete bars at the mapper, `200 []` for a valid symbol with no data), and the rounding design. The trailing-zeros requirement was tested rather than assumed: `Math.Round(40.29m, 4)` serialises as `40.29`, so a `JsonConverter<decimal>` writing `ToString("F4")` is needed to emit `40.2900` — still as a JSON number, not a string. Also confirmed the UTC fallback needs to be visible to the frontend, via `X-Grouping-Timezone` headers rather than an envelope, because the PDF fixes the body as a bare array.

**Why I sent this prompt**

TODO

**What I kept, changed, or rejected**

Hand-written fakes over NSubstitute.

TODO: the rest.

---

## 7. Implementing the Core layer

**Prompt**

> (Continuing from the approved design: build Core — models, `Symbol`, the calculator, and their tests.)

**What the AI produced**

`Symbol` (validated and normalised, accepts `BRK-B` / `VOD.L` / `^GSPC` / `ES=F`), `IntradayBar`, `ExchangeTimeZone` (carries the reported IANA id and an `IsFallback` flag for the frontend warning), `IntradaySeries`, `DailySummary`, and `DailySummaryCalculator`. 23 xUnit tests, all passing; solution builds with 0 warnings.

Three things worth recording:

- **xUnit v2 → v3 (4.0.1).** On the .NET 10 SDK, VSTest is no longer supported by Microsoft Testing Platform, so `dotnet test` failed until the project opted in via `global.json` (`"test": { "runner": "Microsoft.Testing.Platform" }`). Test projects are now executables (`<OutputType>Exe</OutputType>`), and `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio` and `coverlet.collector` were dropped as unnecessary under the new runner.
- **`tests/.editorconfig`** disables CA1707 for test projects only, so test names can keep underscores (`Method_Scenario_ExpectedResult`) without failing the warnings-as-errors build.
- **The daylight-saving test initially failed, and the test was wrong, not the code.** The AI's expected dates were miscalculated. Verified against the runtime: `2026-11-01T04:30Z` in New York is `00:30 on 1 Nov` (still EDT, -4), while a fixed `-5` offset would give `23:30 on 31 Oct`. The test was rewritten to assert the correct behaviour, and it now demonstrates the exact trap it was written for.

**Why I sent this prompt**

TODO

**What I kept, changed, or rejected**

TODO
