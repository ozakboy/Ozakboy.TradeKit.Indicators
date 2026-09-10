namespace Ozakboy.TradeKit.Indicators.Internal;

/// <summary>
/// 固定容量的環形緩衝區,供需要滑動視窗的指標使用。容量在建構時決定,之後不再配置記憶體。
/// Fixed-capacity ring buffer for indicators that need a sliding window; allocates once at construction.
/// </summary>
internal sealed class DecimalRingBuffer
{
    private readonly decimal[] _items;
    private int _writeIndex;
    private int _count;

    /// <summary>建立指定容量的環形緩衝區。Creates a ring buffer with the given capacity.</summary>
    internal DecimalRingBuffer(int capacity)
    {
        _items = new decimal[capacity];
    }

    /// <summary>緩衝區容量。Buffer capacity.</summary>
    internal int Capacity => _items.Length;

    /// <summary>目前持有的元素個數。Number of elements currently held.</summary>
    internal int Count => _count;

    /// <summary>是否已裝滿。Whether the buffer is full.</summary>
    internal bool IsFull => _count == _items.Length;

    /// <summary>依時間順序取值,索引 0 為最舊。Chronological access; index 0 is the oldest element.</summary>
    internal decimal this[int index]
    {
        get
        {
            int start = (_writeIndex - _count + _items.Length) % _items.Length;
            return _items[(start + index) % _items.Length];
        }
    }

    /// <summary>
    /// 加入一個元素;若緩衝區已滿,最舊的元素會被擠出並由 <paramref name="evicted" /> 帶回。
    /// Adds an element; when the buffer is full the oldest element is evicted and returned via <paramref name="evicted" />.
    /// </summary>
    internal bool Add(decimal item, out decimal evicted)
    {
        bool wasFull = IsFull;
        evicted = wasFull ? _items[_writeIndex] : 0m;

        _items[_writeIndex] = item;
        _writeIndex = (_writeIndex + 1) % _items.Length;

        if (!wasFull)
        {
            _count++;
        }

        return wasFull;
    }

    /// <summary>清空緩衝區。Clears the buffer.</summary>
    internal void Clear()
    {
        Array.Clear(_items);
        _writeIndex = 0;
        _count = 0;
    }
}
