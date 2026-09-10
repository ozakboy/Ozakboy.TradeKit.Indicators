using System.Globalization;

namespace Ozakboy.TradeKit.Indicators.Tests;

/// <summary>
/// 測試共用的比對輔助方法。
/// Shared comparison helpers for the tests.
/// </summary>
internal static class IndicatorAssert
{
    /// <summary>
    /// 對照獨立第二實作時的絕對容差,理由見 GoldenVectorProvenance.cs。
    /// Absolute tolerance used against the independent reference implementation; see GoldenVectorProvenance.cs.
    /// </summary>
    internal const decimal GoldenTolerance = 0.00000001m;

    /// <summary>把增量指標逐筆跑完一整段輸入,收集每一步的回傳值。Runs a streaming indicator over a whole series, collecting every step.</summary>
    internal static IReadOnlyList<TOutput?> RunStreaming<TInput, TOutput>(
        IIndicator<TInput, TOutput> indicator,
        IReadOnlyList<TInput> inputs)
        where TOutput : struct
    {
        TOutput?[] results = new TOutput?[inputs.Count];

        for (int i = 0; i < inputs.Count; i++)
        {
            results[i] = indicator.Update(inputs[i]);

            // 每一步的回傳值必須與 Current 一致,否則狀態同步有問題。
            // Each step's return value must agree with Current, otherwise state tracking is broken.
            Assert.AreEqual(results[i], indicator.Current, Describe("Update 與 Current 不一致", i));
        }

        return results;
    }

    /// <summary>比對一整段可為 null 的十進位序列。Compares an entire nullable decimal series.</summary>
    internal static void SequenceMatches(
        IReadOnlyList<decimal?> expected,
        IReadOnlyList<decimal?> actual,
        string label,
        decimal tolerance)
    {
        Assert.HasCount(expected.Count, actual, $"{label}:序列長度不同");

        for (int i = 0; i < expected.Count; i++)
        {
            ValueMatches(expected[i], actual[i], Describe(label, i), tolerance);
        }
    }

    /// <summary>比對單一可為 null 的十進位值。Compares a single nullable decimal value.</summary>
    internal static void ValueMatches(decimal? expected, decimal? actual, string label, decimal tolerance)
    {
        if (expected is null)
        {
            Assert.IsNull(actual, $"{label}:期望暖身期回傳 null,實際為 {Format(actual)}");
            return;
        }

        Assert.IsNotNull(actual, $"{label}:期望為 {Format(expected)},實際為 null");

        decimal difference = Math.Abs(expected.Value - actual.Value);
        Assert.IsLessThanOrEqualTo(
            tolerance,
            difference,
            $"{label}:期望 {Format(expected)},實際 {Format(actual)},差距 {Format(difference)} 超過容差 {Format(tolerance)}");
    }

    /// <summary>把兩段序列做「完全相等」比對,用於批次版與增量版的一致性。Requires exact equality, used for batch-versus-streaming consistency.</summary>
    internal static void SequencesAreIdentical<T>(
        IReadOnlyList<T?> batch,
        IReadOnlyList<T?> streaming,
        string label)
        where T : struct
    {
        Assert.HasCount(batch.Count, streaming, $"{label}:序列長度不同");

        for (int i = 0; i < batch.Count; i++)
        {
            Assert.AreEqual(
                batch[i],
                streaming[i],
                $"{label}[{i.ToString(CultureInfo.InvariantCulture)}]:批次版與增量版不一致");
        }
    }

    private static string Describe(string label, int index)
        => $"{label}[{index.ToString(CultureInfo.InvariantCulture)}]";

    private static string Format(decimal? value)
        => value?.ToString(CultureInfo.InvariantCulture) ?? "null";
}
