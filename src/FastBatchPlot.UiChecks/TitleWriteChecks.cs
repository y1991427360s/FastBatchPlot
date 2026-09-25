using System.Reflection;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private sealed class WriteHost : ICadTitleWriteHost
    {
        public bool Fail;public int Applies;
        public TitleWritePlan PrepareTitleWrite(PlotFrame f,TitleBlockTemplate t,IDictionary<TitleField,string> v)=>TitleWritePlanner.Create(f,t,v,new[]{new TemplateText{Text="旧号",RawText="旧号",Kind="Attribute",Handle="1",Owner="F",AttributeTag="NO",WriteBlockReason=""}});
        public void ApplyTitleWrites(IReadOnlyList<TitleWritePlan> p,bool undo=false){if(Fail)throw new InvalidOperationException("模拟旧值冲突");Applies++;}
    }
    private static void CheckTitleWriteUi()
    {
        var frame=new PlotFrame{OrderIndex=1,Type=FrameType.BlockReference,HandleOrId="F",SourceDocumentId="doc",SourceLayoutId="space",TitleInfo=new TitleBlockInfo{DrawingNo="新号"}};
        var template=new TitleBlockTemplate{Name="A",BlockName="A",Fields=new List<TitleFieldRule>{new(){Field=TitleField.DrawingNo,AttributeTag="NO"}}};var host=new WriteHost();
        using var dialog=new TitleWriteForm(new[]{frame},(f,v)=>host.PrepareTitleWrite(f,template,v));
        typeof(TitleWriteForm).GetMethod("BuildPreview",PrivateInstance)!.Invoke(dialog,null);
        Check(Field<Button>(dialog,"apply").Enabled&&dialog.Plans.Count==1,"写回预览通过唯一目标后才能应用");
        Check(dialog.Plans[0].Changes[0].Before=="旧号"&&dialog.Plans[0].Changes[0].After=="新号","写回预览保留源图原值及新值");
        Field<TextBox>(dialog,"value").Text="统一版次";Check(!Field<Button>(dialog,"apply").Enabled&&dialog.Plans.Count==0,"编辑预览条件后清除旧计划并禁用写入");
        typeof(TitleWriteForm).GetMethod("BuildPreview",PrivateInstance)!.Invoke(dialog,null);
        dialog.ShowInTaskbar=false;dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new System.Drawing.Point(-32000,-32000);dialog.Show();
        string file=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","..","docs","audit-evidence","ui-title-write.png"));
        using(var image=new System.Drawing.Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,image.Width,image.Height));image.Save(file);}
        using var form=new BatchPlotForm(null);var list=Field<List<PlotFrame>>(form,"_frames");list.Add(frame);
        var plan=host.PrepareTitleWrite(frame,template,new Dictionary<TitleField,string>{{TitleField.DrawingNo,"统一号"}});
        host.Fail=true;Reject(()=>Call(form,"CommitTitleWrites",host,new List<TitleWritePlan>{plan}),"宿主旧值冲突时整批写回报错");
        Check(frame.TitleInfo.DrawingNo=="新号"&&host.Applies==0,"写回失败不更新列表信息");host.Fail=false;Call(form,"CommitTitleWrites",host,new List<TitleWritePlan>{plan});
        Check(frame.TitleInfo.DrawingNo=="统一号"&&frame.Status.Contains("未保存")&&host.Applies==1,"写回成功后更新列表并说明DWG未保存");
        Check(Field<List<TitleWritePlan>>(form,"_lastTitleWrite").Count==1,"成功写回保留最近一次撤销计划");
        var items=Field<ContextMenuStrip>(form,"ctxMenu").Items.Cast<ToolStripItem>();Check(items.Any(i=>i.Text?.Contains("标题栏写回")==true),"列表菜单提供标题栏写回入口");
    }
}
