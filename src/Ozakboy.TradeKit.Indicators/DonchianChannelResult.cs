namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 唐奇安通道的三個輸出:N 期最高、N 期最低,以及兩者的中點。
/// The three Donchian Channel outputs: the N-period high, the N-period low, and their midpoint.
/// </summary>
/// <param name="Upper">上軌,視窗內的最高價。Upper channel, the highest high in the window.</param>
/// <param name="Middle">中軌 = (上軌 + 下軌) / 2。Middle channel = (upper + lower) / 2.</param>
/// <param name="Lower">下軌,視窗內的最低價。Lower channel, the lowest low in the window.</param>
public readonly record struct DonchianChannelResult(decimal Upper, decimal Middle, decimal Lower);
