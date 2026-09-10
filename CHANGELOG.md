# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] - 2026-09-11

Initial release.

### Added

- Eight technical indicators, each with a batch implementation and a constant-memory streaming implementation
  that produce identical results:
  - Simple Moving Average (`Sma` / `SmaIndicator`)
  - Exponential Moving Average (`Ema` / `EmaIndicator`), with a selectable seeding convention via `EmaSeedMode`
  - Relative Strength Index (`Rsi` / `RsiIndicator`), Wilder smoothing
  - Average True Range (`Atr` / `AtrIndicator`), Wilder smoothing
  - MACD (`Macd` / `MacdIndicator`), including signal line and histogram
  - Bollinger Bands (`BollingerBands` / `BollingerBandsIndicator`), configurable standard deviation multiplier
  - Donchian Channel (`DonchianChannel` / `DonchianChannelIndicator`)
  - Average Directional Index (`Adx` / `AdxIndicator`), including +DI and -DI
- `IIndicator` and `IIndicator<TInput, TOutput>` so a heterogeneous set of streaming indicators can be driven,
  warm-up checked and reset uniformly.
- `Candle`, an immutable OHLCV `readonly record struct`, plus `Candle.Of` for timestamp-free construction.
- `DecimalMath.Sqrt`, a Newton–Raphson square root for `decimal` seeded from a power-of-ten table, and
  `DecimalMath.TryDivide` for the zero-denominator cases that live markets produce.
- Explicit warm-up semantics across the whole library: every indicator returns `null` rather than `0` or an
  undefined value until it holds enough data, and reports readiness through `IsReady` and `WarmupPeriod`.
- Bilingual (Traditional Chinese and English) XML documentation on every public type and member, including the
  formula and typical usage for each indicator.

### Notes

- Targets `net10.0`. No third-party runtime dependencies; the only package reference is the build-only
  `Microsoft.SourceLink.GitHub`.
- All arithmetic is performed in `decimal`. `double` and `float` are not used anywhere.
- Batch and streaming results are asserted exactly equal, with no tolerance, across a range of periods for
  every indicator.

[Unreleased]: https://github.com/ozakboy/Ozakboy.TradeKit.Indicators/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/ozakboy/Ozakboy.TradeKit.Indicators/releases/tag/v0.1.0
