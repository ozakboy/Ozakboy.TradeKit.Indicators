namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 平均趨向指標(ADX,Wilder)的增量版本,含 +DI 與 −DI。
/// Streaming implementation of Wilder's Average Directional Index (ADX), including +DI and -DI.
/// </summary>
/// <remarks>
/// <para>
/// <b>公式。</b>對每一根 K 線(第一根沒有前一根,不參與計算):
/// </para>
/// <list type="number">
///   <item><description><c>上升動能 = 高ₜ − 高ₜ₋₁</c>、<c>下降動能 = 低ₜ₋₁ − 低ₜ</c>;<c>+DM</c> 在上升動能同時大於下降動能與 0 時等於上升動能,否則為 0,<c>−DM</c> 同理。Up move is <c>high_t - high_(t-1)</c> and down move is <c>low_(t-1) - low_t</c>; <c>+DM</c> equals the up move when it exceeds both the down move and 0, otherwise 0, and <c>-DM</c> mirrors it.</description></item>
///   <item><description>以 Wilder 累加法平滑 TR、+DM、−DM:首值為前 N 期總和,之後 <c>值ₜ = 值ₜ₋₁ − 值ₜ₋₁ / N + 當期值</c>。Wilder-smooth TR, +DM and -DM: the first value is the sum of the first N, thereafter <c>v_t = v_(t-1) - v_(t-1) / N + current</c>.</description></item>
///   <item><description><c>+DI = 100 × 平滑+DM / 平滑TR</c>、<c>−DI = 100 × 平滑−DM / 平滑TR</c>。</description></item>
///   <item><description><c>DX = 100 × |+DI − −DI| / (+DI + −DI)</c>。</description></item>
///   <item><description><c>ADX</c> 首值為前 N 個 DX 的算術平均,之後 <c>ADXₜ = (ADXₜ₋₁ × (N−1) + DXₜ) / N</c>。The first ADX is the mean of the first N DX values, thereafter <c>ADX_t = (ADX_(t-1) * (N - 1) + DX_t) / N</c>.</description></item>
/// </list>
/// <para>
/// <b>暖身期。</b>兩段平滑串在一起,因此需要 <c>2 × N</c> 根 K 線(N = 14 即 28 根)。
/// Wilder 提醒平滑需要時間穩定,實務上建議餵入遠多於暖身期的歷史資料再開始使用。
/// <b>Warm-up.</b> Two smoothing stages stack up, so <c>2 * N</c> candles are required (28 at N = 14).
/// Wilder notes that the smoothing needs time to settle, so feeding substantially more history than the bare
/// warm-up before relying on the output is advisable.
/// </para>
/// <para>
/// <b>除以零的處理。</b>平滑 TR 為 0(整段完全沒有價格區間)時 +DI 與 −DI 皆取 0;
/// <c>+DI + −DI</c> 為 0 時 DX 取 0,代表「完全沒有方向性」。
/// <b>Zero denominators.</b> A smoothed TR of 0 (a window with no range at all) yields +DI and -DI of 0, and a
/// zero <c>+DI + -DI</c> yields a DX of 0, meaning "no directionality whatsoever".
/// </para>
/// <para>
/// <b>典型用法。</b>ADX 只衡量趨勢強度不含方向,常以 20 或 25 作為「有無趨勢」的門檻,方向再看 +DI 與 −DI 孰大。
/// <b>Typical use.</b> ADX measures trend strength without direction; 20 or 25 are common thresholds for "trending",
/// with direction read from whether +DI or -DI is larger.
/// </para>
/// </remarks>
public sealed class AdxIndicator : IIndicator<Candle, AdxResult>
{
    private decimal _previousHigh;
    private decimal _previousLow;
    private decimal _previousClose;
    private bool _hasPreviousCandle;

    private decimal _smoothedTrueRange;
    private decimal _smoothedPlusDm;
    private decimal _smoothedMinusDm;
    private int _smoothingSeedCount;
    private bool _smoothingSeeded;

    private decimal _dxSum;
    private int _dxCount;
    private decimal _adx;
    private bool _adxSeeded;

