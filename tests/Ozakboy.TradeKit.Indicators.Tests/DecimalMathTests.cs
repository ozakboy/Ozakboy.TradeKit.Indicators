using System.Globalization;

namespace Ozakboy.TradeKit.Indicators.Tests;

/// <summary>
/// decimal 平方根的精確性、收斂性與邊界測試,以及安全除法。
/// Accuracy, convergence and boundary tests for the decimal square root, plus the safe division helper.
/// </summary>
/// <remarks>
/// 布林通道的標準差完全依賴這個平方根,它算錯的話通道寬度就是錯的,而錯得很隱晦。
/// Bollinger's standard deviation depends entirely on this square root; if it is wrong the band width is
/// wrong in a way that is easy to miss.
/// </remarks>
[TestClass]
public sealed class DecimalMathTests
{
    /// <summary>
    /// 完全平方數必須回傳精確值,不能是 1.4999…9 或 1.5000…1 這種「幾乎對」的結果。
    /// </summary>
    [TestMethod]
    [DataRow("0", "0")]
    [DataRow("1", "1")]
    [DataRow("4", "2")]
    [DataRow("9", "3")]
    [DataRow("2.25", "1.5")]
    [DataRow("0.0001", "0.01")]
    [DataRow("100", "10")]
    [DataRow("10000", "100")]
    [DataRow("1000000", "1000")]
    [DataRow("0.00000001", "0.0001")]
    [DataRow("0.00000000000000000000000004", "0.0000000000002")]
    public void Sqrt_PerfectSquares_AreExact(string radicand, string expected)
    {
        decimal value = decimal.Parse(radicand, CultureInfo.InvariantCulture);
        decimal want = decimal.Parse(expected, CultureInfo.InvariantCulture);

        Assert.AreEqual(want, DecimalMath.Sqrt(value), $"sqrt({radicand}) 應精確等於 {expected}");
    }

    /// <summary>
    /// 非完全平方數:回代 x² 之後的相對誤差必須小於 1e-26,也就是用滿 decimal 的有效位數。
    /// </summary>
    [TestMethod]
    [DataRow("2")]
    [DataRow("3")]
    [DataRow("5")]
    [DataRow("10")]
    [DataRow("0.5")]
    [DataRow("0.02")]
    [DataRow("1234.5678")]
    [DataRow("0.000000123456789")]
    [DataRow("98765432109876.54321")]
    public void Sqrt_RoundTripRelativeError_IsWithinDecimalPrecision(string radicand)
    {
        decimal value = decimal.Parse(radicand, CultureInfo.InvariantCulture);

        decimal root = DecimalMath.Sqrt(value);
        decimal relativeError = Math.Abs((root * root) - value) / value;

        Assert.IsLessThan(
            0.00000000000000000000000001m,
            relativeError,
            $"sqrt({radicand}) 回代誤差 {relativeError.ToString(CultureInfo.InvariantCulture)} 過大");
    }

    /// <summary>
    /// 已知的十位小數對照值,確認不是「自己跟自己比」的空轉。
    /// </summary>
    [TestMethod]
    public void Sqrt_KnownIrrationalRoots_MatchPublishedDigits()
    {
        Assert.AreEqual(1.4142135624m, Math.Round(DecimalMath.Sqrt(2m), 10), "√2");
        Assert.AreEqual(1.7320508076m, Math.Round(DecimalMath.Sqrt(3m), 10), "√3");
        Assert.AreEqual(2.2360679775m, Math.Round(DecimalMath.Sqrt(5m), 10), "√5");
        Assert.AreEqual(3.1622776602m, Math.Round(DecimalMath.Sqrt(10m), 10), "√10");
    }

    /// <summary>
    /// 單調性:平方根必須隨被開方數遞增,任何一處反轉都代表迭代在某個量級沒有收斂。
    /// </summary>
    [TestMethod]
    public void Sqrt_IsMonotonicAcrossManyMagnitudes()
    {
        decimal previous = -1m;

        for (int exponent = -20; exponent <= 20; exponent++)
        {
            for (int multiplier = 1; multiplier <= 9; multiplier++)
            {
                decimal value = multiplier * Pow10(exponent);
                decimal root = DecimalMath.Sqrt(value);

                Assert.IsGreaterThan(previous, root, $"sqrt({value.ToString(CultureInfo.InvariantCulture)}) 未遞增");
                previous = root;
            }
        }
    }

    /// <summary>
    /// 收斂性:極端量級也必須在迭代上限內停下來並給出正確結果,不能靠迭代上限硬截斷。
    /// </summary>
    [TestMethod]
    public void Sqrt_ExtremeMagnitudes_ConvergeToCorrectOrderOfMagnitude()
    {
        decimal tiny = DecimalMath.Sqrt(0.0000000000000000000000000001m);   // 1e-28
        Assert.AreEqual(0.00000000000001m, tiny, "√1e-28 應為 1e-14");

        decimal huge = DecimalMath.Sqrt(decimal.MaxValue);
        Assert.IsTrue(huge > 281474976710000m && huge < 281474976711000m, $"√decimal.MaxValue 量級不對:{huge}");
    }

    [TestMethod]
    public void Sqrt_NegativeValue_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => DecimalMath.Sqrt(-1m));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => DecimalMath.Sqrt(-0.0000000001m));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => DecimalMath.Sqrt(decimal.MinValue));
    }

    [TestMethod]
    public void TryDivide_NonZeroDivisor_Succeeds()
    {
        Assert.IsTrue(DecimalMath.TryDivide(10m, 4m, out decimal result));
        Assert.AreEqual(2.5m, result);
    }

    [TestMethod]
    public void TryDivide_ZeroDivisor_ReportsFailureInsteadOfThrowing()
    {
        Assert.IsFalse(DecimalMath.TryDivide(10m, 0m, out decimal result));
        Assert.AreEqual(0m, result);

        Assert.IsFalse(DecimalMath.TryDivide(0m, 0m, out decimal zeroOverZero));
        Assert.AreEqual(0m, zeroOverZero);
    }

    private static decimal Pow10(int exponent)
    {
        decimal result = 1m;

        for (int i = 0; i < Math.Abs(exponent); i++)
        {
            result = exponent >= 0 ? result * 10m : result / 10m;
        }

        return result;
    }
}
