using Ozakboy.TradeKit.Indicators.Internal;

namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// KD 隨機指標(Stochastic,台股慣用平滑法)的增量版本。
/// Streaming implementation of the Stochastic oscillator (KD) using the smoothing convention common in Taiwan.
/// </summary>
/// <remarks>
/// <para>
/// <b>公式。</b><c>RSVₜ = (收盤價ₜ − 最近 N 根(含當根)最低價) / (最近 N 根最高價 − 最近 N 根最低價) × 100</c>;
/// <c>Kₜ = Kₜ₋₁ × (kSmoothing − 1) / kSmoothing + RSVₜ / kSmoothing</c>;
/// <c>Dₜ = Dₜ₋₁ × (dSmoothing − 1) / dSmoothing + Kₜ / dSmoothing</c>。
/// 實作上以等價的 <c>(前值 × (n − 1) + 當期值) / n</c> 計算,只做一次除法以保留精度。
/// <b>Formula.</b> <c>RSV_t = (close_t - lowest low of the last N candles) / (highest high - lowest low) * 100</c>,
/// where the window includes the current candle; <c>K_t = K_(t-1) * (kSmoothing - 1) / kSmoothing + RSV_t / kSmoothing</c>
/// and <c>D_t = D_(t-1) * (dSmoothing - 1) / dSmoothing + K_t / dSmoothing</c>. The code evaluates the equivalent
/// <c>(previous * (n - 1) + current) / n</c>, which needs a single division and so keeps more precision.
/// </para>
/// <para>
/// <b>分母為 0 的處理。</b>最近 N 根的最高價等於最低價(整段完全持平)時 RSV 沒有定義,本實作以 50 代替,
/// 也就是視為區間正中央;持平序列的 K、D 因此會一直維持在 50。
/// <b>Zero denominator.</b> When the highest high equals the lowest low (a completely flat window) the RSV is
/// undefined; this implementation substitutes 50, the middle of the range, so a flat series keeps K and D at 50.
/// </para>
/// <para>
/// <b>種子與暖身期。</b>第一個可算出 RSV 的那一根(第 N 根,索引 N − 1),前一期的 K、D 都以 50 起算(台股看盤軟體慣例),
/// 所以第一個輸出就在第 N 根,<see cref="WarmupPeriod" /> 等於 <see cref="Period" />,之前一律回傳 <see langword="null" />。
/// 遞迴平滑讓結果與起算點有關:序列開頭不同(例如往前多抓幾根歷史資料)會讓前幾十根的 K / D 有差異,隨後收斂;
/// 要和看盤軟體對數字,請盡量從同一根 K 線開始計算。
/// <b>Seeding and warm-up.</b> At the first candle with a defined RSV (the N-th candle, index N - 1) the previous
/// K and D are both taken as 50, the convention of Taiwan charting software, so the first output arrives on the
/// N-th candle and <see cref="WarmupPeriod" /> equals <see cref="Period" />; earlier candles return
/// <see langword="null" />. Because the smoothing is recursive the result depends on where the series starts:
/// a different starting candle (for example fetching more history) changes the first few dozen K / D values,
/// which then converge. To match a charting package, start from the same candle it does.
/// </para>
/// <para>
/// <b>實作。</b>最高價與最低價各由一個單調雙端佇列維護,攤還成本 O(1),記憶體 O(N)。
/// <b>Implementation.</b> The highest high and lowest low are each maintained by a monotonic deque:
/// O(1) amortised per bar, O(N) memory.
/// </para>
/// <para>
/// <b>典型用法。</b>台股慣用參數為 (9, 3, 3);K、D 高於 80 視為超買、低於 20 視為超賣,
/// K 由下往上穿越 D 為黃金交叉、由上往下穿越為死亡交叉。
/// <b>Typical use.</b> Taiwan charts default to (9, 3, 3). K and D above 80 read as overbought and below 20 as
/// oversold; K crossing above D is a golden cross, crossing below is a death cross.
/// </para>
/// </remarks>
public sealed class StochasticIndicator : IIndicator<Candle, StochasticResult>
{
    /// <summary>
    /// K、D 的起算值,同時也是 RSV 分母為 0 時的替代值。
    /// Starting value for K and D, also used as the RSV when its denominator is zero.
    /// </summary>
    internal const decimal SeedValue = 50m;

