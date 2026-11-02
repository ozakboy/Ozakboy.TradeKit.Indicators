# Ozakboy.TradeKit.Indicators

Technical indicators for .NET, with a batch implementation and a constant-memory streaming implementation of every indicator.

Zero third-party dependencies. All arithmetic in `decimal`.

[繁體中文說明](README_zh-TW.md)

## Why this exists

Most indicator libraries are built for backtesting: you hand them an array and they hand back an array. That breaks down in a live trading engine, where a new candle closes every minute and recomputing the whole history each time makes cost grow with uptime.

Every indicator here comes in two forms that produce identical results:

- **Batch** — `Rsi.Calculate(prices, 14)` returns a list the same length as the input, index-aligned, with `null` in the warm-up positions.
- **Streaming** — `new RsiIndicator(14).Update(price)` keeps only the state the recursion actually needs and returns the current value.

Three things follow from that split, and they are the reason to prefer this over rolling your own:

- **Warm-up is explicit.** Until an indicator has enough data it returns `null` — never `0`, never an undefined number. A strategy cannot accidentally trade on a garbage reading during start-up.
- **Memory is constant.** A streaming indicator's footprint depends on its period, not on how long the process has been running. EMA-family indicators keep a single previous value; window indicators keep a fixed ring buffer.
- **`decimal` throughout.** No `double`, no `float`, including the square root behind Bollinger's standard deviation, which is implemented here because the BCL only ships a binary-floating-point one.

## Install

```
dotnet add package Ozakboy.TradeKit.Indicators
```

Targets `net10.0`. The library has no package reference of any kind — nothing is added to your dependency tree.

## Indicators

| Indicator | Batch | Streaming | Output | Inputs needed before first value |
| --- | --- | --- | --- | --- |
| Simple Moving Average | `Sma.Calculate` | `SmaIndicator` | `decimal` | `n` |
| Exponential Moving Average | `Ema.Calculate` | `EmaIndicator` | `decimal` | `n` |
| Relative Strength Index | `Rsi.Calculate` | `RsiIndicator` | `decimal` | `n + 1` |
| Average True Range | `Atr.Calculate` | `AtrIndicator` | `decimal` | `n` |
| MACD | `Macd.Calculate` | `MacdIndicator` | `MacdResult` | `slow + signal - 1` |
| Bollinger Bands | `BollingerBands.Calculate` | `BollingerBandsIndicator` | `BollingerBandsResult` | `n` |
| Donchian Channel | `DonchianChannel.Calculate` | `DonchianChannelIndicator` | `DonchianChannelResult` | `n` |
| Average Directional Index | `Adx.Calculate` | `AdxIndicator` | `AdxResult` | `2n` |
| Volume-Weighted Average Price | `Vwap.Calculate` | `VwapIndicator` | `decimal` | first bar of a session with volume |
| Stochastic (KD) | `Stochastic.Calculate` | `StochasticIndicator` | `StochasticResult` | `n` |

RSI needs `n + 1` prices because `n` price changes require `n + 1` prices. ADX needs `2n` candles because two Wilder smoothing stages stack: the first produces smoothed directional movement, the second averages the resulting DX readings.

Indicators that need highs and lows (ATR, Donchian, ADX, KD) take `Candle`. The rest take a `decimal` price, and also accept `Candle` as a convenience — they use its close. VWAP is the exception: it takes `VwapBar`, which adds a time, a volume and an optional traded amount to the high, low and close.

## VWAP

VWAP resets at every trading session. You decide where a session ends by passing `sessionOf`, which maps a bar's time to a date. The sums restart whenever that date changes.

