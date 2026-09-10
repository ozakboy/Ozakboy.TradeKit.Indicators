// =====================================================================================
// 黃金向量的來源與驗證方式(GOLDEN VECTOR PROVENANCE)
// =====================================================================================
//
// 指標算錯的話,上層所有策略與回測結論全部無效,而且很難發現。因此每個指標的期望值都必須
// 有可複查的出處。本專案採用三層交叉驗證,以下逐項說明,任何人都能自己重跑一次。
//
// If an indicator is wrong, every strategy and backtest conclusion built on it is silently
// invalid. Every expected value below therefore has a traceable origin. Three layers of
// cross-checking are used, all of them reproducible by hand.
//
// -------------------------------------------------------------------------------------
// 第一層:公式出處(FORMULAS)
// -------------------------------------------------------------------------------------
// 所有公式逐字取自下列公開來源,不是憑記憶寫的:
//
//   RSI  StockCharts ChartSchool — Relative Strength Index (RSI)
//        https://chartschool.stockcharts.com/table-of-contents/technical-indicators-and-overlays/technical-indicators/relative-strength-index-rsi
//        取用內容:「First Average Gain = Sum of Gains over the past 14 periods / 14」、
//        「Average Gain = [(previous Average Gain) x 13 + current Gain] / 14」,以及
//        平均跌幅為 0 時 RSI 定義為 100、平均漲幅為 0 時 RSI 為 0。
//
//   ATR  StockCharts ChartSchool — Average True Range (ATR)
//        https://chartschool.stockcharts.com/table-of-contents/technical-indicators-and-overlays/technical-indicators/average-true-range-atr
//        取用內容:第一根的 TR 取「High − Low」、首個 14 期 ATR 為前 14 個 TR 的平均、
//        之後「Current ATR = [(Prior ATR x 13) + Current TR] / 14」。
//
//   ADX  StockCharts ChartSchool — Average Directional Index (ADX)
//        https://chartschool.stockcharts.com/table-of-contents/technical-indicators-and-overlays/technical-indicators/average-directional-index-adx
//        取用內容:「First TR14 = Sum of first 14 periods of TR1」、
//        「Subsequent Values = Prior TR14 − (Prior TR14 / 14) + Current TR1」(+DM、−DM 同法)、
//        「+DI14 = (14-day smoothed +DM / 14-day smoothed TR) x 100」、
//        「DX = |+DI14 − −DI14| / (+DI14 + −DI14) x 100」、
//        「First ADX14 = 14-period average of DX」、
//        「Subsequent ADX14 = ((Prior ADX14 x 13) + Current DX) / 14」。
//
//   其餘 Wilder 系指標的原始定義:
//        Wilder, J. Welles (1978), New Concepts in Technical Trading Systems.
//
//   SMA / EMA / MACD / Bollinger Bands / Donchian Channel 的定義為公認的初等公式,
//   已直接寫在各指標類別的 <remarks> 中,並以下述「手算可驗證案例」逐一釘死。
//
// -------------------------------------------------------------------------------------
// 第二層:公開資料集與公開對照值(PUBLISHED ANCHOR)
// -------------------------------------------------------------------------------------
// GoldenVectors.WilderCloses 是 Wilder (1978) RSI 章節的範例收盤價,被 StockCharts 的 RSI
// 教材重製為試算表並廣泛轉載(44.3389、44.0902、44.1497、43.6124 … 43.1314,共 33 筆)。
// 這組資料最廣為流傳的對照值是「第 15 筆(索引 14)的 14 期 RSI 約為 70.53」。
// RsiTests.Rsi14_FirstValue_MatchesPublishedReferenceValue 就是釘這個數字。
//
// 說明其限制:StockCharts 與同類教材把完整輸出表放在圖片與可下載的試算表裡,純文字頁面
// 取不到,因此除了 70.53 這個公開錨點之外,完整序列的期望值來自第三層。
//
// -------------------------------------------------------------------------------------
// 第三層:獨立第二實作(INDEPENDENT REFERENCE IMPLEMENTATION)
// -------------------------------------------------------------------------------------
// GoldenVectors.cs 內的期望值陣列由一份獨立的 Python 參考實作產生。該實作:
//   * 以另一種語言、另一套十進位型別(Python decimal,precision 28)撰寫;
//   * 直接照上述公開來源的公式文字轉寫,撰寫時不參考本專案的 C# 程式碼;
//   * 種子固定,輸出可完全重現。
// 它與本函式庫在 70.53 這個公開錨點上一致,代表兩者對公式的理解相同。
//
// 這一層抓得到的是實作錯誤(索引錯位、暖身期算錯、平滑順序寫反、狀態沒清乾淨),
// 抓不到的是「兩份實作對公式有相同誤解」。因此第一層的逐字引用與第二層的公開錨點缺一不可。
//
// -------------------------------------------------------------------------------------
// 手算可驗證案例(HAND-CHECKABLE CASES)
// -------------------------------------------------------------------------------------
// 除了長序列之外,每個指標另有一組小到可以用計算機驗算的案例,直接寫在各測試方法的註解裡:
//   SMA        [1..5] 的 SMA(3) = 2, 3, 4
//   EMA        k = 2/(3+1) = 0.5,種子 (1+2+3)/3 = 2,下一筆 (4−2)×0.5+2 = 3
//   Bollinger  教科書標準差範例 [2,4,4,4,5,5,7,9]:平均 5、母體標準差恰為 2
//   Donchian   小序列直接讀最高最低價
//   ATR        跳空缺口時 TR 必須取「高 − 前收」而非「高 − 低」
//   RSI        連續上漲 → 100、連續下跌 → 0、完全持平 → 50
//   ADX        零波動序列不得除以零,+DI / −DI / DX 全為 0
//   Sqrt       完全平方數必須精確(2.25 → 1.5),以及 x² 回推誤差的相對量級
//
// -------------------------------------------------------------------------------------
// 容差(TOLERANCE)
// -------------------------------------------------------------------------------------
// 對照第二實作時採用 1e-8 絕對容差:Python decimal 與 .NET decimal 的除法都取 28 位有效數字,
// 但尾數的進位規則不完全相同,理論上可能在第 28 位有差異。指標值的量級在 1 到 100 之間,
// 1e-8 的容差比任何可能的尾數差異大上二十個數量級,同時仍能抓出任何真正的公式或索引錯誤。
//
// 批次版與增量版之間不使用容差,一律要求「完全相等」——那是同一套算術、同樣的運算順序,
// 對不上就是有 bug,不是精度問題。
// =====================================================================================
