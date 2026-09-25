using System;
using System.IO;
using System.Linq;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Tasks;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using Xunit;

namespace FastBatchPlot.Tests
{
    public sealed class PdfTaskHistoryTests : IDisposable
    {
        private readonly string dir=Path.Combine(Path.GetTempPath(),"PdfHistory-"+Guid.NewGuid().ToString("N"));
        public PdfTaskHistoryTests(){Directory.CreateDirectory(dir);}
        private string History=>Path.Combine(dir,"task.json");
        private PlotFrame Frame(int i)=>new PlotFrame {Id=i,OrderIndex=i,MinX=0,MinY=0,MaxX=420,MaxY=297,CalculatedScale=1,
            IsLandscape=true,DetectedPaper=new PaperSize("A3",420,297),SourceDocumentId="session-document",SourceLayoutId="1A",
            HandleOrId="7B",TitleInfo=new TitleBlockInfo{DrawingNo="图号"+i,DrawingName="标题"+i}};
        private PdfTaskRecord Create(int count=2,bool overwrite=false)
        {
            var config=new PlotConfig{OutputDirectory=dir,MergeToSinglePdf=true,OverwriteExisting=overwrite};
            var run=new BatchPlotRun(Enumerable.Range(1,count).Select(i=>new BatchPage(Frame(i),Path.Combine(dir,"page"+i+".pdf"))),config);
            return PdfTaskHistory.Create(run,Path.Combine(dir,"merged.pdf"));
        }
        private void Pdf(string path,double width=420)
        {
            using(var document=new PdfDocument())
            {var page=document.AddPage();page.Width=XUnit.FromMillimeter(width);page.Height=XUnit.FromMillimeter(297);document.Save(path);}
        }

        [Fact]
        public void ResourceRevisionSurvivesHistoryAndOldHistoryRemainsReadable()
        {
            var record=Create();PdfTaskHistory.Save(History,record);Assert.Null(PdfTaskHistory.Load(History).PlotResourceRevision);
            record.PlotResourceRevision="resource-v1";PdfTaskHistory.Save(History,record);
            Assert.Equal("resource-v1",PdfTaskHistory.Load(History).PlotResourceRevision);
        }
        [Fact]
        public void CustomPortraitPaperAndFractionalScaleSurviveRetrySnapshot()
        {
            var frame=Frame(1);
            var candidate=FastBatchPlot.Core.Planning.CustomPaperEdit.Preview(frame,200.125,400.25,true);
            frame.DetectedPaper=candidate.DetectedPaper;frame.IsLandscape=candidate.IsLandscape;frame.CalculatedScale=candidate.CalculatedScale;
            var config=new PlotConfig{OutputDirectory=dir,MergeToSinglePdf=false,MarginMm=2};
            var run=new BatchPlotRun(new[]{new BatchPage(frame,Path.Combine(dir,"custom.pdf"))},config);
            PdfTaskHistory.Save(History,PdfTaskHistory.Create(run,""));
            var loaded=PdfTaskHistory.Load(History);
            var recovered=PdfTaskHistory.PendingPages(loaded).Single().Frame;
            var plan=FastBatchPlot.Core.Planning.PlotPlanBuilder.Create(recovered,loaded.Config);
            Assert.False(recovered.IsLandscape);Assert.Equal("",recovered.DetectedPaper.StandardName);
            Assert.Equal(candidate.CalculatedScale,recovered.CalculatedScale);
            Assert.Equal(204.125,plan.PaperWidthMm);Assert.Equal(404.25,plan.PaperHeightMm);
        }

        [Fact]
        public void RoundTripPreservesIdentityRevisionsAndIsolatedSnapshots()
        {
            var record=Create();record.SourceRevisions["session-document"]="revision-1";
            record.Config.Margins.Left=99;record.Pages[0].Frame.TitleInfo.DrawingName="修改副本";
            PdfTaskHistory.Save(History,record);
            Assert.Equal(1,record.Revision);
            var loaded=PdfTaskHistory.Load(History);
            Assert.Equal(record.Id,loaded.Id);Assert.Equal(1,loaded.Revision);
            Assert.Equal("revision-1",loaded.SourceRevisions["session-document"]);
            Assert.Equal("session-document",loaded.Pages[0].Frame.SourceDocumentId);
            Assert.Equal("1A",loaded.Pages[0].Frame.SourceLayoutId);
            Assert.Equal("标题1",loaded.Pages[0].Frame.TitleInfo.DrawingName);
            Assert.Equal(0,loaded.Config.Margins.Left);
            Assert.DoesNotContain("Stamp",File.ReadAllText(History));
            Assert.DoesNotContain("Permit",File.ReadAllText(History));
        }