```csharp
using Ozakboy.TradeKit.Indicators;

// Taiwan stocks: the session is the Taipei calendar date.
static DateOnly TaipeiDate(DateTimeOffset time)
    => DateOnly.FromDateTime(time.ToOffset(TimeSpan.FromHours(8)).DateTime);

// Amount is "price x shares" but Volume counts lots of 1,000 shares, so the multiplier is 1000.
// Pass 1 for futures, where Amount is "price x contracts".
VwapIndicator vwap = new(TaipeiDate, volumeMultiplier: 1000m);

DateTimeOffset open = new(2026, 10, 30, 9, 0, 0, TimeSpan.FromHours(8));

decimal? first = vwap.Update(new VwapBar(open, 2500m, 2480m, 2490m, 1859m, 4_628_910_000m));
decimal? second = vwap.Update(new VwapBar(open.AddMinutes(1), 2505m, 2490m, 2495m, 60m, 149_700_000m));
// first == 2490, second is about 2490.1563

// The batch form takes the same arguments and returns an index-aligned list.
IReadOnlyList<decimal?> series = Vwap.Calculate(bars, TaipeiDate, 1000m);
```

How each bar contributes:

- The denominator gains `Volume x volumeMultiplier`.
- The numerator gains `Amount` when the bar has one. Otherwise it gains the typical price `(High + Low + Close) / 3` times `Volume x volumeMultiplier`. You can mix bars with and without an amount in one session.
- The result is numerator divided by denominator.

Edge cases:

- While the session has traded no volume yet, VWAP returns `null`, and `IsReady` is `false`.
- After the session has a value, a bar with zero volume repeats the previous value.
- A new session never inherits the previous session's value.
- A negative volume or amount throws `ArgumentOutOfRangeException`. VWAP does not check that bar times ascend, so sort your bars first.
- For futures with a night session, map the night bars to the next trading day inside `sessionOf` so they accumulate together with that day's session.

## KD (Stochastic)

KD follows the smoothing that Taiwan charting software uses, not the SMA-smoothed "slow stochastic" common elsewhere. The defaults are `(9, 3, 3)`.

```csharp
using Ozakboy.TradeKit.Indicators;

StochasticIndicator kd = new();            // KD(9,3,3)

foreach (Candle candle in candles)
{
    if (kd.Update(candle) is StochasticResult value)
    {
        Console.WriteLine($"K={value.K:F2} D={value.D:F2}");
    }
}

// The batch form takes the same parameters and returns an index-aligned list.
IReadOnlyList<StochasticResult?> series = Stochastic.Calculate(candles, period: 9, kSmoothing: 3, dSmoothing: 3);
```

- `RSV = (close - lowest low of the last n candles) / (highest high - lowest low) x 100`, including the current candle.
- `K = previous K x (kSmoothing - 1) / kSmoothing + RSV / kSmoothing`, and D smooths K the same way with `dSmoothing`.
- The first value arrives on the `n`-th candle, where the previous K and D are both taken as `50`. Because the smoothing is recursive, a different starting candle changes the first few dozen values before they converge. To match a charting package, start from the same candle it does.

## Batch example

```csharp
using Ozakboy.TradeKit.Indicators;

decimal[] closes =
[
    44.34m, 44.09m, 44.15m, 43.61m, 44.33m, 44.83m, 45.10m, 45.42m,
    45.84m, 46.08m, 45.89m, 46.03m, 45.61m, 46.28m, 46.28m, 46.00m,
    46.03m, 46.41m, 46.22m, 45.64m, 46.21m, 46.25m, 45.71m, 46.45m,
];

IReadOnlyList<decimal?> rsi = Rsi.Calculate(closes, 14);
IReadOnlyList<decimal?> sma = Sma.Calculate(closes, 10);

// Results are index-aligned with the input; warm-up positions are null.
for (int i = 0; i < closes.Length; i++)
{
    string rsiText = rsi[i] is decimal r ? r.ToString("F2") : "--";
    string smaText = sma[i] is decimal s ? s.ToString("F2") : "--";
    Console.WriteLine($"[{i,2}] close={closes[i],7} sma={smaText,6} rsi={rsiText,6}");
}
```

## Streaming example

