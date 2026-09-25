using System;
using System.IO;
using FastBatchPlot.Core.Pdf;
using PdfSharpCore.Pdf;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class SinglePdfTests
    {
        [Fact]
        public void SaveCopyPreservesBytesAndRequiresExplicitOverwrite()
        {
            string source=SinglePdfFile.CreateTemporaryPath(), directory=Path.GetDirectoryName(source)!;
            try
            {
                using(var doc=new PdfDocument()){doc.AddPage();doc.Save(source);}
                string target=Path.Combine(directory,"saved.pdf");
                SinglePdfFile.SaveCopy(source,target);
                Assert.Equal(File.ReadAllBytes(source),File.ReadAllBytes(target));
                Assert.Throws<IOException>(()=>SinglePdfFile.SaveCopy(source,target));
                File.WriteAllText(target,"old");
                SinglePdfFile.SaveCopy(source,target,true);
                Assert.Equal(File.ReadAllBytes(source),File.ReadAllBytes(target));
                Assert.Empty(Directory.GetFiles(directory,"*.tmp"));
            }
            finally {Directory.Delete(directory,true);}
        }
        [Fact]
        public void InvalidOrMultipagePdfCannotReplaceExistingFile()
        {
            string source=SinglePdfFile.CreateTemporaryPath(),directory=Path.GetDirectoryName(source)!;
            try
            {
                string target=Path.Combine(directory,"saved.pdf");File.WriteAllText(target,"keep");
                using(var doc=new PdfDocument()){doc.AddPage();doc.AddPage();doc.Save(source);}
                Assert.Throws<InvalidDataException>(()=>SinglePdfFile.SaveCopy(source,target,true));
                Assert.Equal("keep",File.ReadAllText(target));
                File.WriteAllText(source,"not pdf");
                Assert.ThrowsAny<Exception>(()=>SinglePdfFile.SaveCopy(source,target,true));
                Assert.Equal("keep",File.ReadAllText(target));
            }
            finally {Directory.Delete(directory,true);}
        }
    }
}