    private readonly MonotonicExtremumWindow _highs;
    private readonly MonotonicExtremumWindow _lows;
    private decimal _k = SeedValue;
    private decimal _d = SeedValue;

    /// <summary>
    /// 建立指定參數的 KD 指標。
    /// Creates a KD indicator with the given parameters.
    /// </summary>
    /// <param name="period">RSV 期數,必須大於 0,台股慣用 9。RSV look-back period; must be greater than 0. Taiwan charts typically use 9.</param>
    /// <param name="kSmoothing">K 值的平滑期數,必須大於 0,慣用 3。Smoothing period for K; must be greater than 0. Typically 3.</param>
    /// <param name="dSmoothing">D 值的平滑期數,必須大於 0,慣用 3。Smoothing period for D; must be greater than 0. Typically 3.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period" />、<paramref name="kSmoothing" /> 或 <paramref name="dSmoothing" /> 小於或等於 0。<paramref name="period" />, <paramref name="kSmoothing" /> or <paramref name="dSmoothing" /> is less than or equal to 0.</exception>
    public StochasticIndicator(int period = 9, int kSmoothing = 3, int dSmoothing = 3)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(kSmoothing);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dSmoothing);

        Period = period;
        KSmoothing = kSmoothing;
        DSmoothing = dSmoothing;
        Name = FormattableString.Invariant($"KD({period},{kSmoothing},{dSmoothing})");
        _highs = new MonotonicExtremumWindow(period, trackMaximum: true);
        _lows = new MonotonicExtremumWindow(period, trackMaximum: false);
    }

    /// <summary>RSV 期數。RSV look-back period.</summary>
    public int Period { get; }

    /// <summary>K 值的平滑期數。Smoothing period for K.</summary>
    public int KSmoothing { get; }

    /// <summary>D 值的平滑期數。Smoothing period for D.</summary>
    public int DSmoothing { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public int WarmupPeriod => Period;

    /// <inheritdoc />
    public bool IsReady => ProcessedCount >= Period;

    /// <inheritdoc />
    public long ProcessedCount { get; private set; }

    /// <inheritdoc />
    public StochasticResult? Current { get; private set; }

    /// <summary>
    /// 餵入一根 K 線並回傳當下的 KD 值;累積筆數不足 <see cref="Period" /> 時回傳 <see langword="null" />。
    /// Feeds one candle and returns the current KD values; returns <see langword="null" /> until <see cref="Period" /> candles have arrived.
    /// </summary>
    /// <param name="input">K 線。The candle.</param>
    /// <returns>當下的 KD 值,未就緒時為 <see langword="null" />。The current KD values, or <see langword="null" /> when not ready.</returns>
    public StochasticResult? Update(Candle input)
    {
        _highs.Add(input.High);
        _lows.Add(input.Low);
        ProcessedCount++;

        if (!IsReady)
        {
            Current = null;
            return null;
        }

        decimal rsv = ComputeRsv(input.Close, _highs.Extremum, _lows.Extremum);
        _k = Smooth(_k, rsv, KSmoothing);
        _d = Smooth(_d, _k, DSmoothing);

        Current = new StochasticResult(_k, _d);
        return Current;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _highs.Clear();
        _lows.Clear();
        _k = SeedValue;
        _d = SeedValue;
        ProcessedCount = 0;
        Current = null;
    }

    /// <summary>
    /// 由收盤價與視窗極值算出 RSV,並套用分母為 0 時的替代值 50。
    /// Computes the RSV from the close and the window extremes, applying the documented fallback of 50 for a zero denominator.
    /// </summary>
    internal static decimal ComputeRsv(decimal close, decimal highest, decimal lowest)
    {
        decimal range = highest - lowest;

        if (range == 0m)
        {
            // 整段完全持平時 RSV 沒有定義,視為區間正中央。
            // A completely flat window leaves the RSV undefined; treat it as the middle of the range.
            return SeedValue;
        }

        return (close - lowest) * 100m / range;
    }

    /// <summary>
    /// 台股 KD 的遞迴平滑:<c>(前值 × (n − 1) + 當期值) / n</c>。
    /// The recursive smoothing used by Taiwan KD: <c>(previous * (n - 1) + current) / n</c>.
    /// </summary>
    internal static decimal Smooth(decimal previous, decimal current, int smoothing)
        => ((previous * (smoothing - 1)) + current) / smoothing;
}