        [Fact]
        public void StaleWriterCannotOverwriteNewRevision()
        {
            var first=Create();PdfTaskHistory.Save(History,first);
            var stale=PdfTaskHistory.Load(History);
            PdfTaskHistory.RecordPage(first,0,BatchPageState.Failed,"设备不可用");PdfTaskHistory.Save(History,first);
            byte[] before=File.ReadAllBytes(History);
            Assert.Throws<IOException>(()=>PdfTaskHistory.Save(History,stale));
            Assert.Equal(before,File.ReadAllBytes(History));Assert.Equal(1,stale.Revision);
            Assert.Equal(2,PdfTaskHistory.Load(History).Revision);
        }

        [Fact]
        public void MissingOrMalformedHistoryIsNotAnEmptyNewTask()
        {
            Assert.Throws<FileNotFoundException>(()=>PdfTaskHistory.Load(History));
            File.WriteAllText(History,"{broken}");Assert.Throws<InvalidDataException>(()=>PdfTaskHistory.Load(History));
            Assert.ThrowsAny<Exception>(()=>PdfTaskHistory.Save(History,Create()));
            Assert.Equal("{broken}",File.ReadAllText(History));
        }

        [Theory]
        [InlineData("version")]
        [InlineData("extra")]
        [InlineData("relative")]
        public void CorruptSchemaTrailingJsonOrRelativeOutputIsRejected(string mode)
        {
            var record=Create();PdfTaskHistory.Save(History,record);
            string json=File.ReadAllText(History);
            if(mode=="version")json=json.Replace("\"SchemaVersion\":1","\"SchemaVersion\":99");
            else if(mode=="extra")json+=" {}";
            else json=json.Replace("page1.pdf","../page1.pdf");
            File.WriteAllText(History,json);
            Assert.ThrowsAny<Exception>(()=>PdfTaskHistory.Load(History));
        }

        [Theory]
        [InlineData(" []")]
        [InlineData(" garbage")]
        [InlineData(" null")]
        [InlineData("\u00a0")]
        public void TrailingContentIsRejected(string suffix)
        {
            PdfTaskHistory.Save(History,Create());
            File.AppendAllText(History,suffix);
            Assert.Throws<InvalidDataException>(()=>PdfTaskHistory.Load(History));
        }

        [Fact]
        public void StringDelimitersAndJsonWhitespaceDoNotConfuseRootValidation()
        {
            var record=Create();
            PdfTaskHistory.RecordPage(record,0,BatchPageState.Failed,"} [ \" 引号 \\ 结束");
            PdfTaskHistory.Save(History,record);
            File.AppendAllText(History," \t\r\n");
            Assert.Equal(record.Pages[0].Error,PdfTaskHistory.Load(History).Pages[0].Error);
        }

        [Theory]
        [InlineData("\"Unsupported\":1,")]
        [InlineData("\"SchemaVersion\":1,")]
        public void UnknownAndDuplicateJsonMembersAreRejected(string injected)
        {
            PdfTaskHistory.Save(History,Create());
            string json=File.ReadAllText(History);
            File.WriteAllText(History,"{"+injected+json.Substring(1));
            Assert.Throws<InvalidDataException>(()=>PdfTaskHistory.Load(History));
        }

        [Fact]
        public void HistoryCannotReplaceAPdfOutputEvenBeforeFirstSave()
        {
            var record=Create();Pdf(record.Pages[0].OutputPath);
            byte[] original=File.ReadAllBytes(record.Pages[0].OutputPath);
            Assert.Throws<IOException>(()=>PdfTaskHistory.Save(record.Pages[0].OutputPath,record));
            Assert.Equal(original,File.ReadAllBytes(record.Pages[0].OutputPath));
        }

