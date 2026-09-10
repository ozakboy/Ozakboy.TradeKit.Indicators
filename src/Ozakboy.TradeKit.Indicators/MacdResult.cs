namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// MACD 的三個輸出:快慢 EMA 差值(MACD 線)、其訊號線、以及兩者之差(柱狀圖)。
/// The three MACD outputs: the fast-minus-slow EMA difference (MACD line), its signal line, and the histogram.
/// </summary>
/// <param name="Macd">MACD 線 = 快線 EMA − 慢線 EMA。MACD line = fast EMA - slow EMA.</param>
/// <param name="Signal">訊號線 = MACD 線的 EMA。Signal line = EMA of the MACD line.</param>
/// <param name="Histogram">柱狀圖 = MACD 線 − 訊號線。Histogram = MACD line - signal line.</param>
public readonly record struct MacdResult(decimal Macd, decimal Signal, decimal Histogram);
