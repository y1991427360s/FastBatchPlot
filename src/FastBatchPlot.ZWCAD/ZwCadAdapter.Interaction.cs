using System;
using ZwSoft.ZwCAD.EditorInput;

namespace FastBatchPlot.ZWCAD
{
    public partial class ZwCadAdapter
    {
        /// <summary>
        /// 从对话框发起图面拾取前调用：模态对话框必须通过 StartUserInteraction 暂时交还 CAD 主窗口，
        /// 否则主窗口处于禁用状态，拾取无法点击；非模态窗口则把焦点交给图形窗口，第一次点击即可生效。
        /// </summary>
        internal static IDisposable? BeginUserInteraction(Editor editor)
        {
            try
            {
                // 调用方通常已先 Hide() 对话框，此时 ActiveForm 为空；按打开顺序取最内层的模态窗口（隐藏时 Modal 仍为 true）。
                System.Windows.Forms.Form? modal = null;
                foreach (System.Windows.Forms.Form form in System.Windows.Forms.Application.OpenForms)
                    if (!form.IsDisposed && form.Modal) modal = form;
                if (modal != null) return editor.StartUserInteraction(modal);
            }
            catch { }
            try { ZwSoft.ZwCAD.Internal.Utils.SetFocusToDwgView(); } catch { }
            return null;
        }
    }
}
