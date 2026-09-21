# Stock Intraday Summary

A self-hosted API and web UI that summarise a month of intraday trading for any stock symbol.

The backend fetches 15-minute bars from Yahoo Finance, groups them into trading days, and returns
each day's average low, average high and total volume. The frontend lets you search a symbol, sort
the results, and export them.

```json
[
  {
    "day": "2026-09-18",
    "lowAverage": 362.8919,
    "highAverage": 364.9772,
    "volume": 41429596
  }
]
```

---

## Prerequisites

| | Version | Notes |
|---|---|---|
| .NET SDK | **8.0 or newer** | The solution targets `net8.0` and builds on any SDK from 8 upwards. Running the tests needs the .NET 8 runtime, which the .NET 8 SDK includes. |
| Node.js | **20 or newer** | Developed against Node 24. |

Verified on the .NET 8.0.425 and 10.0.401 SDKs.

## Running it locally

Two terminals, from the repository root.

**1 — the API**

```bash
cd StockApp/backend
dotnet run --project src/StockApp.Api --launch-profile http
```

Serves `http://localhost:5264`. Interactive API docs are at `http://localhost:5264/swagger`.

**2 — the web UI**

```bash
cd StockApp/frontend
npm install
npm run dev
```

Serves `http://localhost:5173`, and expects the API on its default port. Open it and search for a
symbol such as `TSLA`, `AIR.NZ` or `BRK-B`.

## Running the tests

All 116 tests, from the backend folder:

```bash
cd StockApp/backend
dotnet test
```

They run offline: the Yahoo adapter is tested against a saved response, and the HTTP tests boot the
real application in memory with the data source replaced by a stub. No network access, no API keys,
and nothing to configure first.

One project at a time:

```bash
dotnet test tests/StockApp.Core.Tests            # 23  domain logic
dotnet test tests/StockApp.Infrastructure.Tests  # 52  the Yahoo adapter
dotnet test tests/StockApp.Api.Tests             # 41  the HTTP contract
```

A single test or group, by name:

```bash
dotnet test tests/StockApp.Core.Tests --filter "FullyQualifiedName~Symbol"
dotnet test --filter "FullyQualifiedName~GroupsOnTheExchangeDate"
```

| Project | Covers |
|---|---|
| `StockApp.Core.Tests` | Grouping by exchange date, averaging, daylight-saving transitions, volume totals, symbol validation. |
| `StockApp.Infrastructure.Tests` | The outgoing request, every HTTP failure translated to a domain error, missing and malformed values, the time-zone fallback, and a captured live payload. |
| `StockApp.Api.Tests` | The published JSON byte for byte, the response headers, every error code, the rounding strategies, and the headers CORS exposes to a browser. |

**If the tests fail to start** with a message about `Microsoft.NETCore.App 8.0.0` not being found,
the .NET 8 runtime is missing. The API itself rolls forward onto a newer runtime, but the test host
is pinned to the version its testing packages were built for. Installing the .NET 8 SDK resolves it.

The frontend has no test suite yet; see *Known extension points*.

## Project layout

```
StockApp/
├── backend/
│   ├── src/
│   │   ├── StockApp.Core/            domain: models, aggregation, and the interfaces it depends on
│   │   ├── StockApp.Infrastructure/  the Yahoo Finance adapter
│   │   └── StockApp.Api/             controllers, contracts, error handling, configuration
│   └── tests/                        one test project per source project
├── frontend/                         React + TypeScript (Vite)
└── Technical-Documents/              the assessment brief and UI wireframes
```

`StockApp.Core` references no packages at all. The market data source is reached through an
interface the domain owns, so replacing Yahoo means writing one class and changing one registration.

---

## The API

### `GET /api/v1/stocks/{symbol}/daily`

Returns one entry per trading day for the last month, oldest first.

| Parameter | In | Description |
|---|---|---|
| `symbol` | path | The ticker, e.g. `TSLA`, `BRK-B`, `VOD.L`, `^GSPC`. Case-insensitive. |
| `rounding` | query | Optional. How prices are reduced to four decimal places: `AwayFromZero` (default), `ToEven`, or `ToZero` to truncate. |

**Response headers** describe how the answer was assembled:

