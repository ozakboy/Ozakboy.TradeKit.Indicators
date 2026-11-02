# Ozakboy.TradeKit.Indicators

.NET 技術指標函式庫。每一個指標都同時提供「批次計算」與「常數記憶體的增量計算」兩種版本。

零第三方相依,全程 `decimal` 運算。

[English](README.md)

## 為什麼有這個套件

多數指標函式庫是為回測設計的:你給它一個陣列,它回你一個陣列。這在即時交易引擎裡會出問題——每分鐘收一根 K 線,如果每次都把整段歷史重算一遍,成本會隨著程式跑多久而線性增長。

這裡的每個指標都有兩種形式,而且結果完全一致:

- **批次**:`Rsi.Calculate(prices, 14)` 回傳一個與輸入等長、索引對齊的序列,暖身期的位置是 `null`。
- **增量**:`new RsiIndicator(14).Update(price)` 只保留遞迴真正需要的狀態,回傳當下的值。

由此帶來三件事,也是自己刻之前值得先看一眼這裡的理由:

- **暖身期是明講的。**資料不足時一律回傳 `null`,不會是 `0`,也不會是任何未定義的數字。策略不可能在啟動初期不小心拿垃圾數值下單。
- **記憶體是常數。**增量指標佔用的空間只跟週期有關,跟程式已經跑了多久無關。EMA 這類遞迴指標只留前一個值,需要視窗的指標則用固定大小的環形緩衝區。
- **全程 `decimal`。**沒有 `double`、沒有 `float`,連布林通道標準差用到的平方根也是——BCL 只有浮點數版本,所以這裡自己實作了一個。

## 安裝

```
dotnet add package Ozakboy.TradeKit.Indicators
```

目標框架 `net10.0`。本函式庫沒有任何套件參照,不會在你的相依樹裡多加一條。

## 指標一覽

| 指標 | 批次 | 增量 | 輸出型別 | 產出第一個值所需筆數 |
| --- | --- | --- | --- | --- |
| 簡單移動平均 | `Sma.Calculate` | `SmaIndicator` | `decimal` | `n` |
| 指數移動平均 | `Ema.Calculate` | `EmaIndicator` | `decimal` | `n` |
| 相對強弱指標 | `Rsi.Calculate` | `RsiIndicator` | `decimal` | `n + 1` |
| 平均真實區間 | `Atr.Calculate` | `AtrIndicator` | `decimal` | `n` |
| MACD | `Macd.Calculate` | `MacdIndicator` | `MacdResult` | `慢線 + 訊號 − 1` |
| 布林通道 | `BollingerBands.Calculate` | `BollingerBandsIndicator` | `BollingerBandsResult` | `n` |
| 唐奇安通道 | `DonchianChannel.Calculate` | `DonchianChannelIndicator` | `DonchianChannelResult` | `n` |
| 平均趨向指標 | `Adx.Calculate` | `AdxIndicator` | `AdxResult` | `2n` |
| 成交量加權平均價 | `Vwap.Calculate` | `VwapIndicator` | `decimal` | 時段內第一根有成交量的 K 線 |
| KD 隨機指標 | `Stochastic.Calculate` | `StochasticIndicator` | `StochasticResult` | `n` |

RSI 要 `n + 1` 筆是因為 `n` 個價差需要 `n + 1` 個價格。ADX 要 `2n` 根是因為兩段 Wilder 平滑串在一起:第一段把方向動能平滑起來,第二段再對算出的 DX 取平均。

需要高低價的指標(ATR、唐奇安、ADX、KD)吃 `Candle`;其餘吃 `decimal` 價格,同時也接受 `Candle` 當作方便寫法,取其收盤價。VWAP 是例外:它吃 `VwapBar`,除了高低收之外還帶時間、成交量與選填的成交金額。

## VWAP

VWAP 每個交易時段重設一次。時段怎麼切由你決定:傳入 `sessionOf`,把每根 K 線的時間映射成一個日期,日期一變累計值就歸零重算。

