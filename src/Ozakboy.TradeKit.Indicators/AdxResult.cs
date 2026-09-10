namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// ADX 的三個輸出:趨勢強度本身,以及構成它的正負方向指標。
/// The three ADX outputs: the trend-strength reading itself plus the directional indicators it is built from.
/// </summary>
/// <param name="Adx">平均趨向指標,0 到 100,只表達趨勢強度不表達方向。Average Directional Index, 0 to 100; measures trend strength only, not direction.</param>
/// <param name="PlusDi">正向指標 +DI,0 到 100。Plus Directional Indicator, 0 to 100.</param>
/// <param name="MinusDi">負向指標 −DI,0 到 100。Minus Directional Indicator, 0 to 100.</param>
public readonly record struct AdxResult(decimal Adx, decimal PlusDi, decimal MinusDi);
