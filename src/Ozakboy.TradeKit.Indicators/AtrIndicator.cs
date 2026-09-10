namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 平均真實區間(ATR,Wilder 平滑)的增量版本。
/// Streaming implementation of the Average True Range (ATR) with Wilder smoothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>公式。</b>真實區間 <c>TR = max(高 − 低, |高 − 前收|, |前收 − 低|)</c>,第一根沒有前收盤價,依 Wilder 原著取 <c>高 − 低</c>。
/// 首個 ATR 是前 N 個 TR 的算術平均,之後 <c>ATRₜ = (ATRₜ₋₁ × (N−1) + TRₜ) / N</c>。
/// <b>Formula.</b> True range is <c>TR = max(high - low, |high - prevClose|, |prevClose - low|)</c>; the first
/// bar has no previous close and uses <c>high - low</c> as in Wilder's original. The first ATR is the plain
/// mean of the first N true ranges, and thereafter <c>ATR_t = (ATR_(t-1) * (N - 1) + TR_t) / N</c>.
/// </para>
/// <para>
/// <b>實作。</b>只保留前一根收盤價與目前的 ATR,記憶體為常數。
/// <b>Implementation.</b> Only the previous close and the running ATR are kept, so memory is constant.
/// </para>
/// <para>
/// <b>典型用法。</b>波動度量尺:用來換算部位大小、設定追蹤停損距離(例如 3 × ATR),或過濾盤整期。
/// <b>Typical use.</b> A volatility yardstick for position sizing, trailing-stop distance (such as 3 x ATR), or filtering out quiet regimes.
/// </para>
/// <para>
/// ATR 與價格同單位,不同商品之間不可直接比較,需要先除以價格轉成百分比。
/// ATR is denominated in price units and is not comparable across instruments until divided by price.
/// </para>
/// </remarks>
public sealed class AtrIndicator : IIndicator<Candle, decimal>
{
    private decimal _previousClose;
    private bool _hasPreviousClose;
    private decimal _seedSum;
    private int _seedCount;
    private decimal _atr;
    private bool _seeded;

    /// <summary>
    /// 建立指定週期的 ATR 指標。
    /// Creates an ATR indicator with the given period.
    /// </summary>
    /// <param name="period">期數,必須大於 0,Wilder 原著使用 14。Number of periods; must be greater than 0. Wilder's original uses 14.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period" /> 小於或等於 0。<paramref name="period" /> is less than or equal to 0.</exception>
    public AtrIndicator(int period)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        Period = period;
        Name = FormattableString.Invariant($"ATR({period})");
    }

    /// <summary>期數。Number of periods.</summary>
    public int Period { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public int WarmupPeriod => Period;

    /// <inheritdoc />
    public bool IsReady => _seeded;

    /// <inheritdoc />
    public long ProcessedCount { get; private set; }

    /// <inheritdoc />
    public decimal? Current { get; private set; }

    /// <summary>
    /// 餵入一根 K 線並回傳當下的 ATR;資料量不足時回傳 <see langword="null" />。
    /// Feeds one candle and returns the current ATR; returns <see langword="null" /> until warmed up.
    /// </summary>
    /// <param name="input">K 線。The candle.</param>
    /// <returns>當下的 ATR,未就緒時為 <see langword="null" />。The current ATR, or <see langword="null" /> when not ready.</returns>
    public decimal? Update(Candle input)
    {
        ProcessedCount++;

        decimal trueRange = _hasPreviousClose
            ? ComputeTrueRange(input, _previousClose)
            : input.High - input.Low;

        _previousClose = input.Close;
        _hasPreviousClose = true;

        if (!_seeded)
        {
            _seedSum += trueRange;
            _seedCount++;

            if (_seedCount < Period)
            {
                Current = null;
                return null;
            }

            _atr = _seedSum / Period;
            _seeded = true;
        }
        else
        {
            _atr = ((_atr * (Period - 1)) + trueRange) / Period;
        }

        Current = _atr;
        return Current;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _previousClose = 0m;
        _hasPreviousClose = false;
        _seedSum = 0m;
        _seedCount = 0;
        _atr = 0m;
        _seeded = false;
        ProcessedCount = 0;
        Current = null;
    }

    /// <summary>
    /// 計算單根 K 線的真實區間。
    /// Computes the true range of a single candle.
    /// </summary>
    internal static decimal ComputeTrueRange(Candle candle, decimal previousClose)
    {
        decimal highLow = candle.High - candle.Low;
        decimal highToPreviousClose = Math.Abs(candle.High - previousClose);
        decimal previousCloseToLow = Math.Abs(previousClose - candle.Low);
        return Math.Max(highLow, Math.Max(highToPreviousClose, previousCloseToLow));
    }
}
