using Ozakboy.TradeKit.Indicators.Internal;

namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 布林通道(Bollinger Bands)的增量版本。
/// Streaming implementation of Bollinger Bands.
/// </summary>
/// <remarks>
/// <para>
/// <b>公式。</b><c>中軌 = SMA(N)</c>、<c>σ = 視窗內收盤價對中軌的母體標準差</c>、
/// <c>上軌 = 中軌 + k × σ</c>、<c>下軌 = 中軌 − k × σ</c>。Bollinger 原始參數為 N = 20、k = 2。
/// <b>Formula.</b> <c>middle = SMA(N)</c>, <c>sigma</c> is the population standard deviation of the window
/// about that mean, <c>upper = middle + k * sigma</c> and <c>lower = middle - k * sigma</c>. Bollinger's
/// original parameters are N = 20 and k = 2.
/// </para>
/// <para>
/// <b>母體 vs 樣本標準差。</b>本實作除以 N(母體),與 Bollinger 原著及主流看盤軟體一致;
/// 若改除以 N−1(樣本)在 N = 20 時通道會寬約 2.6%。
/// <b>Population vs sample.</b> This implementation divides by N (population), matching Bollinger's original
/// work and mainstream charting packages; dividing by N-1 would widen the bands by roughly 2.6% at N = 20.
/// </para>
/// <para>
/// <b>實作。</b>長度 N 的環形緩衝區同時支撐滾動總和(中軌)與變異數掃描(標準差),
/// 記憶體為 O(N) 且與已處理筆數無關。平方根由 <see cref="DecimalMath.Sqrt(decimal)" /> 以牛頓法計算。
/// <b>Implementation.</b> One length-N ring buffer backs both the running sum (middle band) and the variance
/// scan (standard deviation); memory is O(N) and independent of the number of bars processed. The square root
/// comes from <see cref="DecimalMath.Sqrt(decimal)" />, a decimal Newton–Raphson implementation.
/// </para>
/// <para>
/// <b>典型用法。</b>價格觸及上下軌代表相對極端;通道收窄(擠壓)常被視為波動即將擴張的前兆。
/// <b>Typical use.</b> Touching a band marks a relative extreme, and a narrowing channel (a squeeze) is widely
/// read as a precursor to expanding volatility.
/// </para>
/// </remarks>
public sealed class BollingerBandsIndicator
    : IIndicator<decimal, BollingerBandsResult>, IIndicator<Candle, BollingerBandsResult>
{
    private readonly DecimalRingBuffer _window;
    private decimal _sum;

    /// <summary>
    /// 建立布林通道指標。
    /// Creates a Bollinger Bands indicator.
    /// </summary>
    /// <param name="period">期數,必須大於 0。Number of periods; must be greater than 0.</param>
    /// <param name="standardDeviations">標準差倍數,不可為負。Standard deviation multiplier; must not be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period" /> 小於或等於 0,或 <paramref name="standardDeviations" /> 為負。<paramref name="period" /> is less than or equal to 0, or <paramref name="standardDeviations" /> is negative.</exception>
    public BollingerBandsIndicator(int period = 20, decimal standardDeviations = 2m)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        ArgumentOutOfRangeException.ThrowIfNegative(standardDeviations);

        Period = period;
        StandardDeviations = standardDeviations;
        Name = FormattableString.Invariant($"BB({period},{standardDeviations})");
        _window = new DecimalRingBuffer(period);
    }

    /// <summary>期數。Number of periods.</summary>
    public int Period { get; }

    /// <summary>標準差倍數。Standard deviation multiplier.</summary>
    public decimal StandardDeviations { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public int WarmupPeriod => Period;

    /// <inheritdoc />
    public bool IsReady => _window.IsFull;

    /// <inheritdoc />
    public long ProcessedCount { get; private set; }

    /// <inheritdoc />
    public BollingerBandsResult? Current { get; private set; }

    /// <summary>
    /// 餵入一筆價格並回傳當下的通道;累積筆數不足 <see cref="Period" /> 時回傳 <see langword="null" />。
    /// Feeds one price and returns the current bands; returns <see langword="null" /> until <see cref="Period" /> inputs have arrived.
    /// </summary>
    /// <param name="input">價格。The price.</param>
    /// <returns>當下的通道,未就緒時為 <see langword="null" />。The current bands, or <see langword="null" /> when not ready.</returns>
    public BollingerBandsResult? Update(decimal input)
    {
        if (_window.Add(input, out decimal evicted))
        {
            _sum -= evicted;
        }

        _sum += input;
        ProcessedCount++;

        if (!_window.IsFull)
        {
            Current = null;
            return null;
        }

        decimal middle = _sum / Period;
        decimal squaredDeviationSum = 0m;

        for (int i = 0; i < Period; i++)
        {
            decimal deviation = _window[i] - middle;
            squaredDeviationSum += deviation * deviation;
        }

        decimal standardDeviation = DecimalMath.Sqrt(squaredDeviationSum / Period);
        decimal offset = StandardDeviations * standardDeviation;

        Current = new BollingerBandsResult(middle, middle + offset, middle - offset, standardDeviation);
        return Current;
    }

    /// <summary>
    /// 以 K 線的收盤價餵入一筆資料。
    /// Feeds one candle, using its close price.
    /// </summary>
    /// <param name="input">K 線。The candle.</param>
    /// <returns>當下的通道,未就緒時為 <see langword="null" />。The current bands, or <see langword="null" /> when not ready.</returns>
    public BollingerBandsResult? Update(Candle input) => Update(input.Close);

    /// <inheritdoc />
    public void Reset()
    {
        _window.Clear();
        _sum = 0m;
        ProcessedCount = 0;
        Current = null;
    }
}
