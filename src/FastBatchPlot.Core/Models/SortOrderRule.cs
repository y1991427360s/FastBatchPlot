namespace FastBatchPlot.Core.Models
{
    /// <summary>
    /// 批量打印图纸排序规则
    /// </summary>
    public enum SortOrderRule
    {
        /// <summary>
        /// 从左到右，从上到下 (经典图纸排版习惯)
        /// </summary>
        LeftToRight_TopToBottom = 0,

        /// <summary>
        /// 从上到下，从左到右 (竖向分栏排版)
        /// </summary>
        TopToBottom_LeftToRight = 1,

        /// <summary>
        /// 按图纸图号自然排序 (如 01, 02, 10)
        /// </summary>
        ByDrawingNo = 2,

        /// <summary>
        /// 按图框图块定义优先级 (目录优先、再施工图、再详图)
        /// </summary>
        ByBlockPriority = 3,

        /// <summary>
        /// 手工拖拽自定义顺序
        /// </summary>
        Custom = 4
    }
}
