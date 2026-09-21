# Stock Intraday Application

A self-hosted API and web UI that displays a month of intraday trading for any stock symbol.

The backend fetches 15-minute bars from Yahoo Finance, groups them into trading days, and returns
each day's average low, average high and total volume. The frontend lets you search a symbol. Using the table you may sort the results, and export them.

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

Note: Running the tests requires the .NET 8 runtime, included with the .NET 8 SDK.

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

Serves `http://localhost:5173`

## Running the tests

```bash
cd StockApp/backend
dotnet test
```

123 tests across three projects: the domain logic, the Yahoo adapter, and the HTTP contract.

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
| `rounding` | query | Optional. How prices are reduced to four decimal places: `AwayFromZero` (default), or `ToZero` to truncate. |

**Response headers** describe how the answer was assembled:

| Header | Meaning |
|---|---|
| `X-Grouping-Timezone` | The exchange clock the days were grouped on, e.g. `America/New_York`. |
| `X-Grouping-Timezone-Fallback` | `true` when that clock could not be resolved and UTC was used instead. Days may then split a trading session. |
| `X-Exchange-Name` | The exchange's display name, e.g. `NasdaqGS`. |

### Errors
| `errorCode` | Status | Meaning |
|---|---|---|
| `INVALID_SYMBOL` | 400 | The symbol is not a well-formed ticker. |
| `INVALID_ROUNDING` | 400 | The requested rounding strategy is not supported. |
| `SYMBOL_NOT_FOUND` | 404 | The data source has no such instrument. |
| `UPSTREAM_RATE_LIMITED` | 503 | The data source is throttling. `Retry-After` is passed through when supplied. |
| `UPSTREAM_UNAVAILABLE` | 502 | The data source could not be reached. Retrying may help. |
| `UPSTREAM_CONTRACT` | 502 | The data source API has changed. Retrying will not help. |

## Repository contents

- [`PROMPT_LOG.md`](PROMPT_LOG.md) — the AI prompts used to build this, why each was sent, and what
  was kept or rejected.
- [`StockApp/Technical-Documents/`](StockApp/Technical-Documents/) — the assessment brief and the UI
  wireframes the frontend was built to.
