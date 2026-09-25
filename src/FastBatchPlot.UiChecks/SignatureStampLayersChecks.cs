using System.Reflection;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckSignatureStampLayersUi()
    {
        using var form = new SignatureStampLayersForm("MS_Sign", "MS_Stamp");
        var signature = Field<TextBox>(form, "signature");
        var stamp = Field<TextBox>(form, "stamp");
        signature.Text = "项目|签名"; stamp.Text = "项目|印章";
        Check(form.SignatureLayerName == "MS_Sign", "修改图层映射草稿不提前应用");
        var read = form.GetType().GetMethod("ReadMapping", PrivateInstance)!;
        read.Invoke(form, null);
        Check(form.SignatureLayerName == "项目|签名" && form.StampLayerName == "项目|印章", "图层映射窗口读取两种独立完整名称");
        signature.Text = "0";
        try { read.Invoke(form, null); throw new Exception("通用层映射未拒绝"); }
        catch (TargetInvocationException ex) when (ex.InnerException is ArgumentException)
        { Check(form.SignatureLayerName == "项目|签名", "非法映射拒绝后保留上次有效设置"); }
        signature.Text = "项目|签名";
        form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual;
        form.Location = new System.Drawing.Point(-32000, -32000); form.Show(); form.Size = form.MinimumSize;
        form.PerformLayout();
        Check(signature.Visible && stamp.Visible && signature.Parent!.ClientRectangle.Contains(signature.Bounds) &&
            stamp.Parent!.ClientRectangle.Contains(stamp.Bounds), "最小窗口映射输入完整可见");
        using var bitmap = new System.Drawing.Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height));
        bitmap.Save(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "docs", "audit-evidence", "ui-signature-stamp-layers.png")));
    }
}