```csharp
using Ozakboy.TradeKit.Indicators;

// 台股:時段就是台北的日曆日期。
static DateOnly TaipeiDate(DateTimeOffset time)
    => DateOnly.FromDateTime(time.ToOffset(TimeSpan.FromHours(8)).DateTime);

// Amount 是「價 × 股數」,Volume 卻是張(1 張 = 1,000 股),所以係數傳 1000。
// 期貨的 Amount 是「價 × 口」,傳 1。
VwapIndicator vwap = new(TaipeiDate, volumeMultiplier: 1000m);

DateTimeOffset open = new(2026, 10, 30, 9, 0, 0, TimeSpan.FromHours(8));

decimal? first = vwap.Update(new VwapBar(open, 2500m, 2480m, 2490m, 1859m, 4_628_910_000m));
decimal? second = vwap.Update(new VwapBar(open.AddMinutes(1), 2505m, 2490m, 2495m, 60m, 149_700_000m));
// first == 2490,second 約為 2490.1563

// 批次版參數相同,回傳與輸入索引對齊的序列。
IReadOnlyList<decimal?> series = Vwap.Calculate(bars, TaipeiDate, 1000m);
```

每根 K 線的貢獻方式:

- 分母加上 `Volume × volumeMultiplier`。
- 分子:這根有 `Amount` 就加 `Amount`;沒有就加典型價 `(High + Low + Close) / 3` 乘上 `Volume × volumeMultiplier`。同一個時段裡可以混用有、無成交金額的 K 線。
- 結果是分子除以分母。

邊界情形:

- 時段內還沒有任何成交量時回傳 `null`,`IsReady` 為 `false`。
- 時段內已經有值之後,某根成交量為 0,會沿用上一個值。
- 新時段不會繼承上一個時段的值。
- 成交量或成交金額為負數時丟出 `ArgumentOutOfRangeException`。VWAP 不檢查時間是否遞增,請先依時間排序。
- 期貨有夜盤時,請在 `sessionOf` 裡把夜盤時間映射到下一個交易日,夜盤才會與該日日盤一起累計。

## KD 隨機指標

KD 採用台股看盤軟體的平滑法,不是國外常見、以 SMA 平滑的 Slow Stochastic。預設參數 `(9, 3, 3)`。

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

// 批次版參數相同,回傳與輸入索引對齊的序列。
IReadOnlyList<StochasticResult?> series = Stochastic.Calculate(candles, period: 9, kSmoothing: 3, dSmoothing: 3);
```

- `RSV = (收盤價 − 最近 n 根最低價) / (最近 n 根最高價 − 最近 n 根最低價) × 100`,視窗含當根。
- `K = 前一期 K × (kSmoothing − 1) / kSmoothing + RSV / kSmoothing`,D 以 `dSmoothing` 對 K 做同樣的平滑。
- 第一個值出現在第 `n` 根,此時前一期的 K、D 都以 `50` 起算。平滑是遞迴的,起算的 K 線不同,前幾十根的值就會不同,之後才收斂;要和看盤軟體對數字,請從同一根 K 線開始算。

## 批次計算範例

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

// 結果與輸入索引對齊,暖身期是 null。
for (int i = 0; i < closes.Length; i++)
{
    string rsiText = rsi[i] is decimal r ? r.ToString("F2") : "--";
    string smaText = sma[i] is decimal s ? s.ToString("F2") : "--";
    Console.WriteLine($"[{i,2}] close={closes[i],7} sma={smaText,6} rsi={rsiText,6}");
}
```

## 增量計算範例

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

    // 只要還有任何一個沒暖身完成就不要下決策 —— 這些 null 是刻意的,不是漏寫。
    if (rsiValue is not decimal r || atrValue is not decimal a || macdValue is not MacdResult m)
    {
        continue;
    }

    Console.WriteLine($"rsi={r:F2} atr={a:F4} macd={m.Macd:F4} hist={m.Histogram:F4}");
}