        [Fact]
        public void SuccessfulPageIsVerifiedAndOmittedFromRetry()
        {
            var record=Create();Pdf(record.Pages[0].OutputPath);
            PdfTaskHistory.RecordPage(record,0,BatchPageState.Succeeded,"");
            PdfTaskHistory.RecordPage(record,1,BatchPageState.Failed,"失败");
            PdfTaskHistory.Save(History,record);
            var loaded=PdfTaskHistory.Load(History);var pending=PdfTaskHistory.PendingPages(loaded);
            Assert.Single(pending);Assert.Equal(record.Pages[1].OutputPath,pending[0].OutputPath);
            Assert.Equal(64,loaded.Pages[0].Sha256.Length);Assert.True(loaded.Pages[0].Bytes>0);
            pending[0].Frame.TitleInfo.DrawingName="其他";Assert.Equal("标题2",loaded.Pages[1].Frame.TitleInfo.DrawingName);
            Assert.Throws<InvalidOperationException>(()=>PdfTaskHistory.ValidateCompleted(loaded));
        }

        [Fact]
        public void ChangedCompletedPdfBlocksAllRetry()
        {
            var record=Create();Pdf(record.Pages[0].OutputPath);PdfTaskHistory.RecordPage(record,0,BatchPageState.Succeeded,"");
            File.AppendAllText(record.Pages[0].OutputPath,"\n% external change\n");
            Assert.Throws<IOException>(()=>PdfTaskHistory.PendingPages(record));
        }

        [Fact]
        public void MissingCompletedPdfBlocksAllRetry()
        {
            var record=Create();Pdf(record.Pages[0].OutputPath);PdfTaskHistory.RecordPage(record,0,BatchPageState.Succeeded,"");
            File.Delete(record.Pages[0].OutputPath);Assert.Throws<FileNotFoundException>(()=>PdfTaskHistory.PendingPages(record));
        }

        [Fact]
        public void CrashLeftoverMustNotBeRecognizedAsSuccessfulOrOverwritten()
        {
            var record=Create();PdfTaskHistory.RecordPage(record,0,BatchPageState.Running,"");Pdf(record.Pages[0].OutputPath);
            byte[] before=File.ReadAllBytes(record.Pages[0].OutputPath);
            Assert.Throws<IOException>(()=>PdfTaskHistory.PendingPages(record));
            Assert.Equal(BatchPageState.Running,record.Pages[0].State);Assert.Equal("",record.Pages[0].Sha256);
            Assert.Equal(before,File.ReadAllBytes(record.Pages[0].OutputPath));
        }

        [Fact]
        public void OverwriteModeRetriesLeftoverWithoutTreatingItAsSuccess()
        {
            var record=Create(overwrite:true);PdfTaskHistory.RecordPage(record,0,BatchPageState.Failed,"x");Pdf(record.Pages[0].OutputPath);
            var pending=PdfTaskHistory.PendingPages(record);
            Assert.Equal(2,pending.Count);
            Assert.Equal(BatchPageState.Failed,record.Pages[0].State);Assert.Equal("",record.Pages[0].Sha256);
        }

        [Fact]
        public void OverwriteSettingSurvivesHistoryRoundTrip()
        {
            var strict=Create();PdfTaskHistory.Save(History,strict);
            Assert.False(PdfTaskHistory.Load(History).Config.OverwriteExisting);
            File.Delete(History);
            var overwrite=Create(overwrite:true);PdfTaskHistory.Save(History,overwrite);
            Assert.True(PdfTaskHistory.Load(History).Config.OverwriteExisting);
        }

        [Fact]
        public void WrongPaperCannotBeRecordedAsSuccess()
        {
            var record=Create();Pdf(record.Pages[0].OutputPath,210);
            Assert.Throws<IOException>(()=>PdfTaskHistory.RecordPage(record,0,BatchPageState.Succeeded,""));
            Assert.Equal(BatchPageState.Pending,record.Pages[0].State);Assert.Equal("",record.Pages[0].Sha256);Assert.Equal(0,record.Pages[0].Bytes);
        }

