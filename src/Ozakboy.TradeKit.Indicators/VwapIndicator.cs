namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 成交量加權平均價(VWAP)的增量版本,每個時段重設一次。
/// Streaming implementation of the Volume-Weighted Average Price (VWAP), reset at every session.
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
/// <b>實作。</b>內部只有兩個累計值、目前時段與上一個輸出,狀態大小為常數,每根 K 線 O(1)。
/// <b>Implementation.</b> Two running sums, the current session and the previous output: constant state, O(1) per bar.
/// </para>
/// <para>
/// <b>時段重設。</b><c>sessionOf(bar.Time)</c> 與上一根不同時,累計值先歸零再累加這一根。
/// 時段怎麼切由呼叫端決定:台股傳台北日期;期貨夜盤可把夜盤時間映射到下一個交易日。
/// <b>Session reset.</b> When <c>sessionOf(bar.Time)</c> differs from the previous bar's, the sums are zeroed
/// before this bar is added. The caller decides how sessions are cut: the Taipei date for Taiwan stocks; for
/// futures, map the night session onto the next trading day.
/// </para>
/// <para>
/// <b>暖身與零量。</b><see cref="WarmupPeriod" /> 為 1,但時段開始後前幾根都沒量時仍回傳 <see langword="null" />
/// (<see cref="IsReady" /> 為 <see langword="false" />);時段內已經有值之後某根量為 0,沿用上一個值。
/// <b>Warm-up and zero volume.</b> <see cref="WarmupPeriod" /> is 1, yet <see langword="null" /> is still returned
/// while the first bars of a session traded nothing (<see cref="IsReady" /> is <see langword="false" />); once the
/// session has a value, a later zero-volume bar repeats it.
/// </para>
/// <para>
/// <b>輸入檢查與限制。</b>建構子對 <see langword="null" /> 的 <c>sessionOf</c> 與小於或等於 0 的 <c>volumeMultiplier</c>
/// 丟出例外,與其他指標對週期的處理一致;負的 <c>Volume</c> / <c>Amount</c> 會讓累計值失去意義,
/// <see cref="Update(VwapBar)" /> 遇到時丟出 <see cref="ArgumentOutOfRangeException" />,且不改動任何狀態。
/// 不檢查時間是否遞增:呼叫端負責依時間排序。
/// <b>Input checks and limits.</b> The constructor throws for a <see langword="null" /> <c>sessionOf</c> and for a
/// <c>volumeMultiplier</c> less than or equal to 0, matching how the other indicators treat their periods; a negative
/// <c>Volume</c> / <c>Amount</c> would make the sums meaningless, so <see cref="Update(VwapBar)" /> throws
/// <see cref="ArgumentOutOfRangeException" /> for it without touching any state. Time order is not checked: the caller
/// is responsible for sorting by time.
/// </para>
/// <para>
/// <b>典型用法。</b>當日機構成本線:價格在 VWAP 之上偏多、之下偏空;也常作為當沖進出場的參考價。
/// <b>Typical use.</b> The day's average cost basis: price above VWAP leans bullish, below leans bearish; a common
/// reference level for intraday entries and exits.
/// </para>
/// </remarks>
public sealed class VwapIndicator : IIndicator<VwapBar, decimal>
{
    private readonly Func<DateTimeOffset, DateOnly> _sessionOf;
    private DateOnly? _session;
    private decimal _numerator;
    private decimal _denominator;

    /// <summary>
    /// 建立 VWAP 指標。
    /// Creates a VWAP indicator.
    /// </summary>
    /// <param name="sessionOf">把 K 線時間映射到所屬時段(日期)的函式;時段變了就重設累計。Maps a bar time to its session (a date); accumulation resets whenever the session changes.</param>
    /// <param name="volumeMultiplier">把 <c>Volume</c> 換算成與 <c>Amount</c> 同單位數量的係數(台股張數傳 1000、期貨傳 1),必須大於 0。Factor converting <c>Volume</c> into the quantity unit behind <c>Amount</c> (1000 for Taiwan stock lots, 1 for futures); must be greater than 0.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sessionOf" /> 為 <see langword="null" />。<paramref name="sessionOf" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="volumeMultiplier" /> 小於或等於 0。<paramref name="volumeMultiplier" /> is less than or equal to 0.</exception>
    public VwapIndicator(Func<DateTimeOffset, DateOnly> sessionOf, decimal volumeMultiplier = 1m)
    {
        ArgumentNullException.ThrowIfNull(sessionOf);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(volumeMultiplier);

        _sessionOf = sessionOf;
        VolumeMultiplier = volumeMultiplier;
        Name = FormattableString.Invariant($"VWAP({volumeMultiplier})");
    }

    /// <summary>成交量換算係數。Volume conversion factor.</summary>
    public decimal VolumeMultiplier { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public int WarmupPeriod => 1;

    /// <inheritdoc />
    /// <remarks>目前時段已有非 <see langword="null" /> 的值。The current session already holds a non-<see langword="null" /> value.</remarks>
    public bool IsReady => Current is not null;

    /// <inheritdoc />
    public long ProcessedCount { get; private set; }

    /// <inheritdoc />
    public decimal? Current { get; private set; }

    /// <summary>
    /// 餵入一根 K 線並回傳當下的 VWAP;目前時段還沒有任何成交量時回傳 <see langword="null" />。
    /// Feeds one bar and returns the current VWAP; returns <see langword="null" /> while the current session has traded no volume.
    /// </summary>
    /// <param name="input">K 線。The bar.</param>
    /// <returns>當下的 VWAP,未就緒時為 <see langword="null" />。The current VWAP, or <see langword="null" /> when not ready.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="input" /> 的 <c>Volume</c> 或 <c>Amount</c> 為負數。<c>Volume</c> or <c>Amount</c> of <paramref name="input" /> is negative.</exception>
    public decimal? Update(VwapBar input)
    {
        // 先驗證再動任何狀態,丟例外後指標仍可繼續使用。
        // Validate before touching any state so the indicator stays usable after an exception.
        ArgumentOutOfRangeException.ThrowIfNegative(input.Volume);
        if (input.Amount is decimal amount)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(amount);
        }

        DateOnly barSession = _sessionOf(input.Time);
        if (_session != barSession)
        {
            // 換時段:累計值與上一個輸出都不能帶過來。
            // New session: neither the sums nor the previous output may carry over.
            _session = barSession;
            _numerator = 0m;
            _denominator = 0m;
            Current = null;
        }

        // 運算順序與 Vwap.Calculate 完全相同:(典型價 × 量) × 係數。
        // Same operation order as Vwap.Calculate: (typical price × volume) × multiplier.
        _denominator += input.Volume * VolumeMultiplier;
        _numerator += input.Amount ?? (((input.High + input.Low + input.Close) / 3m) * input.Volume * VolumeMultiplier);

        // 分母為 0 時 Current 維持原樣:時段剛開始是 null,時段內已有值就沿用。
        // While the denominator is 0, Current stays as it was: null at session start, the previous value otherwise.
        if (_denominator > 0m)
        {
            Current = _numerator / _denominator;
        }

        ProcessedCount++;
        return Current;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _session = null;
        _numerator = 0m;
        _denominator = 0m;
        ProcessedCount = 0;
        Current = null;
    }
}
