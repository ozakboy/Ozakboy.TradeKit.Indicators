namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 指數移動平均(EMA)的增量版本。
/// Streaming implementation of the Exponential Moving Average (EMA).
/// </summary>
/// <remarks>
/// <para>
/// <b>公式。</b>平滑係數 <c>k = 2 / (N + 1)</c>,遞迴式 <c>EMAₜ = (Pₜ − EMAₜ₋₁) × k + EMAₜ₋₁</c>。
/// 起始值由 <see cref="EmaSeedMode" /> 決定,預設取前 N 筆的簡單移動平均。
/// <b>Formula.</b> Smoothing factor <c>k = 2 / (N + 1)</c> and the recursion
/// <c>EMA_t = (P_t - EMA_(t-1)) * k + EMA_(t-1)</c>. The seed is chosen by <see cref="EmaSeedMode" />,
/// defaulting to the simple moving average of the first N inputs.
/// </para>
/// <para>
/// <b>實作。</b>遞迴式本身只需要前一個值,因此暖身完成後內部狀態是常數大小,不保留任何歷史序列。
/// <b>Implementation.</b> The recursion needs only the previous value, so once warmed up the internal
/// state is constant-size and no history is retained.
/// </para>
/// <para>
/// <b>典型用法。</b>比 SMA 更快反應近期價格,常用於 MACD 的組件與短線趨勢判斷。
/// <b>Typical use.</b> Reacts to recent prices faster than an SMA; the building block of MACD and of short-term trend rules.
/// </para>
/// </remarks>
public sealed class EmaIndicator : IIndicator<decimal, decimal>, IIndicator<Candle, decimal>
{
    private decimal _seedSum;
    private int _seedCount;
    private decimal _ema;
    private bool _hasEma;

    /// <summary>
    /// 建立指定週期的 EMA 指標。
    /// Creates an EMA indicator with the given period.
    /// </summary>
    /// <param name="period">期數,必須大於 0。Number of periods; must be greater than 0.</param>
    /// <param name="seedMode">起始值取法,預設為前 N 筆的簡單移動平均。How to seed the recursion; defaults to the simple moving average of the first N inputs.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period" /> 小於或等於 0。<paramref name="period" /> is less than or equal to 0.</exception>
    public EmaIndicator(int period, EmaSeedMode seedMode = EmaSeedMode.SimpleMovingAverage)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        Period = period;
        SeedMode = seedMode;
        SmoothingFactor = 2m / (period + 1);
        Name = FormattableString.Invariant($"EMA({period})");
    }

    /// <summary>期數。Number of periods.</summary>
    public int Period { get; }

    /// <summary>起始值取法。How the recursion is seeded.</summary>
    public EmaSeedMode SeedMode { get; }

    /// <summary>平滑係數 <c>k = 2 / (N + 1)</c>。Smoothing factor <c>k = 2 / (N + 1)</c>.</summary>
    public decimal SmoothingFactor { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public int WarmupPeriod => Period;

    /// <inheritdoc />
    public bool IsReady => ProcessedCount >= Period;

    /// <inheritdoc />
    public long ProcessedCount { get; private set; }

    /// <inheritdoc />
    public decimal? Current { get; private set; }

    /// <summary>
    /// 餵入一筆價格並回傳當下的 EMA;累積筆數不足 <see cref="Period" /> 時回傳 <see langword="null" />。
    /// Feeds one price and returns the current EMA; returns <see langword="null" /> until <see cref="Period" /> inputs have arrived.
    /// </summary>
    /// <param name="input">價格。The price.</param>
    /// <returns>當下的 EMA,未就緒時為 <see langword="null" />。The current EMA, or <see langword="null" /> when not ready.</returns>
    public decimal? Update(decimal input)
    {
        ProcessedCount++;

        if (SeedMode == EmaSeedMode.FirstValue)
        {
            // 第一筆直接當作種子,之後每一筆都走遞迴;但仍要滿 N 筆才視為就緒。
            // The first input becomes the seed and every later input recurses, yet readiness still waits for N inputs.
            _ema = _hasEma ? ((input - _ema) * SmoothingFactor) + _ema : input;
            _hasEma = true;
        }
        else if (!_hasEma)
        {
            _seedSum += input;
            _seedCount++;

            if (_seedCount == Period)
            {
                _ema = _seedSum / Period;
                _hasEma = true;
            }
        }
        else
        {
            _ema = ((input - _ema) * SmoothingFactor) + _ema;
        }

        Current = IsReady ? _ema : null;
        return Current;
    }

    /// <summary>
    /// 以 K 線的收盤價餵入一筆資料。
    /// Feeds one candle, using its close price.
    /// </summary>
    /// <param name="input">K 線。The candle.</param>
    /// <returns>當下的 EMA,未就緒時為 <see langword="null" />。The current EMA, or <see langword="null" /> when not ready.</returns>
    public decimal? Update(Candle input) => Update(input.Close);

    /// <inheritdoc />
    public void Reset()
    {
        _seedSum = 0m;
        _seedCount = 0;
        _ema = 0m;
        _hasEma = false;
        ProcessedCount = 0;
        Current = null;
    }
}
