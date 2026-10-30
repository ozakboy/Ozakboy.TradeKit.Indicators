namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 成交量加權平均價(VWAP)的批次計算。
/// Batch calculation of the Volume-Weighted Average Price (VWAP).
/// </summary>
/// <remarks>
/// <para>
/// <b>公式。</b>每個時段內累計:<c>分母 += Volume × volumeMultiplier</c>;
/// <c>分子 += Amount ?? (典型價 × Volume × volumeMultiplier)</c>,其中典型價 = (High + Low + Close) / 3;
/// 輸出 <c>分子 / 分母</c>。
/// <b>Formula.</b> Within each session accumulate <c>den += Volume × volumeMultiplier</c> and
/// <c>num += Amount ?? (typical price × Volume × volumeMultiplier)</c>, where typical price = (High + Low + Close) / 3;
/// the output is <c>num / den</c>.
/// </para>
/// <para>
/// <b>時段重設。</b><c>sessionOf(bar.Time)</c> 與上一根不同時,分子分母先歸零再累加這一根。
/// 時段怎麼切由呼叫端決定:台股傳台北日期;期貨夜盤可把夜盤時間映射到下一個交易日。
/// <b>Session reset.</b> When <c>sessionOf(bar.Time)</c> differs from the previous bar's, both sums are zeroed
/// before this bar is added. The caller decides how sessions are cut: pass the Taipei date for Taiwan stocks;
/// for futures, map the night session onto the next trading day.
/// </para>
/// <para>
/// <b>單位換算。</b>台股的 <c>Amount</c> 是「價 × 股數」而 <c>Volume</c> 是張,所以呼叫端傳
/// <c>volumeMultiplier = 1000</c>;期貨的 <c>Amount</c> 是「價 × 口」,傳 1。
/// <b>Unit conversion.</b> For Taiwan stocks <c>Amount</c> is "price × shares" while <c>Volume</c> is in lots, so the
/// caller passes <c>volumeMultiplier = 1000</c>; for futures <c>Amount</c> is "price × contracts" and 1 is passed.
/// </para>
/// <para>
/// <b>暖身與零量。</b>時段開始後分母仍為 0(前幾根都沒量)時輸出 <see langword="null" />;
/// 時段內已經有值之後某根量為 0,輸出沿用上一個值(不是 <see langword="null" />、也不是 0)。
/// <b>Warm-up and zero volume.</b> While the denominator is still 0 after a session starts (the first bars traded
/// nothing) the output is <see langword="null" />; once the session has a value, a later zero-volume bar repeats
/// the previous value (neither <see langword="null" /> nor 0).
/// </para>
/// <para>
/// <b>輸入檢查。</b>與其他指標一致,只在引數層級以 <see cref="ArgumentNullException" /> /
/// <see cref="ArgumentOutOfRangeException" /> 拒絕非法輸入;另外因為負的成交量或成交金額會讓累計值失去意義,
/// 遇到時同樣丟出 <see cref="ArgumentOutOfRangeException" />。不檢查時間是否遞增:呼叫端負責依時間排序。
/// <b>Input checks.</b> As with the other indicators, invalid input is rejected only at the argument level, with
/// <see cref="ArgumentNullException" /> / <see cref="ArgumentOutOfRangeException" />; because a negative volume or
/// amount would make the running sums meaningless, those also throw <see cref="ArgumentOutOfRangeException" />.
/// Time order is not checked: the caller is responsible for sorting by time.
/// </para>
/// </remarks>
/// <seealso cref="VwapIndicator" />
public static class Vwap
{
    /// <summary>
    /// 計算整段 K 線序列的 VWAP。
    /// Calculates the VWAP over an entire bar series.
    /// </summary>
    /// <param name="bars">依時間先後排列的 K 線。Bars in chronological order.</param>
    /// <param name="sessionOf">把 K 線時間映射到所屬時段(日期)的函式;時段變了就重設累計。Maps a bar time to its session (a date); accumulation resets whenever the session changes.</param>
    /// <param name="volumeMultiplier">把 <c>Volume</c> 換算成與 <c>Amount</c> 同單位數量的係數,必須大於 0。Factor converting <c>Volume</c> into the quantity unit behind <c>Amount</c>; must be greater than 0.</param>
    /// <returns>與輸入等長、索引對齊的結果;時段內尚無成交量的位置為 <see langword="null" />。An index-aligned list the same length as the input, with <see langword="null" /> wherever the session has no volume yet.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bars" /> 或 <paramref name="sessionOf" /> 為 <see langword="null" />。<paramref name="bars" /> or <paramref name="sessionOf" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="volumeMultiplier" /> 小於或等於 0,或任一根的 <c>Volume</c> / <c>Amount</c> 為負數。<paramref name="volumeMultiplier" /> is less than or equal to 0, or any bar has a negative <c>Volume</c> / <c>Amount</c>.</exception>
    public static IReadOnlyList<decimal?> Calculate(
        IReadOnlyList<VwapBar> bars,
        Func<DateTimeOffset, DateOnly> sessionOf,
        decimal volumeMultiplier = 1m)
    {
        ArgumentNullException.ThrowIfNull(bars);
        ArgumentNullException.ThrowIfNull(sessionOf);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(volumeMultiplier);

        decimal?[] results = new decimal?[bars.Count];
        DateOnly? session = null;
        decimal numerator = 0m;
        decimal denominator = 0m;
        decimal? last = null;

        for (int i = 0; i < bars.Count; i++)
        {
            VwapBar bar = bars[i];

            ArgumentOutOfRangeException.ThrowIfNegative(bar.Volume);
            if (bar.Amount is decimal amount)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(amount);
            }

            DateOnly barSession = sessionOf(bar.Time);
            if (session != barSession)
            {
                // 換時段:累計值與上一個輸出都不能帶過來。
                // New session: neither the sums nor the previous output may carry over.
                session = barSession;
                numerator = 0m;
                denominator = 0m;
                last = null;
            }

            // 運算順序與 VwapIndicator 完全相同:(典型價 × 量) × 係數。
            // Same operation order as VwapIndicator: (typical price × volume) × multiplier.
            denominator += bar.Volume * volumeMultiplier;
            numerator += bar.Amount ?? (((bar.High + bar.Low + bar.Close) / 3m) * bar.Volume * volumeMultiplier);

            // 分母為 0 時 last 維持原樣:時段剛開始是 null,時段內已有值就沿用。
            // While the denominator is 0, last stays as it was: null at session start, the previous value otherwise.
            if (denominator > 0m)
            {
                last = numerator / denominator;
            }

            results[i] = last;
        }

        return results;
    }
}