    /// <summary>
    /// 建立指定週期的 ADX 指標。
    /// Creates an ADX indicator with the given period.
    /// </summary>
    /// <param name="period">期數,必須大於 0,Wilder 原著使用 14。Number of periods; must be greater than 0. Wilder's original uses 14.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period" /> 小於或等於 0。<paramref name="period" /> is less than or equal to 0.</exception>
    public AdxIndicator(int period = 14)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        Period = period;
        Name = FormattableString.Invariant($"ADX({period})");
    }

    /// <summary>期數。Number of periods.</summary>
    public int Period { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>
    /// 兩段 Wilder 平滑串接,需要 <c>2 × <see cref="Period" /></c> 根 K 線。
    /// Two stacked Wilder smoothing stages require <c>2 * <see cref="Period" /></c> candles.
    /// </summary>
    public int WarmupPeriod => 2 * Period;

    /// <inheritdoc />
    public bool IsReady => _adxSeeded;

    /// <inheritdoc />
    public long ProcessedCount { get; private set; }

    /// <inheritdoc />
    public AdxResult? Current { get; private set; }

    /// <summary>
    /// 目前的 +DI;方向指標比 ADX 早 <c><see cref="Period" /> − 1</c> 根 K 線可用,此屬性讓呼叫端提早取用。
    /// The current +DI. The directional indicators become available <c><see cref="Period" /> - 1</c> candles
    /// before ADX does, and this property exposes them during that gap.
    /// </summary>
    public decimal? PlusDi { get; private set; }

    /// <summary>
    /// 目前的 −DI,可用時機同 <see cref="PlusDi" />。
    /// The current -DI, available on the same schedule as <see cref="PlusDi" />.
    /// </summary>
    public decimal? MinusDi { get; private set; }

    /// <summary>
    /// 餵入一根 K 線並回傳當下的 ADX 三元組;資料量不足時回傳 <see langword="null" />。
    /// Feeds one candle and returns the current ADX triple; returns <see langword="null" /> until warmed up.
    /// </summary>
    /// <param name="input">K 線。The candle.</param>
    /// <returns>當下的 ADX 結果,未就緒時為 <see langword="null" />。The current ADX result, or <see langword="null" /> when not ready.</returns>
    public AdxResult? Update(Candle input)
    {
        ProcessedCount++;

        if (!_hasPreviousCandle)
        {
            RememberCandle(input);
            _hasPreviousCandle = true;
            Current = null;
            return null;
        }

        decimal trueRange = AtrIndicator.ComputeTrueRange(input, _previousClose);
        decimal upMove = input.High - _previousHigh;
        decimal downMove = _previousLow - input.Low;
        decimal plusDm = upMove > downMove && upMove > 0m ? upMove : 0m;
        decimal minusDm = downMove > upMove && downMove > 0m ? downMove : 0m;

        RememberCandle(input);

        if (!_smoothingSeeded)
        {
            _smoothedTrueRange += trueRange;
            _smoothedPlusDm += plusDm;
            _smoothedMinusDm += minusDm;
            _smoothingSeedCount++;

            if (_smoothingSeedCount < Period)
            {
                Current = null;
                return null;
            }

            _smoothingSeeded = true;
        }
        else
        {
            _smoothedTrueRange = _smoothedTrueRange - (_smoothedTrueRange / Period) + trueRange;
            _smoothedPlusDm = _smoothedPlusDm - (_smoothedPlusDm / Period) + plusDm;
            _smoothedMinusDm = _smoothedMinusDm - (_smoothedMinusDm / Period) + minusDm;
        }

        decimal plusDi = DecimalMath.TryDivide(100m * _smoothedPlusDm, _smoothedTrueRange, out decimal computedPlusDi)
            ? computedPlusDi
            : 0m;
        decimal minusDi = DecimalMath.TryDivide(100m * _smoothedMinusDm, _smoothedTrueRange, out decimal computedMinusDi)
            ? computedMinusDi
            : 0m;

        PlusDi = plusDi;
        MinusDi = minusDi;

        decimal directionalSum = plusDi + minusDi;
        decimal directionalIndex = DecimalMath.TryDivide(100m * Math.Abs(plusDi - minusDi), directionalSum, out decimal computedDx)
            ? computedDx
            : 0m;

        if (!_adxSeeded)
        {
            _dxSum += directionalIndex;
            _dxCount++;

            if (_dxCount < Period)
            {
                Current = null;
                return null;
            }

            _adx = _dxSum / Period;
            _adxSeeded = true;
        }
        else
        {
            _adx = ((_adx * (Period - 1)) + directionalIndex) / Period;
        }

        Current = new AdxResult(_adx, plusDi, minusDi);
        return Current;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _previousHigh = 0m;
        _previousLow = 0m;
        _previousClose = 0m;
        _hasPreviousCandle = false;
        _smoothedTrueRange = 0m;
        _smoothedPlusDm = 0m;
        _smoothedMinusDm = 0m;
        _smoothingSeedCount = 0;
        _smoothingSeeded = false;
        _dxSum = 0m;
        _dxCount = 0;
        _adx = 0m;
        _adxSeeded = false;
        ProcessedCount = 0;
        PlusDi = null;
        MinusDi = null;
        Current = null;
    }

    private void RememberCandle(Candle candle)
    {
        _previousHigh = candle.High;
        _previousLow = candle.Low;
        _previousClose = candle.Close;
    }
}
