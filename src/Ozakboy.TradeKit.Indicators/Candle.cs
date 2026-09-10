namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 不可變的 K 線(OHLCV)輸入型別,所有價量欄位皆為 <see cref="decimal" />。
/// Immutable OHLCV candle input; every price and volume field is a <see cref="decimal" />.
/// </summary>
/// <remarks>
/// <para>
/// 本型別刻意不驗證欄位之間的關係(例如 High ≥ Low),因為交易所偶爾會送出異常 K 線,
/// 由呼叫端決定要丟棄還是照收;指標本身對任何輸入都能給出定義明確的結果。
/// This type intentionally performs no cross-field validation (such as High ≥ Low): exchanges
/// occasionally publish anomalous candles and it is the caller's decision whether to drop them.
/// </para>
/// <para>
/// <see cref="OpenTime" /> 不參與任何指標計算,只是讓呼叫端能把時間資訊一起帶著走。
/// <see cref="OpenTime" /> takes no part in any indicator calculation; it is carried for the caller's convenience.
/// </para>
/// </remarks>
/// <param name="OpenTime">K 線開盤時間。Candle open time.</param>
/// <param name="Open">開盤價。Open price.</param>
/// <param name="High">最高價。High price.</param>
/// <param name="Low">最低價。Low price.</param>
/// <param name="Close">收盤價。Close price.</param>
/// <param name="Volume">成交量。Traded volume.</param>
public readonly record struct Candle(
    DateTimeOffset OpenTime,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume)
{
    /// <summary>
    /// 建立一根不帶時間戳記的 K 線,方便測試與不關心時間的情境。
    /// Creates a candle without a timestamp, convenient for tests and time-agnostic scenarios.
    /// </summary>
    /// <param name="open">開盤價。Open price.</param>
    /// <param name="high">最高價。High price.</param>
    /// <param name="low">最低價。Low price.</param>
    /// <param name="close">收盤價。Close price.</param>
    /// <param name="volume">成交量,預設 0。Traded volume, defaults to 0.</param>
    /// <returns>對應的 <see cref="Candle" />。The resulting <see cref="Candle" />.</returns>
    public static Candle Of(decimal open, decimal high, decimal low, decimal close, decimal volume = 0m)
        => new(default, open, high, low, close, volume);
}
