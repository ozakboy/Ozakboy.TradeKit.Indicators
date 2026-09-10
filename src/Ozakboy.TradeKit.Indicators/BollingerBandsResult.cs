namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 布林通道的四個輸出:中軌、上軌、下軌與該視窗的母體標準差。
/// The four Bollinger Bands outputs: middle, upper and lower band plus the window's population standard deviation.
/// </summary>
/// <param name="Middle">中軌,即 N 期簡單移動平均。Middle band, the N-period simple moving average.</param>
/// <param name="Upper">上軌 = 中軌 + 倍數 × 標準差。Upper band = middle + multiplier * standard deviation.</param>
/// <param name="Lower">下軌 = 中軌 − 倍數 × 標準差。Lower band = middle - multiplier * standard deviation.</param>
/// <param name="StandardDeviation">視窗內的母體標準差(除以 N,非 N−1)。Population standard deviation over the window (divided by N, not N-1).</param>
public readonly record struct BollingerBandsResult(
    decimal Middle,
    decimal Upper,
    decimal Lower,
    decimal StandardDeviation);