        [Fact]
        public void CompletedValidationChecksEveryPdfAndFailureClearsMetadata()
        {
            var record=Create();
            for(int i=0;i<2;i++){Pdf(record.Pages[i].OutputPath);PdfTaskHistory.RecordPage(record,i,BatchPageState.Succeeded,"");}
            PdfTaskHistory.ValidateCompleted(record);
            PdfTaskHistory.RecordPage(record,1,BatchPageState.Failed,"重试被拒绝");
            Assert.Equal("",record.Pages[1].Sha256);Assert.Equal(0,record.Pages[1].Bytes);
            Assert.Throws<IOException>(()=>PdfTaskHistory.PendingPages(record));
        }

        [Fact]
        public void DuplicateOutputsAndUnknownSourceRevisionAreRejectedBeforeSave()
        {
            var record=Create();record.Pages[1].OutputPath=record.Pages[0].OutputPath;
            Assert.Throws<InvalidDataException>(()=>PdfTaskHistory.Save(History,record));Assert.False(File.Exists(History));
            record=Create();record.SourceRevisions["another-document"]="1";
            Assert.Throws<InvalidDataException>(()=>PdfTaskHistory.Save(History,record));Assert.False(File.Exists(History));
        }

        [Theory]
        [InlineData("page:stream.pdf")]
        [InlineData("CON.pdf")]
        [InlineData("nul.extra.pdf")]
        [InlineData("COM1.pdf")]
        [InlineData("LPT².pdf")]
        [InlineData("CON .pdf")]
        [InlineData("page.pdf ")]
        [InlineData("page.pdf.")]
        [InlineData(" page.pdf")]
        public void DeviceNamesAlternateDataStreamsAndAmbiguousNamesAreRejected(string name)
        {
            var record=Create();record.Pages[0].OutputPath=Path.Combine(dir,name);
            Assert.Throws<InvalidDataException>(()=>PdfTaskHistory.Save(History,record));Assert.False(File.Exists(History));
            record=Create();record.MergedOutputPath=Path.Combine(dir,name);
            Assert.Throws<InvalidDataException>(()=>PdfTaskHistory.Save(History,record));Assert.False(File.Exists(History));
        }

        [Fact]
        public void PagesAndMergedFileMustStayInConfiguredDirectory()
        {
            var record=Create();record.Pages[0].OutputPath=Path.Combine(dir,"another","page.pdf");
            Assert.Throws<InvalidDataException>(()=>PdfTaskHistory.Save(History,record));
            record=Create();record.MergedOutputPath=Path.Combine(dir,"another","merged.pdf");
            Assert.Throws<InvalidDataException>(()=>PdfTaskHistory.Save(History,record));Assert.False(File.Exists(History));
        }

        [Theory]
        [InlineData("relative")]
        [InlineData("reserved")]
        [InlineData("trailing")]
        [InlineData("mismatch")]
        public void ConfiguredOutputDirectoryMustBeCanonicalAndMatchEveryPage(string mode)
        {
            string output=mode=="relative"?"relative-output":mode=="reserved"?Path.Combine(dir,"CON"):
                mode=="trailing"?Path.Combine(dir,"folder "):Path.Combine(dir,"different");
            var run=new BatchPlotRun(new[]{new BatchPage(Frame(1),Path.Combine(dir,"page.pdf"))},
                new PlotConfig{OutputDirectory=output,MergeToSinglePdf=false});
            Assert.Throws<InvalidDataException>(()=>PdfTaskHistory.Create(run,""));
        }

        [Fact]
        public void DirectoryWithOrdinaryTrailingSeparatorIsAccepted()
        {
            var run=new BatchPlotRun(new[]{new BatchPage(Frame(1),Path.Combine(dir,"page.pdf"))},
                new PlotConfig{OutputDirectory=dir+Path.DirectorySeparatorChar,MergeToSinglePdf=false});
            var record=PdfTaskHistory.Create(run,"");PdfTaskHistory.Save(History,record);
            Assert.Equal(dir+Path.DirectorySeparatorChar,PdfTaskHistory.Load(History).Config.OutputDirectory);
        }

