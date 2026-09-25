using System;
using System.Collections.Generic;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Paper;

namespace FastBatchPlot.Core.Detection
{
    public class RawBlockCandidate
    {
        public string Handle { get; set; } = string.Empty;
        public string BlockName { get; set; } = string.Empty;
        public string Layer { get; set; } = string.Empty;
        public string SourceDocumentId { get; set; } = string.Empty;
        public string SourceFileName { get; set; } = string.Empty;
        public string SourceLayoutId { get; set; } = string.Empty;
        public string LayoutName { get; set; } = "Model";
        public int LayoutOrder { get; set; }
        public double InsertionX { get; set; }
        public double InsertionY { get; set; }
        public double ScaleX { get; set; } = 1.0;
        public double ScaleY { get; set; } = 1.0;
        public double RotationDegrees { get; set; } = 0.0;
        public Rect2D Bounds { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 图块型图框智能识别与属性提取引擎
    /// </summary>
    public static class BlockFrameDetector
    {
        public static List<PlotFrame> ProcessBlockCandidates(IEnumerable<RawBlockCandidate> candidates, double preferredScale = 0, DetectionReport? report = null)
        {
            PaperSizeDetector.ValidatePreferredScale(preferredScale);
            var frames = new List<PlotFrame>();
            int idx = 1;

            foreach (var cand in candidates)
            {
                var bounds = cand.Bounds;
                if (bounds.Width < 100 || bounds.Height < 100)
                {
                    report?.Add("尺寸小于 100×100", cand);
                    continue; // 忽略过小图块
                }

                // 若未显式传入指定比例，优先使用图块插入缩放比作为比例提示；镜像块的缩放为负，按绝对值比较。
                double scaleHint = preferredScale;
                double scaleX = Math.Abs(cand.ScaleX), scaleY = Math.Abs(cand.ScaleY);
                if (scaleHint <= 0 && scaleX > 0 && Math.Abs(scaleX - scaleY) <= 0.05 * scaleX)
                {
                    scaleHint = scaleX;
                }

                var detection = preferredScale>0?PaperSizeDetector.AtExactScale(bounds.Width,bounds.Height,preferredScale):PaperSizeDetector.Detect(bounds.Width, bounds.Height, scaleHint);
                // 插入缩放不是出图比例。未经用户指定的提示不吻合时重新推断。
                if (preferredScale <= 0 && detection.MatchScore > PaperSizeDetector.AcceptableMatchError && scaleHint > 0)
                {
                    detection = PaperSizeDetector.Detect(bounds.Width, bounds.Height);
                }
                if (detection.MatchScore > PaperSizeDetector.AcceptableMatchError)
                {
                    report?.Add("自动纸张匹配失败（可指定识别比例）", cand);
                    continue; // 忽略长宽比例或尺寸严重不符合图纸规格的普通图块（如设备块、家具块）
                }

                var titleInfo = ExtractTitleBlockInfo(cand.Attributes);

                var frame = new PlotFrame
                {
                    Id = idx,
                    OrderIndex = idx,
                    MinX = bounds.MinX,
                    MinY = bounds.MinY,
                    MaxX = bounds.MaxX,
                    MaxY = bounds.MaxY,
                    Type = FrameType.BlockReference,
                    SourceBlockName = cand.BlockName,
                    SourceLayer = cand.Layer,
                    HandleOrId = cand.Handle,
                    SourceDocumentId = cand.SourceDocumentId,
                    SourceFileName = cand.SourceFileName,
                    SourceLayoutId = cand.SourceLayoutId,
                    LayoutName = cand.LayoutName,
                    LayoutOrder = cand.LayoutOrder,
                    RotationDegrees = cand.RotationDegrees,
                    DetectedPaper = detection.Paper,
                    CalculatedScale = detection.Scale,
                    IsLandscape = detection.IsLandscape,
                    TitleInfo = titleInfo
                };

                frames.Add(frame);
                idx++;
            }

            return frames;
        }

        public static TitleBlockInfo ExtractTitleBlockInfo(IDictionary<string, string> attributes)
        {
            var info = new TitleBlockInfo();
            if (attributes == null) return info;

            foreach (var kvp in attributes)
            {
                info.RawAttributes[kvp.Key] = kvp.Value;
                string key = kvp.Key.Trim().ToUpperInvariant();
                string val = kvp.Value.Trim();

                if (string.IsNullOrEmpty(val)) continue;

                // 图号匹配
                if (string.IsNullOrEmpty(info.DrawingNo))
                {
                    if (key.Contains("图号") || key == "DWG_NO" || key == "DWGNO" || key == "SHEET_NO" || key == "SHEETNO" || key == "NUMBER" || key == "DRAWING_NO" || key == "A")
                    {
                        info.DrawingNo = val;
                        continue;
                    }
                }

                // 图名匹配
                if (string.IsNullOrEmpty(info.DrawingName))
                {
                    if (key.Contains("图名") || key == "DWG_NAME" || key == "DWGNAME" || key == "SHEET_TITLE" || key == "TITLE" || key == "DRAWING_NAME" || key == "SUB_TITLE" || key == "B")
                    {
                        info.DrawingName = val;
                        continue;
                    }
                }

                // 工程名称匹配
                if (string.IsNullOrEmpty(info.ProjectName))
                {
                    if (key.Contains("工程名称") || key.Contains("项目名称") || key == "PROJECT" || key == "PROJECT_NAME" || key == "PROJ_NAME" || key == "C")
                    {
                        info.ProjectName = val;
                        continue;
                    }
                }

                // 版次
                if (string.IsNullOrEmpty(info.Revision))
                {
                    if (key.Contains("版次") || key.Contains("版本") || key == "REV" || key == "REVISION" || key == "D")
                    {
                        info.Revision = val;
                        continue;
                    }
                }

                // 日期
                if (string.IsNullOrEmpty(info.Date))
                {
                    if (key.Contains("日期") || key == "DATE" || key == "E")
                    {
                        info.Date = val;
                        continue;
                    }
                }

                // 比例
                if (string.IsNullOrEmpty(info.Scale))
                {
                    if (key.Contains("比例") || key == "SCALE" || key == "F")
                    {
                        info.Scale = val;
                        continue;
                    }
                }
            }

            return info;
        }
    }
}
