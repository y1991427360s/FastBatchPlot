using FastBatchPlot.Core.Models;

namespace FastBatchPlot.CadBridge
{
    /// <summary>
    /// 仅用于同一 CAD 文档会话中的重试资格检查。返回值是保守的变更标记，
    /// 可包含宿主已知外参的内容摘要，但不是完整出图依赖摘要；不得持久化后用于跨会话自动绑定，也不能忽略失败后出现的变更。
    /// </summary>
    public interface ICadRevisionHost
    {
        string GetSourceRevision(PlotFrame frame);
    }
}
