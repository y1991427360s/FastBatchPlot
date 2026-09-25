using System;
using System.Collections.Generic;

namespace FastBatchPlot.Core.Models
{
    /// <summary>
    /// 图框标题栏与图签元数据
    /// </summary>
    public class TitleBlockInfo
    {
        public string DrawingNo { get; set; } = string.Empty;       // 图号 (如 S-01, 建施-02)
        public string DrawingName { get; set; } = string.Empty;     // 图名 (如 一层平面图)
        public string ProjectName { get; set; } = string.Empty;     // 项目/工程名称
        public string SubProject { get; set; } = string.Empty;      // 子项/单体名称
        public string Stage { get; set; } = string.Empty;           // 阶段 (施工图/方案/初设)
        public string Discipline { get; set; } = string.Empty;      // 专业 (建筑/结构/给排水/暖通/电气)
        public string Revision { get; set; } = string.Empty;        // 版次 (A, B, 0, 1)
        public string Date { get; set; } = string.Empty;            // 出图日期
        public string Scale { get; set; } = string.Empty;           // 图面标注比例 (如 1:100)
        public string Designer { get; set; } = string.Empty;        // 设计人
        public string Checker { get; set; } = string.Empty;         // 校对人
        public string Approver { get; set; } = string.Empty;        // 审核/审定人

        /// <summary>
        /// 图块中所有的动态属性或常规属性键值对
        /// </summary>
        public Dictionary<string, string> RawAttributes { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public override string ToString()
        {
            return $"[{DrawingNo}] {DrawingName}";
        }
    }
}
