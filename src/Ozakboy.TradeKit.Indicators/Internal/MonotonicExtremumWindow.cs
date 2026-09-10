namespace Ozakboy.TradeKit.Indicators.Internal;

/// <summary>
/// 以單調雙端佇列維護滑動視窗的最大值或最小值,攤還成本為 O(1),記憶體為 O(週期)。
/// Sliding-window maximum or minimum maintained by a monotonic deque: O(1) amortised per update, O(period) memory.
/// </summary>
/// <remarks>
/// 佇列中只保留「還可能成為極值」的元素:新元素進場時,所有被它支配的舊元素直接丟棄,
/// 因此佇列由頭到尾單調遞減(求最大值)或遞增(求最小值),頭部永遠是當前視窗的極值。
/// 每個元素最多進出佇列各一次,所以雖然單次更新可能彈出多筆,長期攤還仍是常數時間。
/// The deque only keeps elements that could still become the extremum: an incoming element discards every
/// older element it dominates, so the deque stays monotonically decreasing (for maximum) or increasing
/// (for minimum) and its head is always the current window's extremum. Each element enters and leaves at
/// most once, so a single update may pop several entries yet the amortised cost stays constant.
/// </remarks>
internal sealed class MonotonicExtremumWindow
{
    private readonly decimal[] _values;
    private readonly long[] _sequences;
    private readonly int _period;
    private readonly bool _trackMaximum;
    private int _head;
    private int _size;
    private long _nextSequence;

    /// <summary>建立指定週期的極值視窗。Creates an extremum window for the given period.</summary>
    /// <param name="period">視窗長度。Window length.</param>
    /// <param name="trackMaximum"><see langword="true" /> 追蹤最大值,否則追蹤最小值。<see langword="true" /> to track the maximum, otherwise the minimum.</param>
    internal MonotonicExtremumWindow(int period, bool trackMaximum)
    {
        _period = period;
        _trackMaximum = trackMaximum;

        // 容量必須是 period + 1:Add 是「先推入、再淘汰滑出視窗的那一筆」,
        // 所以佇列會短暫地同時持有 period + 1 筆。若只配置 period,推入時的環形索引
        // 會正好覆蓋掉佇列頭部那筆尚未淘汰的元素,極值就會憑空變成錯的。
        // Capacity must be period + 1: Add pushes first and only then evicts whatever has slid out of the
        // window, so the deque transiently holds period + 1 entries. Sizing it at period makes the push
        // wrap around onto the head entry that has not been evicted yet, silently corrupting the extremum.
        _values = new decimal[period + 1];
        _sequences = new long[period + 1];
    }

    /// <summary>目前視窗的極值。僅在至少推入過一筆資料後才有意義。Current window extremum; meaningful only after at least one push.</summary>
    internal decimal Extremum => _values[_head];

    /// <summary>推入一筆新資料並淘汰滑出視窗的舊資料。Pushes a value and evicts whatever has slid out of the window.</summary>
    internal void Add(decimal value)
    {
        int capacity = _values.Length;

        while (_size > 0)
        {
            int lastIndex = (_head + _size - 1) % capacity;
            decimal last = _values[lastIndex];
            bool dominated = _trackMaximum ? last <= value : last >= value;

            if (!dominated)
            {
                break;
            }

            _size--;
        }

        int writeIndex = (_head + _size) % capacity;
        _values[writeIndex] = value;
        _sequences[writeIndex] = _nextSequence;
        _size++;

        // 剛推入的元素序號必定在視窗內,所以此處最多只會淘汰一筆,佇列不會被清空。
        // The just-pushed element is always inside the window, so at most one entry is evicted here.
        if (_sequences[_head] <= _nextSequence - _period)
        {
            _head = (_head + 1) % capacity;
            _size--;
        }

        _nextSequence++;
    }

    /// <summary>清空視窗。Clears the window.</summary>
    internal void Clear()
    {
        _head = 0;
        _size = 0;
        _nextSequence = 0;
    }
}