```csharp
using Ozakboy.TradeKit.Indicators;

RsiIndicator rsi = new(14);
AtrIndicator atr = new(14);
MacdIndicator macd = new();

foreach (Candle candle in ReadCandles())
{
    decimal? rsiValue = rsi.Update(candle);
    decimal? atrValue = atr.Update(candle);
    MacdResult? macdValue = macd.Update(candle);

    // Do not act while anything is still warming up; the nulls are deliberate, not omissions.
    if (rsiValue is not decimal r || atrValue is not decimal a || macdValue is not MacdResult m)
    {
        continue;
    }

    Console.WriteLine($"rsi={r:F2} atr={a:F4} macd={m.Macd:F4} hist={m.Histogram:F4}");
}

static IEnumerable<Candle> ReadCandles()
{
    // Replace with your candle source: exchange WebSocket, database, CSV, and so on.
    decimal price = 100m;
    for (int i = 0; i < 200; i++)
    {
        price += (i % 7) - 3;
        yield return Candle.Of(price, price + 1.5m, price - 1.5m, price + 0.4m);
    }
}
```

`Candle.Of(open, high, low, close, volume)` builds a candle without a timestamp. Use the full constructor when you want to carry the open time along: `new Candle(openTime, open, high, low, close, volume)`.

## Driving indicators uniformly

Every streaming indicator implements `IIndicator`, so a heterogeneous set can share one warm-up gate:

```csharp
List<IIndicator> indicators = [rsi, atr, macd];

if (indicators.TrueForAll(indicator => indicator.IsReady))
{
    // Everything is ready; the strategy may run.
}
```

`IIndicator` also exposes `Name` (for example `"MACD(12,26,9)"`), `WarmupPeriod`, `ProcessedCount` and `Reset()`. `Reset()` returns an instance to its freshly-constructed state, which is what you want after a gap in the feed or a symbol switch.

## Conventions worth knowing

Indicator definitions differ between charting packages. These are the choices this library makes, all of them documented on the types themselves:

- **EMA seeding** is configurable via `EmaSeedMode`. The default, `SimpleMovingAverage`, seeds from the mean of the first `n` inputs, matching Wilder and StockCharts. `FirstValue` seeds from the first input, matching many live charting tools. MACD passes its choice down to all three of its EMAs.
- **Bollinger's standard deviation is the population form** (divided by `n`, not `n - 1`), as in Bollinger's original work. The sample form would widen the bands by roughly 2.6% at `n = 20`.
- **Donchian includes the current candle.** Breakout rules of the Turtle kind should compare price against the *previous* bar's channel — read `Current` before calling `Update`.
- **ATR's first true range is `high - low`**, since the first candle has no previous close.

### Division by zero

These are not theoretical corners; live markets produce them regularly.

- RSI with an average loss of `0` and a positive average gain returns `100`, the standard convention. Average gain of `0` returns `0`.
- RSI on a **completely flat window**, where both averages are `0`, returns `100`. The standard formula defines only the two single-sided zero cases, so this boundary has no agreed answer; the library deliberately follows the common convention, where the zero-denominator branch keys on the average loss alone and a flat series therefore falls into it. The cost is that a motionless market reads as extremely overbought, so a strategy that needs to tell the two apart should check whether the price moved at all — for example by watching ATR alongside the RSI.
- ADX with a smoothed true range of `0`, or with `+DI + -DI` equal to `0`, reports `0` — no range, therefore no directionality.
- KD on a window whose highest high equals its lowest low uses an RSV of `50`, the middle of the range. A completely flat series therefore keeps K and D at `50`.

## Accuracy

Expected values in the test suite come from three independent layers: formulas quoted verbatim from StockCharts ChartSchool and Wilder's *New Concepts in Technical Trading Systems* (1978); the published 70.53 anchor for Wilder's own RSI example data; and a reference implementation written separately in another language directly from those formula texts. Each indicator additionally has a case small enough to check on a calculator. The full provenance, including the URLs, is in `tests/Ozakboy.TradeKit.Indicators.Tests/GoldenVectorProvenance.cs`.

Batch and streaming results are asserted **exactly equal** for every indicator across a range of periods — no tolerance. They are the same arithmetic in the same order, so a mismatch would be a bug, not a rounding artifact.

`DecimalMath.Sqrt` uses Newton–Raphson seeded from a power-of-ten table. Measured across roughly 340,000 inputs spanning every decimal scale, it converged within 8 iterations in every case, and returns perfect squares exactly.

## License

MIT. See [LICENSE](LICENSE).