        [Fact]
        public void NonPdfOrPrinterRunCannotBePersisted()
        {
            foreach(var config in new[]{new PlotConfig{SendToPrinter=true},new PlotConfig{ExportFormat=PlotExportFormat.PNG}})
            {
                var run=new BatchPlotRun(new[]{new BatchPage(Frame(1),Path.Combine(dir,"page.pdf"))},config);
                Assert.Throws<InvalidDataException>(()=>PdfTaskHistory.Create(run,Path.Combine(dir,"merged.pdf")));
            }
        }

        [Fact]
        public void DeletedSavedRecordIsNotSilentlyRecreated()
        {
            var record=Create();PdfTaskHistory.Save(History,record);File.Delete(History);
            Assert.Throws<IOException>(()=>PdfTaskHistory.Save(History,record));Assert.False(File.Exists(History));
        }
        [Fact]
        public void CreateFromFinishedRunStillInspectsEverySucceededFile()
        {
            var run=new BatchPlotRun(Enumerable.Range(1,3).Select(i=>new BatchPage(Frame(i),Path.Combine(dir,"page"+i+".pdf"))),
                new PlotConfig{OutputDirectory=dir,MergeToSinglePdf=false});
            while(!run.IsFinished)run.Step((page,config)=>{
                Pdf(page.OutputPath,page.Frame.OrderIndex==3?210:420);return new BatchPageResult(true);
            });
            Assert.Throws<IOException>(()=>PdfTaskHistory.Create(run,""));
            Pdf(Path.Combine(dir,"page3.pdf"));
            var record=PdfTaskHistory.Create(run,"");
            Assert.All(record.Pages,p=>{Assert.Equal(BatchPageState.Succeeded,p.State);Assert.Equal(64,p.Sha256.Length);Assert.True(p.Bytes>0);});
        }

        [Fact]
        public void FailedAtomicReplaceDoesNotAdvanceCallerOrOverwriteHistory()
        {
            var record=Create();PdfTaskHistory.Save(History,record);
            var before=File.ReadAllBytes(History);long revision=record.Revision,updated=record.UpdatedUtcTicks;
            using(var held=new FileStream(History,FileMode.Open,FileAccess.Read,FileShare.Read))
                Assert.Throws<IOException>(()=>PdfTaskHistory.Save(History,record));
            Assert.Equal(revision,record.Revision);Assert.Equal(updated,record.UpdatedUtcTicks);
            Assert.Equal(before,File.ReadAllBytes(History));Assert.Empty(Directory.GetFiles(dir,"*.tmp"));
            PdfTaskHistory.Save(History,record);Assert.Equal(revision+1,record.Revision);
        }

        [Fact]
        public void CachedContractsStillRejectNestedUnknownAndDuplicateMembers()
        {
            var record=Create(100);PdfTaskHistory.Save(History,record);
            string original=File.ReadAllText(History);
            for(int i=0;i<2;i++)Assert.Equal(100,PdfTaskHistory.Load(History).Pages.Count);
            foreach(string injection in new[]{"\"NotAFrameField\":1,", "\"Order\":1,"})
            {
                string invalid=original.Replace("\"Frame\":{","\"Frame\":{"+injection);
                Assert.NotEqual(original,invalid);File.WriteAllText(History,invalid);
                Assert.Throws<InvalidDataException>(()=>PdfTaskHistory.Load(History));
                Assert.Throws<InvalidDataException>(()=>PdfTaskHistory.Save(History,record));
                Assert.Equal(invalid,File.ReadAllText(History));
            }
        }

        [Fact]
        public void ThousandPageHistoryRoundTripPreservesLastPageAndRevision()
        {
            var record=Create(1000);PdfTaskHistory.Save(History,record);
            PdfTaskHistory.RecordPage(record,999,BatchPageState.Failed,"最后一页失败");PdfTaskHistory.Save(History,record);
            var loaded=PdfTaskHistory.Load(History);
            Assert.Equal(1000,loaded.Pages.Count);Assert.Equal(2,loaded.Revision);
            Assert.Equal("标题1000",loaded.Pages[999].Frame.TitleInfo.DrawingName);
            Assert.Equal("最后一页失败",loaded.Pages[999].Error);
            Assert.All(loaded.Pages.Take(999),p=>Assert.Equal(BatchPageState.Pending,p.State));
        }
        public void Dispose(){Directory.Delete(dir,true);}
    }
}
