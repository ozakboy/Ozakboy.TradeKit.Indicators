namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 不可變的 VWAP 輸入型別:一根 K 線的時間、高低收、成交量與(選填的)成交金額。
/// Immutable VWAP input: one bar's time, high, low, close, volume and (optionally) traded amount.
/// </summary>
/// <remarks>
/// <para>
/// <b>為什麼有 <paramref name="Amount" />。</b>有些資料來源直接提供每根的成交金額(價 × 數量 的精確加總),
/// 此時 VWAP 的分子應該用它,而不是用典型價去近似——典型價只是「這根的平均價」的粗估。
/// 來源沒提供時填 <see langword="null" />,指標會退回以典型價 × 成交量估算。
/// <b>Why <paramref name="Amount" />.</b> Some feeds publish the exact traded amount per bar (the true sum of
/// price × quantity). When it exists it is the correct numerator, whereas the typical price is only a rough
/// estimate of the bar's average price. Pass <see langword="null" /> when the feed has none and the indicator
/// falls back to typical price × volume.
/// </para>
/// <para>
/// <b>單位。</b><paramref name="Volume" /> 是呼叫端自己的單位(台股「張」、期貨「口」);
/// <paramref name="Amount" /> 是貨幣單位。兩者的換算係數由 <c>volumeMultiplier</c> 負責,見 <see cref="Vwap" />。
/// <b>Units.</b> <paramref name="Volume" /> is in the caller's own unit (lots for Taiwan stocks, contracts for
/// futures); <paramref name="Amount" /> is in currency. The conversion factor between them is the
/// <c>volumeMultiplier</c>; see <see cref="Vwap" />.
/// </para>
/// <para>
/// 本型別不驗證欄位(與 <see cref="Candle" /> 相同的立場);負的成交量或成交金額由指標在使用時拒絕。
/// This type performs no validation (same stance as <see cref="Candle" />); negative volume or amount is
/// rejected by the indicator when it is used.
/// </para>
/// </remarks>
/// <param name="Time">這根 K 線的時間,交給呼叫端提供的 <c>sessionOf</c> 判斷屬於哪個時段。Bar time, handed to the caller-supplied <c>sessionOf</c> to decide which session the bar belongs to.</param>
/// <param name="High">最高價。High price.</param>
/// <param name="Low">最低價。Low price.</param>
/// <param name="Close">收盤價。Close price.</param>
/// <param name="Volume">這根的成交量(呼叫端的單位,例如台股「張」、期貨「口」)。Traded volume of this bar, in the caller's unit (lots, contracts, ...).</param>
/// <param name="Amount">這根的成交金額(貨幣單位);<see langword="null" /> 表示來源沒有提供。Traded amount of this bar in currency; <see langword="null" /> when the source does not provide it.</param>
public readonly record struct VwapBar(
    DateTimeOffset Time,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume,
    decimal? Amount);
