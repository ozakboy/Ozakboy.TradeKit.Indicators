using Ozakboy.TradeKit.Indicators.Internal;

namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 簡單移動平均(SMA)的增量版本。
/// Streaming implementation of the Simple Moving Average (SMA).
/// </summary>
/// <remarks>
/// <para>
/// <b>公式。</b><c>SMA(N)ₜ = (Pₜ + Pₜ₋₁ + … + Pₜ₋ₙ₊₁) / N</c>。
/// <b>Formula.</b> <c>SMA(N)_t = (P_t + P_(t-1) + ... + P_(t-N+1)) / N</c>.
/// </para>
/// <para>
/// <b>實作。</b>內部只有一個長度 N 的環形緩衝區與一個滾動總和:新值進場時加上新值、減去被擠出的舊值,
/// 因此每根 K 線的成本是 O(1),記憶體與已處理筆數無關。
/// <b>Implementation.</b> A single length-N ring buffer plus a running sum: each update adds the incoming
/// value and subtracts the evicted one, so cost is O(1) per bar and memory is independent of how many
/// bars have been processed.
/// </para>
/// <para>
/// <b>典型用法。</b>作為趨勢過濾器(價格在 SMA(200) 之上才做多),或作為其他指標的中軌與種子。
/// <b>Typical use.</b> A trend filter (only go long above SMA(200)), or the middle band and seed of other indicators.
/// </para>
/// </remarks>
public sealed class SmaIndicator : IIndicator<decimal, decimal>, IIndicator<Candle, decimal>
{
    private readonly DecimalRingBuffer _window;
    private decimal _sum;

    /// <summary>
    /// 建立指定週期的 SMA 指標。
    /// Creates an SMA indicator with the given period.
    /// </summary>
    /// <param name="period">平均的期數,必須大於 0。Number of periods to average; must be greater than 0.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period" /> 小於或等於 0。<paramref name="period" /> is less than or equal to 0.</exception>
    public SmaIndicator(int period)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        Period = period;
        Name = FormattableString.Invariant($"SMA({period})");
        _window = new DecimalRingBuffer(period);
    }

    /// <summary>平均的期數。Number of periods averaged.</summary>
    public int Period { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public int WarmupPeriod => Period;

    /// <inheritdoc />
    public bool IsReady => _window.IsFull;

    /// <inheritdoc />
    public long ProcessedCount { get; private set; }

    /// <inheritdoc />
    public decimal? Current { get; private set; }

    /// <summary>
    /// 餵入一筆價格並回傳當下的 SMA;累積筆數不足 <see cref="Period" /> 時回傳 <see langword="null" />。
    /// Feeds one price and returns the current SMA; returns <see langword="null" /> until <see cref="Period" /> inputs have arrived.
    /// </summary>
    /// <param name="input">價格。The price.</param>
    /// <returns>當下的 SMA,未就緒時為 <see langword="null" />。The current SMA, or <see langword="null" /> when not ready.</returns>
    public decimal? Update(decimal input)
    {
        if (_window.Add(input, out decimal evicted))
        {
            _sum -= evicted;
        }

        _sum += input;
        ProcessedCount++;
        Current = _window.IsFull ? _sum / Period : null;
        return Current;
    }

    /// <summary>
    /// 以 K 線的收盤價餵入一筆資料。
    /// Feeds one candle, using its close price.
    /// </summary>
    /// <param name="input">K 線。The candle.</param>
    /// <returns>當下的 SMA,未就緒時為 <see langword="null" />。The current SMA, or <see langword="null" /> when not ready.</returns>
    public decimal? Update(Candle input) => Update(input.Close);

    /// <inheritdoc />
    public void Reset()
    {
        _window.Clear();
        _sum = 0m;
        ProcessedCount = 0;
        Current = null;
    }
}
