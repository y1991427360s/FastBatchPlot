using System;
using System.Collections.Generic;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Paper
{
    public class PaperDetectionResult
    {
        public PaperSize Paper { get; set; }
        public double Scale { get; set; }
        public bool IsLandscape { get; set; }
        public double MatchScore { get; set; } // 0 (best) to 1

        public PaperDetectionResult(PaperSize paper, double scale, bool isLandscape, double score)
        {
            Paper = paper;
            Scale = scale;
            IsLandscape = isLandscape;
            MatchScore = score;
        }
    }

    /// <summary>
    /// 智能图纸幅面、比例与打印方向识别器
    /// </summary>
    public static class PaperSizeDetector
    {
        /// <summary>
        /// 自动识别接受的最大平均尺寸误差。标准图框通常在 0.5% 以内，伸出的会签栏约 1%~2%；
        /// 过宽的阈值会把表格、设备外框及大外框误认为图纸。
        /// </summary>
        public const double AcceptableMatchError = 0.03;
        private const double AcceptableRatioDifference = 0.05;
        public static void ValidatePreferredScale(double scale)
        {
            if(double.IsNaN(scale)||double.IsInfinity(scale)||scale<0||scale>1000000)
                throw new ArgumentException("识别比例必须为 0（自动）或不超过 1000000 的有限正数。");
        }

        /// <summary>用户明确指定比例时按实际范围生成纸张，不把非标范围强行匹配为标准纸。</summary>
        public static PaperDetectionResult AtExactScale(double width,double height,double scale)
        {
            ValidatePreferredScale(scale);
            if(scale==0||double.IsNaN(width)||double.IsInfinity(width)||double.IsNaN(height)||double.IsInfinity(height)||width<=0||height<=0)
                throw new ArgumentException("指定比例和图框尺寸必须为有限正数。");
            double w=width/scale,h=height/scale;
            if(double.IsInfinity(w)||double.IsInfinity(h)||w<=0||h<=0)throw new ArgumentException("指定比例后的纸张尺寸无效。");
            var paper=new PaperSize("自定义",w,h,w>=h);
            foreach(var standard in PaperSize.StandardSizes)
                if(Math.Abs(standard.LongerEdgeMm-Math.Max(w,h))<=1.5 && Math.Abs(standard.ShorterEdgeMm-Math.Min(w,h))<=1.5)
                {paper.Name=standard.Name;paper.StandardName=standard.StandardName;break;}
            return new PaperDetectionResult(paper,scale,w>=h,0);
        }
        /// <summary>
        /// 根据CAD中框选图形的长宽尺寸 (CAD图形单位)，自动推断最佳标准图幅与出图比例
        /// </summary>
        public static PaperDetectionResult Detect(double cadWidth, double cadHeight, double preferredScale = 0)
        {
            double w = Math.Abs(cadWidth);
            double h = Math.Abs(cadHeight);

            if (double.IsNaN(w) || double.IsNaN(h) || double.IsInfinity(w) || double.IsInfinity(h) || w < 1e-3 || h < 1e-3)
            {
                return new PaperDetectionResult(new PaperSize("A1", 841, 594, true), 100.0, true, 1.0);
            }

            bool isLandscape = w >= h;
            double cadLonger = Math.Max(w, h);
            double cadShorter = Math.Min(w, h);
            double cadRatio = cadLonger / cadShorter;

            PaperSize? bestPaper = null;
            double bestScale = 100.0;
            double lowestError = double.MaxValue;
            double bestCloseness = double.MaxValue;

            // 遍历所有标准图纸
            foreach (var standard in PaperSize.StandardSizes)
            {
                double stdLonger = standard.LongerEdgeMm;
                double stdShorter = standard.ShorterEdgeMm;
                double stdRatio = stdLonger / stdShorter;

                // 检查长宽比例差 (容差8%)
                double ratioDiff = Math.Abs(cadRatio - stdRatio) / stdRatio;
                if (ratioDiff > AcceptableRatioDifference)
                {
                    continue;
                }

                // 计算推断比例
                double rawScale = cadShorter / stdShorter;
                double candidateScale;

                if (preferredScale > 0)
                {
                    candidateScale = preferredScale;
                }
                else
                {
                    candidateScale = ScaleCalculator.MatchClosestStandardScale(rawScale);
                    if (candidateScale <= 0)
                    {
                        continue;
                    }
                }

                // 检验误差
                double expectedLonger = stdLonger * candidateScale;
                double expectedShorter = stdShorter * candidateScale;

                double errLonger = Math.Abs(cadLonger - expectedLonger) / expectedLonger;
                double errShorter = Math.Abs(cadShorter - expectedShorter) / expectedShorter;
                double totalError = (errLonger + errShorter) / 2.0;

                // 标准图幅 A0-A4 适当给予微小奖励权重 (优先匹配标准无加长图幅)
                if (!standard.Name.Contains("+"))
                {
                    totalError *= 0.95;
                }

                // A 系列图幅互为 2 倍关系（A3×2=840 与 A1=841 仅差 1mm），0.1% 量级的差异也要区分，
                // 因此以几何误差为主（布局中 1:1 的 A1 不能识别成 1:2 的 A3）。只有几何上完全等价
                // （如 A4×2=A2、A3+1×2=A1+1）时，才按对数距离优先更常用的比例（1:1、1:100 附近）。
                double closeness = Math.Abs(Math.Log10(candidateScale / 100.0));
                if (Math.Abs(candidateScale - 1.0) < 1e-9) closeness = 0; // 布局 1:1 出图最常见
                bool isBetter = totalError < lowestError - 4e-4 ||
                    (Math.Abs(totalError - lowestError) <= 4e-4 && closeness < bestCloseness);

                if (isBetter)
                {
                    lowestError = totalError;
                    bestPaper = standard;
                    bestScale = candidateScale;
                    bestCloseness = closeness;
                }
            }

            if (bestPaper == null || lowestError > AcceptableMatchError)
            {
                // 如果误差过大，说明根本不符合任何标准工程图幅和比例
                if (bestPaper == null)
                {
                    bestPaper = new PaperSize("A1", 841, 594, isLandscape);
                    bestScale = ScaleCalculator.MatchClosestStandardScale(cadShorter / 594.0);
                    lowestError = 1.0;
                }
            }

            var resultPaper = isLandscape ? bestPaper.Clone() : bestPaper.CloneRotated();

            // 与原版一致：纸张尺寸 = 打印范围 / 比例。范围超出标准纸 1.5mm 以上（如会签栏伸出外框）时
            // 按实际尺寸生成非标纸张，避免识别为标准纸后出图时才因放不下而失败；略小于标准纸时保留标准纸。
            if (lowestError < 1.0 && bestScale > 0)
            {
                double needLonger = cadLonger / bestScale, needShorter = cadShorter / bestScale;
                if (needLonger > bestPaper.LongerEdgeMm + 1.5 || needShorter > bestPaper.ShorterEdgeMm + 1.5)
                {
                    double width = isLandscape ? needLonger : needShorter, height = isLandscape ? needShorter : needLonger;
                    resultPaper = new PaperSize("自定义", Math.Round(width, 2), Math.Round(height, 2), isLandscape);
                }
            }

            return new PaperDetectionResult(resultPaper, bestScale, isLandscape, lowestError);
        }
    }
}
