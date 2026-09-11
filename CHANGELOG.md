# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.1] - 2026-09-11

### Added

- Package icon. The shared ozakboy brand mark now shows on nuget.org and in IDE package managers.

No code changed in this release. NuGet package metadata cannot be altered on an already-published
version, so refreshing the icon requires publishing a new one.

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

- Targets `net10.0`. The library project has no `PackageReference` at all: Source Link is provided by the .NET
  SDK itself (`PublishRepositoryUrl` and `EmbedUntrackedSources`), so no package is needed for it.
- RSI on a completely flat window, where the average gain and the average loss are both `0`, returns `100`.
  The standard formula only defines the single-sided zero cases, so this boundary is a deliberate choice made
  to match what most technical-analysis libraries return; see the `RsiIndicator` documentation for the reasoning.
- All arithmetic is performed in `decimal`. `double` and `float` are not used anywhere.
- Batch and streaming results are asserted exactly equal, with no tolerance, across a range of periods for
  every indicator.

[Unreleased]: https://github.com/ozakboy/Ozakboy.TradeKit.Indicators/compare/v0.1.1...HEAD
[0.1.1]: https://github.com/ozakboy/Ozakboy.TradeKit.Indicators/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/ozakboy/Ozakboy.TradeKit.Indicators/releases/tag/v0.1.0
