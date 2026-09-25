using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using FastBatchPlot.Core.Assets;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;

internal static class Program
{
    private static void CheckRaster(byte[] png)
    {
        string directory=Path.Combine(Path.GetTempPath(),"FastBatchPlot.FrameworkChecks-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string input=Path.Combine(directory,"input.png"),output=Path.Combine(directory,"output.png");
        try
        {
            File.WriteAllBytes(input,png);
            var frame=new PlotFrame{MaxX=12,MaxY=6,CalculatedScale=1,DetectedPaper=new PaperSize{Name="test",WidthMm=12,HeightMm=6}};
            PlotFileFormats.ValidateAndCommit(input,output,PlotExportFormat.PNG,PlotPlanBuilder.Create(frame,new PlotConfig()));
            if(!File.Exists(output)||File.Exists(input))throw new Exception("图片验证或提交失败。");
            Console.WriteLine("NET48_RASTER_VALIDATION_OK");
        }
        finally {if(File.Exists(input))File.Delete(input);if(File.Exists(output))File.Delete(output);Directory.Delete(directory);}
    }
    private static void CheckPdf()
    {
        string directory=Path.Combine(Path.GetTempPath(),"FastBatchPlot.PdfFramework-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string source=Path.Combine(directory,"source.pdf"),single=Path.Combine(directory,"single.pdf"),merged=Path.Combine(directory,"merged.pdf");
            using(var document=new PdfSharpCore.Pdf.PdfDocument())
            {
                var page=document.AddPage();
                page.Width=PdfSharpCore.Drawing.XUnit.FromMillimeter(420);
                page.Height=PdfSharpCore.Drawing.XUnit.FromMillimeter(297);
                document.Save(source);
            }
            var frame=new PlotFrame{MaxX=420,MaxY=297,CalculatedScale=1,DetectedPaper=new PaperSize("A3",420,297)};
            PlotOutputCommitter.ValidateAndCommit(source,single,PlotPlanBuilder.Create(frame,new PlotConfig()));
            var items=new[]{new FastBatchPlot.Core.Pdf.PdfMergeItem(single,"第一页")};
            if(!FastBatchPlot.Core.Pdf.PdfMerger.MergePdfFiles(items,merged,out var error,false))throw new Exception(error);
            using(var pdf=PdfSharpCore.Pdf.IO.PdfReader.Open(merged,PdfSharpCore.Pdf.IO.PdfDocumentOpenMode.Import))
                if(pdf.PageCount!=1)throw new Exception("合并页数错误。");
            using(var cancel=new System.Threading.CancellationTokenSource())
            {
                cancel.Cancel();bool rejected=false;
                try{FastBatchPlot.Core.Pdf.PdfMerger.MergePdfFiles(items,Path.Combine(directory,"cancelled.pdf"),cancel.Token,out _,false);}
                catch(OperationCanceledException){rejected=true;}
                if(!rejected||File.Exists(Path.Combine(directory,"cancelled.pdf")))throw new Exception("PDF取消失败。");
            }
            frame.SourceDocumentId="offline-session";frame.SourceLayoutId="offline-space";
            var taskConfig=new PlotConfig{OutputDirectory=directory};
            var run=new FastBatchPlot.Core.Tasks.BatchPlotRun(new[]{new FastBatchPlot.Core.Tasks.BatchPage(frame,single)},taskConfig);
            var record=FastBatchPlot.Core.Tasks.PdfTaskHistory.Create(run,merged);
            FastBatchPlot.Core.Tasks.PdfTaskHistory.RecordPage(record,0,FastBatchPlot.Core.Tasks.BatchPageState.Succeeded,"");
            string history=Path.Combine(directory,"history.json");
            FastBatchPlot.Core.Tasks.PdfTaskHistory.Save(history,record);
            var loaded=FastBatchPlot.Core.Tasks.PdfTaskHistory.Load(history);
            FastBatchPlot.Core.Tasks.PdfTaskHistory.ValidateCompleted(loaded);
            if(FastBatchPlot.Core.Tasks.PdfTaskHistory.PendingPages(loaded).Count!=0)throw new Exception("成功页面被重复安排。");
            Console.WriteLine("NET48_PDF_HISTORY_OK");
            Console.WriteLine("NET48_PDF_SCOPE_OK");
        }
        finally{foreach(string file in Directory.GetFiles(directory))File.Delete(file);Directory.Delete(directory);}
    }

    // 合成图片与固定测试口令；没有 CAD 引用，也不读取用户资产。
    private static int Main(string[] args)
    {
        try
        {
            if(args.Length>0 && args[0]=="--trace")AppDomain.CurrentDomain.AssemblyResolve+=(sender,eventArgs)=>{
                Console.Error.WriteLine("REQUEST="+eventArgs.Name+" FROM="+eventArgs.RequestingAssembly?.FullName+" AT="+eventArgs.RequestingAssembly?.Location);return null;};
            FastBatchPlot.Core.Common.LegacyDependencyResolution.EnsureInitialized();
            GeneratedMediaChecks.Run();
            CheckPdf();
            if(args.Length>0 && args[0]=="--pdf")return 0;
            var watch=Stopwatch.StartNew();
            byte[] png;
            using(var bitmap=new Bitmap(12,6))
            using(var graphics=Graphics.FromImage(bitmap))
            using(var buffer=new MemoryStream())
            {
                graphics.Clear(Color.Transparent);graphics.FillRectangle(Brushes.Red,2,1,8,4);
                bitmap.Save(buffer,ImageFormat.Png);png=buffer.ToArray();
            }
            CheckRaster(png);
            var asset=StampAsset.Import("框架兼容性测试",png);
            const string code="test-only-authorization-2026";
            var now=DateTimeOffset.UtcNow;
            asset.Details=new StampDetails{Kind=StampKind.Registration,Sizing=StampSizing.PhysicalSize,WidthMm=62,HeightMm=32,ValidUntilUtcTicks=now.AddHours(2).UtcDateTime.Ticks};
            var encrypted=StampAuthorization.Protect(asset,code,now.AddDays(1),now);
            if(encrypted.Protection!.Version!=2)throw new Exception("新属性未纳入新版认证。");
            var permit=StampAuthorization.Unlock(encrypted,code,DateTimeOffset.UtcNow);
            var plain=permit.GetAsset(encrypted,DateTimeOffset.UtcNow);
            if(plain.PngBase64!=asset.PngBase64||encrypted.PngBase64!="")throw new Exception("保护后明文检查或解锁内容不匹配。");
            bool expired=false;
            try{permit.GetAsset(encrypted,now.AddHours(2));}catch(InvalidOperationException){expired=true;}
            if(!expired)throw new Exception("印章独立有效期未执行。");
            var changed=encrypted.Copy();changed.Protection!.ExpiresUtcTicks++;
            bool rejected=false;
            try{StampAuthorization.Unlock(changed,code,DateTimeOffset.UtcNow);}
            catch(InvalidOperationException){rejected=true;}
            if(!rejected)throw new Exception("期限篡改未被拒绝。");
            Console.WriteLine("NET48_STAMP_AUTHORIZATION_OK; elapsedMs="+watch.ElapsedMilliseconds);
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
