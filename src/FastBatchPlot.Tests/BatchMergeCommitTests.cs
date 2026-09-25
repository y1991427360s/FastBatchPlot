using System;
using System.IO;
using FastBatchPlot.Core.Pdf;
using PdfSharpCore.Pdf;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class BatchMergeCommitTests
    {
        [Fact] public void BatchMergeNeverReplacesExistingTargetAndCleansTemporary()
        {
            string directory=Path.Combine(Path.GetTempPath(),"batch-merge-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            string input=Path.Combine(directory,"one.pdf"),output=Path.Combine(directory,"merged.pdf");
            try
            {
                using(var pdf=new PdfDocument()){pdf.AddPage();pdf.Save(input);}
                File.WriteAllText(output,"existing-output");
                Assert.False(PdfMerger.MergePdfFiles(new[]{new PdfMergeItem(input,"one")},output,out var error,overwrite:false));
                Assert.Equal("existing-output",File.ReadAllText(output));Assert.NotEmpty(error);Assert.Empty(Directory.GetFiles(directory,".batchplot-*"));
                File.Delete(output);Assert.True(PdfMerger.MergePdfFiles(new[]{new PdfMergeItem(input,"one")},output,out error,overwrite:false));Assert.True(new FileInfo(output).Length>0);
            }
            finally{foreach(var f in Directory.GetFiles(directory))File.Delete(f);Directory.Delete(directory);}
        }
    }
}
