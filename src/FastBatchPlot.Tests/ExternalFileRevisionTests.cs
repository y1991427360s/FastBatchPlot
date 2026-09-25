using System;
using System.IO;
using FastBatchPlot.Core.Tasks;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class ExternalFileRevisionTests
    {
        [Fact]
        public void DetectsReplacementWithIdenticalLengthAndTimestamp()
        {
            string directory=Path.Combine(Path.GetTempPath(),"xref-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            string path=Path.Combine(directory,"xref.dwg");
            try
            {
                File.WriteAllText(path,"AAAA");var time=File.GetLastWriteTimeUtc(path);string original=ExternalFileRevision.Read(path);
                Assert.Equal(original,ExternalFileRevision.Read(path));
                File.WriteAllText(path,"BBBB");File.SetLastWriteTimeUtc(path,time);
                Assert.NotEqual(original,ExternalFileRevision.Read(path));
                File.Delete(path);Assert.Equal("missing",ExternalFileRevision.Read(path));
                File.WriteAllText(path,"AAAA");Assert.Equal(original,ExternalFileRevision.Read(path));
            }
            finally{Directory.Delete(directory,true);}
        }
        [Fact]
        public void RefusesWritingFileInsteadOfMarkingItMissing()
        {
            string path=Path.GetTempFileName();
            try
            {
                using(var writer=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                    Assert.Throws<IOException>(()=>ExternalFileRevision.Read(path));
            }
            finally{File.Delete(path);}
        }
        [Fact]
        public void ResolvesNestedReferenceAgainstItsOwner()
        {
            string owner=Path.Combine(Path.GetTempPath(),"project","nested","owner.dwg");
            Assert.Equal(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(owner)!,"..","xref.dwg")),ExternalFileRevision.ResolvePath(owner,Path.Combine("..","xref.dwg")));
            Assert.Throws<InvalidOperationException>(()=>ExternalFileRevision.ResolvePath("Drawing1.dwg","xref.dwg"));
            Assert.Equal("unresolved",ExternalFileRevision.Read(""));
        }
    }
}
