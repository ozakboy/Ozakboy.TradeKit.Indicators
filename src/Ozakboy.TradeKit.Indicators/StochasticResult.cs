namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// KD 隨機指標(Stochastic)的兩個輸出:K 值與其平滑後的 D 值。
/// The two Stochastic (KD) outputs: the K value and its smoothed D value.
/// </summary>
/// <param name="K">K 值 = RSV 的遞迴平滑,介於 0 到 100。K = recursively smoothed RSV, between 0 and 100.</param>
/// <param name="D">D 值 = K 值的遞迴平滑,介於 0 到 100。D = recursively smoothed K, between 0 and 100.</param>
public readonly record struct StochasticResult(decimal K, decimal D);
