namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 所有增量指標的共通狀態介面(不含輸入與輸出型別)。
/// State surface shared by every streaming indicator, independent of input and output types.
/// </summary>
/// <remarks>
/// 讓上層能以統一方式管理一組異質指標(檢查暖身狀態、重設),不必知道各自的輸入輸出型別。
/// Lets callers manage a heterogeneous set of indicators uniformly (warm-up checks, reset)
/// without knowing their concrete input and output types.
/// </remarks>
public interface IIndicator
{
    /// <summary>
    /// 指標名稱,含週期參數,例如 <c>SMA(20)</c>。適合寫入日誌與診斷輸出。
    /// Indicator name including its period parameters, e.g. <c>SMA(20)</c>; suitable for logs and diagnostics.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 產生第一個有效值所需的最少資料筆數。餵入的筆數少於此值時,指標永遠回報未就緒。
    /// Minimum number of inputs required before the first valid value can be produced;
    /// the indicator always reports not-ready until at least this many inputs have been fed.
    /// </summary>
    int WarmupPeriod { get; }

    /// <summary>
    /// 是否已通過暖身期並持有有效值。為 <see langword="false" /> 時不得使用任何輸出。
    /// Whether warm-up has completed and a valid value is available; no output may be used while this is <see langword="false" />.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// 自建立或上次 <see cref="Reset" /> 之後已處理的輸入筆數。
    /// Number of inputs processed since construction or the last <see cref="Reset" />.
    /// </summary>
    long ProcessedCount { get; }

    /// <summary>
    /// 清空所有內部狀態,回到剛建立的樣子(週期等建構參數保留)。
    /// Clears all internal state and returns the instance to its freshly-constructed condition
    /// (constructor parameters such as the period are retained).
    /// </summary>
    void Reset();
}

/// <summary>
/// 具體的增量指標介面:每次餵入一筆 <typeparamref name="TInput" />,回傳當下的指標值。
/// Strongly typed streaming indicator: feed one <typeparamref name="TInput" /> at a time and receive the current value.
/// </summary>
/// <typeparam name="TInput">輸入型別,通常是 <see cref="decimal" />(價格)或 <see cref="Candle" />。Input type, typically <see cref="decimal" /> (a price) or <see cref="Candle" />.</typeparam>
/// <typeparam name="TOutput">輸出型別,必須是實值型別,好讓「未就緒」以 <see langword="null" /> 精確表達。Output type; constrained to a value type so that "not ready" can be expressed exactly as <see langword="null" />.</typeparam>
/// <remarks>
/// <para>
/// 暖身期的約定是本函式庫的核心:資料量不足以產生有意義的值時一律回傳 <see langword="null" />,
/// 絕不回傳 0 或任何未定義的數字,避免上層策略在啟動初期拿垃圾數值下單。
/// The warm-up contract is central to this library: while there is not enough data to produce a
/// meaningful value the result is always <see langword="null" />, never 0 or an undefined number,
/// so that callers cannot act on garbage during start-up.
/// </para>
/// <para>
/// 實作皆非執行緒安全。同一個實例請由單一執行緒(或同一條 K 線處理流程)驅動。
/// Implementations are not thread-safe; drive each instance from a single thread or pipeline.
/// </para>
/// </remarks>
public interface IIndicator<TInput, TOutput> : IIndicator
    where TOutput : struct
{
    /// <summary>
    /// 最近一次 <see cref="Update(TInput)" /> 的結果;尚未就緒時為 <see langword="null" />。
    /// Result of the most recent <see cref="Update(TInput)" />; <see langword="null" /> while not ready.
    /// </summary>
    TOutput? Current { get; }

    /// <summary>
    /// 以增量方式餵入一筆資料並更新內部狀態,回傳當下的指標值;暖身期未滿時回傳 <see langword="null" />。
    /// Incrementally feeds one input, updates the internal state and returns the current value;
    /// returns <see langword="null" /> until warm-up completes.
    /// </summary>
    /// <param name="input">要餵入的資料。The input to feed.</param>
    /// <returns>當下的指標值,未就緒時為 <see langword="null" />。The current value, or <see langword="null" /> when not ready.</returns>
    TOutput? Update(TInput input);
}
