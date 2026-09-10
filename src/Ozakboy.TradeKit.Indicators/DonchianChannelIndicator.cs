using Ozakboy.TradeKit.Indicators.Internal;

namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 唐奇安通道(Donchian Channel)的增量版本。
/// Streaming implementation of the Donchian Channel.
/// </summary>
/// <remarks>
/// <para>
/// <b>公式。</b><c>上軌 = 最近 N 根(含當根)的最高價</c>、<c>下軌 = 最近 N 根(含當根)的最低價</c>、
/// <c>中軌 = (上軌 + 下軌) / 2</c>。
/// <b>Formula.</b> <c>upper</c> is the highest high over the last N candles including the current one,
/// <c>lower</c> the lowest low over the same window, and <c>middle = (upper + lower) / 2</c>.
/// </para>
/// <para>
/// <b>含當根 vs 不含當根。</b>本實作採用含當根的定義。海龜交易法那類「突破前 N 根高點才進場」的規則,
/// 若直接拿含當根的上軌比對會永遠無法突破,請改用前一根的通道值(先讀 <see cref="Current" /> 再 <see cref="Update(Candle)" />)。
/// <b>Inclusive vs exclusive.</b> This implementation includes the current candle. Breakout rules of the
/// Turtle kind ("enter when price exceeds the prior N-bar high") can never trigger against an inclusive upper
/// band, so read <see cref="Current" /> before calling <see cref="Update(Candle)" /> to compare against the
/// previous bar's channel.
/// </para>
/// <para>
/// <b>實作。</b>上下軌各由一個單調雙端佇列維護,攤還成本 O(1),記憶體 O(N)。
/// <b>Implementation.</b> Each band is maintained by a monotonic deque: O(1) amortised per bar, O(N) memory.
/// </para>
/// <para>
/// <b>典型用法。</b>突破策略的進出場界線,或作為波動區間的視覺化包絡。
/// <b>Typical use.</b> Entry and exit boundaries for breakout systems, or a visual envelope of the trading range.
/// </para>
/// </remarks>
public sealed class DonchianChannelIndicator : IIndicator<Candle, DonchianChannelResult>
{
    private readonly MonotonicExtremumWindow _highs;
    private readonly MonotonicExtremumWindow _lows;

    /// <summary>
    /// 建立指定週期的唐奇安通道指標。
    /// Creates a Donchian Channel indicator with the given period.
    /// </summary>
    /// <param name="period">期數,必須大於 0。Number of periods; must be greater than 0.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period" /> 小於或等於 0。<paramref name="period" /> is less than or equal to 0.</exception>
    public DonchianChannelIndicator(int period = 20)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        Period = period;
        Name = FormattableString.Invariant($"DC({period})");
        _highs = new MonotonicExtremumWindow(period, trackMaximum: true);
        _lows = new MonotonicExtremumWindow(period, trackMaximum: false);
    }

    /// <summary>期數。Number of periods.</summary>
    public int Period { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public int WarmupPeriod => Period;

    /// <inheritdoc />
    public bool IsReady => ProcessedCount >= Period;

    /// <inheritdoc />
    public long ProcessedCount { get; private set; }

    /// <inheritdoc />
    public DonchianChannelResult? Current { get; private set; }

    /// <summary>
    /// 餵入一根 K 線並回傳當下的通道;累積筆數不足 <see cref="Period" /> 時回傳 <see langword="null" />。
    /// Feeds one candle and returns the current channel; returns <see langword="null" /> until <see cref="Period" /> candles have arrived.
    /// </summary>
    /// <param name="input">K 線。The candle.</param>
    /// <returns>當下的通道,未就緒時為 <see langword="null" />。The current channel, or <see langword="null" /> when not ready.</returns>
    public DonchianChannelResult? Update(Candle input)
    {
        _highs.Add(input.High);
        _lows.Add(input.Low);
        ProcessedCount++;

        if (!IsReady)
        {
            Current = null;
            return null;
        }

        decimal upper = _highs.Extremum;
        decimal lower = _lows.Extremum;

        Current = new DonchianChannelResult(upper, (upper + lower) / 2m, lower);
        return Current;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _highs.Clear();
        _lows.Clear();
        ProcessedCount = 0;
        Current = null;
    }
}
