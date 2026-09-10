namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 相對強弱指標(RSI,Wilder 平滑)的增量版本。
/// Streaming implementation of the Relative Strength Index (RSI) with Wilder smoothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>公式。</b>先把每根 K 線的收盤價差拆成漲幅與跌幅(跌幅取絕對值),再取 N 期平均:
/// 首值 <c>平均漲幅 = 前 N 期漲幅總和 / N</c>,之後 <c>平均漲幅ₜ = (平均漲幅ₜ₋₁ × (N−1) + 漲幅ₜ) / N</c>,
/// 平均跌幅同理;最後 <c>RS = 平均漲幅 / 平均跌幅</c>、<c>RSI = 100 − 100 / (1 + RS)</c>。
/// <b>Formula.</b> Close-to-close changes are split into gains and losses (losses as positive magnitudes)
/// and averaged over N periods: the first average is the plain mean of the first N values, and thereafter
/// <c>avg_t = (avg_(t-1) * (N - 1) + current_t) / N</c>. Then <c>RS = avgGain / avgLoss</c> and
/// <c>RSI = 100 - 100 / (1 + RS)</c>.
/// </para>
/// <para>
/// <b>除以零的處理。</b>連續上漲會讓平均跌幅變成 0,連續下跌會讓平均漲幅變成 0,這在真實行情每天都會發生:
/// </para>
/// <list type="bullet">
///   <item><description>平均跌幅為 0 且平均漲幅大於 0 → RSI = 100(公認慣例)。Average loss 0 with a positive average gain gives RSI = 100, the standard convention.</description></item>
///   <item><description>平均漲幅為 0 且平均跌幅大於 0 → RSI = 0。Average gain 0 with a positive average loss gives RSI = 0.</description></item>
///   <item><description>兩者皆為 0(整段完全沒有價格變動)→ RSI = 50。此情境沒有公認答案,本函式庫選擇中性值,避免把「毫無波動」誤報成「極度超買」。Both zero (a completely flat window) gives RSI = 50. There is no agreed convention here; this library returns the neutral value rather than reporting a flat market as extremely overbought.</description></item>
/// </list>
/// <para>
/// <b>實作。</b>只保留前一根收盤價與兩個滑動平均值,記憶體為常數。
/// <b>Implementation.</b> Only the previous close and the two smoothed averages are kept, so memory is constant.
/// </para>
/// <para>
/// <b>典型用法。</b>Wilder 以 70 / 30 作為超買超賣界線;趨勢策略則常用 50 中線判斷多空。
/// <b>Typical use.</b> Wilder treats 70 / 30 as overbought and oversold; trend systems often use the 50 centreline instead.
/// </para>
/// </remarks>
public sealed class RsiIndicator : IIndicator<decimal, decimal>, IIndicator<Candle, decimal>
{
    private decimal _previousPrice;
    private bool _hasPreviousPrice;
    private decimal _seedGainSum;
    private decimal _seedLossSum;
    private int _seedCount;
    private decimal _averageGain;
    private decimal _averageLoss;
    private bool _seeded;

    /// <summary>
    /// 建立指定週期的 RSI 指標。
    /// Creates an RSI indicator with the given period.
    /// </summary>
    /// <param name="period">期數,必須大於 0,Wilder 原著使用 14。Number of periods; must be greater than 0. Wilder's original uses 14.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period" /> 小於或等於 0。<paramref name="period" /> is less than or equal to 0.</exception>
    public RsiIndicator(int period)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        Period = period;
        Name = FormattableString.Invariant($"RSI({period})");
    }

    /// <summary>期數。Number of periods.</summary>
    public int Period { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>
    /// 需要 <see cref="Period" /> + 1 筆價格,因為 N 個價差需要 N + 1 個價格。
    /// Requires <see cref="Period" /> + 1 prices, because N changes need N + 1 prices.
    /// </summary>
    public int WarmupPeriod => Period + 1;

    /// <inheritdoc />
    public bool IsReady => _seeded;

    /// <inheritdoc />
    public long ProcessedCount { get; private set; }

    /// <inheritdoc />
    public decimal? Current { get; private set; }

    /// <summary>
    /// 餵入一筆價格並回傳當下的 RSI;資料量不足時回傳 <see langword="null" />。
    /// Feeds one price and returns the current RSI; returns <see langword="null" /> until warmed up.
    /// </summary>
    /// <param name="input">價格。The price.</param>
    /// <returns>當下的 RSI(0 到 100),未就緒時為 <see langword="null" />。The current RSI between 0 and 100, or <see langword="null" /> when not ready.</returns>
    public decimal? Update(decimal input)
    {
        ProcessedCount++;

        if (!_hasPreviousPrice)
        {
            _previousPrice = input;
            _hasPreviousPrice = true;
            Current = null;
            return null;
        }

        decimal change = input - _previousPrice;
        _previousPrice = input;

        decimal gain = change > 0m ? change : 0m;
        decimal loss = change < 0m ? -change : 0m;

        if (!_seeded)
        {
            _seedGainSum += gain;
            _seedLossSum += loss;
            _seedCount++;

            if (_seedCount < Period)
            {
                Current = null;
                return null;
            }

            _averageGain = _seedGainSum / Period;
            _averageLoss = _seedLossSum / Period;
            _seeded = true;
        }
        else
        {
            _averageGain = ((_averageGain * (Period - 1)) + gain) / Period;
            _averageLoss = ((_averageLoss * (Period - 1)) + loss) / Period;
        }

        Current = ComputeRsi(_averageGain, _averageLoss);
        return Current;
    }

    /// <summary>
    /// 以 K 線的收盤價餵入一筆資料。
    /// Feeds one candle, using its close price.
    /// </summary>
    /// <param name="input">K 線。The candle.</param>
    /// <returns>當下的 RSI,未就緒時為 <see langword="null" />。The current RSI, or <see langword="null" /> when not ready.</returns>
    public decimal? Update(Candle input) => Update(input.Close);

    /// <inheritdoc />
    public void Reset()
    {
        _previousPrice = 0m;
        _hasPreviousPrice = false;
        _seedGainSum = 0m;
        _seedLossSum = 0m;
        _seedCount = 0;
        _averageGain = 0m;
        _averageLoss = 0m;
        _seeded = false;
        ProcessedCount = 0;
        Current = null;
    }

    /// <summary>
    /// 由平均漲幅與平均跌幅算出 RSI,並套用文件化的除以零替代值。
    /// Turns the smoothed gain and loss into an RSI, applying the documented zero-denominator fallbacks.
    /// </summary>
    internal static decimal ComputeRsi(decimal averageGain, decimal averageLoss)
    {
        if (averageLoss == 0m)
        {
            // 完全沒有波動時沒有公認答案,取中性值;只有上漲沒有下跌才是真正的極度超買。
            // A completely flat window has no agreed answer, so return neutral; only pure gains mean truly overbought.
            return averageGain == 0m ? 50m : 100m;
        }

        if (averageGain == 0m)
        {
            return 0m;
        }

        decimal relativeStrength = averageGain / averageLoss;
        return 100m - (100m / (1m + relativeStrength));
    }
}
