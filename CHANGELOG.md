# Changelog

本檔記錄本套件所有值得注意的變更。
All notable changes to this package are documented here.

格式依循 [Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/),版號依循
[語意化版本](https://semver.org/lang/zh-TW/)。
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the versioning follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### 文件 / Documentation

- CHANGELOG 改為中英雙語(先中後英),與 ozakboy 套件家族其他 repo 一致;既有條目的英文原文未動,
  只補上對應的中文。
  The changelog is now bilingual (Traditional Chinese first, then English), matching the other repositories in
  the ozakboy package family. The existing English text is unchanged; the Chinese was added alongside it.
- csproj 補上 `PackageReleaseNotes`,nuget.org 的套件頁面從此看得到 0.1.0 與 0.1.1 的摘要,
  不必再點進 repo 找 CHANGELOG。
  `PackageReleaseNotes` is added to the project file, so the package page on nuget.org shows a summary of 0.1.0
  and 0.1.1 without a detour to the repository's changelog.

## [0.1.1] - 2026-09-11

### 新增功能 / Added

- 套件圖示。ozakboy 套件家族共用的品牌圖示,會顯示在 nuget.org 與 IDE 的套件管理員。
  Package icon. The shared ozakboy brand mark now shows on nuget.org and in IDE package managers.

本版沒有任何程式碼變更。NuGet 已發佈版本的套件中繼資料不可更動,要更新圖示只能發新版。
No code changed in this release. NuGet package metadata cannot be altered on an already-published
version, so refreshing the icon requires publishing a new one.

## [0.1.0] - 2026-09-11

首個版本。
Initial release.

### 新增功能 / Added

- 八個技術指標,每一個都同時提供批次實作與常數記憶體的增量(streaming)實作,兩者結果完全一致:
  Eight technical indicators, each with a batch implementation and a constant-memory streaming implementation
  that produce identical results:
  - 簡單移動平均 / Simple Moving Average(`Sma` / `SmaIndicator`)
  - 指數移動平均 / Exponential Moving Average(`Ema` / `EmaIndicator`),可透過 `EmaSeedMode` 選擇起始值慣例
    / with a selectable seeding convention via `EmaSeedMode`
  - 相對強弱指標 / Relative Strength Index(`Rsi` / `RsiIndicator`),Wilder 平滑 / Wilder smoothing
  - 平均真實區間 / Average True Range(`Atr` / `AtrIndicator`),Wilder 平滑 / Wilder smoothing
  - MACD(`Macd` / `MacdIndicator`),含訊號線與柱狀圖 / including signal line and histogram
  - 布林通道 / Bollinger Bands(`BollingerBands` / `BollingerBandsIndicator`),標準差倍數可設定
    / configurable standard deviation multiplier
  - 唐奇安通道 / Donchian Channel(`DonchianChannel` / `DonchianChannelIndicator`)
  - 平均趨向指標 / Average Directional Index(`Adx` / `AdxIndicator`),含 +DI 與 -DI / including +DI and -DI
- `IIndicator` 與 `IIndicator<TInput, TOutput>`:讓一組型別各異的增量指標能以統一的方式餵資料、檢查暖身與重設。
  `IIndicator` and `IIndicator<TInput, TOutput>` so a heterogeneous set of streaming indicators can be driven,
  warm-up checked and reset uniformly.
- `Candle`:不可變的 OHLCV `readonly record struct`,另有 `Candle.Of` 供不帶時間戳的建構。
  `Candle`, an immutable OHLCV `readonly record struct`, plus `Candle.Of` for timestamp-free construction.
- `DecimalMath.Sqrt`:以十的冪次表起始的 `decimal` 牛頓法平方根;`DecimalMath.TryDivide`:處理實際行情會出現的
  分母為零的情況。
  `DecimalMath.Sqrt`, a Newton–Raphson square root for `decimal` seeded from a power-of-ten table, and
  `DecimalMath.TryDivide` for the zero-denominator cases that live markets produce.
- 整個函式庫的暖身語意都是明講的:每個指標在資料不足時一律回傳 `null`,不是 `0` 也不是任何未定義的值,
  並透過 `IsReady` 與 `WarmupPeriod` 回報就緒狀態。
  Explicit warm-up semantics across the whole library: every indicator returns `null` rather than `0` or an
  undefined value until it holds enough data, and reports readiness through `IsReady` and `WarmupPeriod`.
- 所有公開型別與成員都有中英雙語(繁體中文與英文)的 XML 文件,含每個指標的公式與典型用法。
  Bilingual (Traditional Chinese and English) XML documentation on every public type and member, including the
  formula and typical usage for each indicator.

### 備註 / Notes

- 目標框架 `net10.0`。函式庫專案完全沒有 `PackageReference`:Source Link 由 .NET SDK 本身提供
  (`PublishRepositoryUrl` 與 `EmbedUntrackedSources`),不需要額外套件。
  Targets `net10.0`. The library project has no `PackageReference` at all: Source Link is provided by the .NET
  SDK itself (`PublishRepositoryUrl` and `EmbedUntrackedSources`), so no package is needed for it.
- RSI 在完全持平的視窗(平均漲幅與平均跌幅同為 `0`)回傳 `100`。標準公式只定義了單邊為零的情況,
  這個邊界是刻意的選擇,與多數技術分析函式庫的結果一致;理由見 `RsiIndicator` 的文件。
  RSI on a completely flat window, where the average gain and the average loss are both `0`, returns `100`.
  The standard formula only defines the single-sided zero cases, so this boundary is a deliberate choice made
  to match what most technical-analysis libraries return; see the `RsiIndicator` documentation for the reasoning.
- 所有運算都以 `decimal` 進行,任何地方都不使用 `double` 與 `float`。
  All arithmetic is performed in `decimal`. `double` and `float` are not used anywhere.
- 每個指標在多種週期下,批次與增量的結果都以完全相等斷言,沒有容差。
  Batch and streaming results are asserted exactly equal, with no tolerance, across a range of periods for
  every indicator.

[Unreleased]: https://github.com/ozakboy/Ozakboy.TradeKit.Indicators/compare/v0.1.1...HEAD
[0.1.1]: https://github.com/ozakboy/Ozakboy.TradeKit.Indicators/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/ozakboy/Ozakboy.TradeKit.Indicators/releases/tag/v0.1.0
