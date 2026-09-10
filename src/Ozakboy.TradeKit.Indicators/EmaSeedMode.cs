namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// EMA 遞迴的起始值取法。不同軟體採用不同慣例,兩者在數十根 K 線之後會收斂到幾乎相同的數值,
/// 但暖身期附近差異明顯,因此明確開放給呼叫端選擇。
/// How the EMA recursion is seeded. Charting packages disagree on this; the two conventions converge
/// after a few dozen bars but differ visibly around warm-up, so the choice is made explicit.
/// </summary>
public enum EmaSeedMode
{
    /// <summary>
    /// 以前 N 筆的簡單移動平均作為種子,第 N 筆(索引 N-1)才產出第一個值。
    /// 這是 StockCharts、Wilder 原著與多數技術分析教材採用的慣例,本函式庫的預設值。
    /// Seeds with the simple moving average of the first N inputs, emitting the first value at input N
    /// (index N-1). This is the convention used by StockCharts, Wilder's original work and most
    /// textbooks, and is this library's default.
    /// </summary>
    SimpleMovingAverage = 0,

    /// <summary>
    /// 以第一筆輸入作為種子,遞迴自第一筆開始;仍然要等到累積 N 筆才視為就緒。
    /// 多數即時看盤軟體採用此法,好處是不需要先攢滿一段資料才能開始遞迴。
    /// Seeds with the first input and starts recursing immediately, while still reporting ready only
    /// after N inputs. Many live charting tools use this because the recursion needs no initial batch.
    /// </summary>
    FirstValue = 1,
}