| Header | Meaning |
|---|---|
| `X-Grouping-Timezone` | The exchange clock the days were grouped on, e.g. `America/New_York`. |
| `X-Grouping-Timezone-Fallback` | `true` when that clock could not be resolved and UTC was used instead. Days may then split a trading session. |
| `X-Exchange-Name` | The exchange's display name, e.g. `NasdaqGS`. |

### Errors

Every failure is an [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) problem document carrying a
stable `errorCode`, so a client branches on an identifier rather than on prose.

| `errorCode` | Status | Meaning |
|---|---|---|
| `INVALID_SYMBOL` | 400 | The symbol is not a well-formed ticker. |
| `INVALID_ROUNDING` | 400 | The requested rounding strategy is not supported. |
| `SYMBOL_NOT_FOUND` | 404 | The data source has no such instrument. |
| `UPSTREAM_RATE_LIMITED` | 503 | The data source is throttling. `Retry-After` is passed through when supplied. |
| `UPSTREAM_UNAVAILABLE` | 502 | The data source could not be reached. Retrying may help. |
| `UPSTREAM_CONTRACT` | 502 | The data source replied in a shape this API cannot interpret — its API has changed. Retrying will not help. |

The last two are deliberately distinguishable: both are 502s, but only one is worth retrying.

## Configuration

`StockApp/backend/src/StockApp.Api/appsettings.json`, overridable by environment variable using `__`
as the separator (`PricePrecision__Rounding=ToZero`).

| Setting | Default | Purpose |
|---|---|---|
| `YahooFinance:BaseUrl` | Yahoo's chart endpoint | Where market data is fetched from. |
| `YahooFinance:UserAgent` | a browser agent | Required: Yahoo answers requests without one with `429`. |
| `PricePrecision:DecimalPlaces` | `4` | Decimal places for published prices. |
| `PricePrecision:Rounding` | `AwayFromZero` | Default strategy, overridable per request. |
| `Cors:AllowedOrigins` | *(empty)* | Browser origins allowed to call the API. `http://localhost:5173` in development. |

Invalid settings stop the application at startup rather than producing wrong figures under traffic.

To point the UI at a different API, set `VITE_API_BASE_URL` before `npm run dev` or `npm run build`.

---

## Notes on the design

**Days are grouped on the exchange's clock, not UTC.** Yahoo returns UTC instants and the exchange's
IANA zone. For US symbols the two agree, so the distinction is invisible — but a session that crosses
UTC midnight, such as `AIR.NZ`, is otherwise split into two partial days whose averages describe
nothing. The conversion applies the offset in force at each instant, so a range spanning a
daylight-saving change is still grouped correctly.

**Prices are `decimal`, and rounding happens once, at the edge.** The domain keeps full precision;
reduction to four decimal places is a property of the published contract. The default rounds halves
away from zero, matching EU Regulation 1103/97 and Regulation DD, both of which round halves up and
neither of which permits truncating an intermediate value. Truncation is available for callers whose
counterparty requires it, but it is not the default: on real data it biases every published figure
low by roughly half a unit in the last place.

**Incomplete data is discarded, not defaulted.** Yahoo emits `null` prices for intervals with no
trading. Such a bar is dropped, because a zero would drag the day's average toward nothing. Every
bar that reaches the domain is complete, so nothing downstream checks for missing values.

**The upstream's shape stops at the adapter.** The types mirroring Yahoo's JSON are internal to the
infrastructure project and every field is nullable, because the wire format makes no promises. If
Yahoo changes its API, the result is a `502` with `UPSTREAM_CONTRACT` and a logged explanation, not
an unhandled error.

## Known extension points

Deliberately not built, since the brief is an MVP, but the structure accommodates them:

- **Caching.** 15-minute data does not change between requests; response caching would cut upstream
  calls and the rate-limiting risk substantially.
- **User-selectable ranges.** Interval and lookback are already domain enumerations rather than
  hardcoded query strings, so adding `5m` or `3mo` means extending an enum and a translation table.
- **A second data source.** `IIntradayDataProvider` is the only surface the domain knows.
- **Containerisation and CI.**

## Repository contents

- [`PROMPT_LOG.md`](PROMPT_LOG.md) — the AI prompts used to build this, why each was sent, and what
  was kept or rejected.
- [`StockApp/Technical-Documents/`](StockApp/Technical-Documents/) — the assessment brief and the UI
  wireframes the frontend was built to.