static IEnumerable<Candle> ReadCandles()
{
    // 換成你自己的 K 線來源(交易所 WebSocket、資料庫、CSV⋯)。
    decimal price = 100m;
    for (int i = 0; i < 200; i++)
    {
        price += (i % 7) - 3;
        yield return Candle.Of(price, price + 1.5m, price - 1.5m, price + 0.4m);
    }
}
```

`Candle.Of(open, high, low, close, volume)` 建立一根不帶時間戳記的 K 線。想把開盤時間一起帶著走就用完整建構式:`new Candle(openTime, open, high, low, close, volume)`。

## 用統一的方式驅動指標

每個增量指標都實作 `IIndicator`,所以一組型別各異的指標可以共用同一道暖身閘門:

```csharp
List<IIndicator> indicators = [rsi, atr, macd];

if (indicators.TrueForAll(indicator => indicator.IsReady))
{
    // 全部就緒,可以開始評估策略。
}
```

`IIndicator` 另外提供 `Name`(例如 `"MACD(12,26,9)"`)、`WarmupPeriod`、`ProcessedCount` 與 `Reset()`。`Reset()` 會讓實例回到剛建立的狀態,這正是資料流中斷或換標的之後該做的事。

## 值得先知道的取捨

各家看盤軟體對指標的定義不盡相同。以下是本函式庫的選擇,每一項在對應型別的文件註解裡也都有寫:

- **EMA 的種子**可以用 `EmaSeedMode` 切換。預設 `SimpleMovingAverage` 取前 `n` 筆的平均,與 Wilder 原著、StockCharts 一致;`FirstValue` 直接拿第一筆當種子,與多數即時看盤軟體一致。MACD 會把這個選擇傳給它底下的三條 EMA。
- **布林通道的標準差用母體公式**(除以 `n`,不是 `n − 1`),與 Bollinger 原著相同。改用樣本公式在 `n = 20` 時通道會寬約 2.6%。
- **唐奇安通道含當根 K 線。**海龜那類「突破前 N 根高點才進場」的規則,要拿**前一根**的通道值來比——先讀 `Current` 再呼叫 `Update`。
- **ATR 第一根的真實區間取「高 − 低」**,因為第一根沒有前一根的收盤價。

### 除以零

這些不是理論邊界,真實行情每天都會製造出來。

- RSI 在平均跌幅為 `0`、平均漲幅大於 `0` 時回傳 `100`,這是公認慣例;平均漲幅為 `0` 時回傳 `0`。
- RSI 在**整段完全沒有價格變動**、兩個平均同時為 `0` 時回傳 `100`。標準公式只定義了單邊為 `0` 的兩種情形,這個邊界沒有公認答案;本函式庫刻意跟隨業界慣例——多數實作的除以零分支只看分母(平均跌幅),平均跌幅為 `0` 就回傳 `100`,持平序列自然落在同一條分支。代價是「毫無波動」看起來像「極度超買」,策略若需要分辨這兩者,請自行檢查價格是否有變動(例如同時觀察 ATR),不要只看 RSI。
- ADX 在平滑真實區間為 `0`、或 `+DI + −DI` 為 `0` 時回報 `0`:沒有區間,自然也沒有方向性。
- KD 在視窗最高價等於最低價時,RSV 以 `50`(區間正中央)代替,所以整段完全持平時 K、D 會一直維持 `50`。

## 準確度

測試的期望值來自三層獨立來源:逐字引用自 StockCharts ChartSchool 與 Wilder《New Concepts in Technical Trading Systems》(1978) 的公式;Wilder 自己那組 RSI 範例資料公開轉載的 70.53 對照值;以及一份用另一種語言、直接照那些公式文字另外寫的參考實作。每個指標另有一組小到可以用計算機驗算的案例。完整出處(含網址)寫在 `tests/Ozakboy.TradeKit.Indicators.Tests/GoldenVectorProvenance.cs`。

批次版與增量版的結果對每個指標、在多組週期下都以**完全相等**斷言,不設容差。它們是同一套算術、同樣的運算順序,對不上就是有 bug,不是精度問題。

`DecimalMath.Sqrt` 用牛頓法,初始猜測值取自 10 的冪次查表。以約 34 萬個涵蓋所有 decimal scale 的輸入實測,全部在 8 次迭代內收斂,完全平方數回傳精確值。

## 授權

MIT,見 [LICENSE](LICENSE)。
