using System;
using System.IO;
using FastBatchPlot.Core.Tasks;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class PlotResourceRevisionTests
    {
        [Fact]
        public void StyleSearchRejectsMissingAndAmbiguousNames()
        {
            string root=Path.Combine(Path.GetTempPath(),"styles-"+Guid.NewGuid().ToString("N"));
            string a=Path.Combine(root,"a"),b=Path.Combine(root,"b");Directory.CreateDirectory(a);Directory.CreateDirectory(b);
            try
            {
                Assert.Throws<InvalidOperationException>(()=>PlotResourceRevision.ResolveStyle("mono.ctb",a+";"+b));
                File.WriteAllText(Path.Combine(a,"mono.ctb"),"A");
                Assert.Equal(Path.Combine(a,"mono.ctb"),PlotResourceRevision.ResolveStyle("mono.ctb",a+";"+a+";"+b));
                File.WriteAllText(Path.Combine(b,"mono.ctb"),"B");
                Assert.Throws<InvalidOperationException>(()=>PlotResourceRevision.ResolveStyle("mono.ctb",a+";"+b));
                Assert.Throws<InvalidOperationException>(()=>PlotResourceRevision.ResolveStyle("../mono.ctb",a));
            }
            finally{Directory.Delete(root,true);}
        }
        [Fact]
        public void OrderDoesNotMatterButContentAndPathDo()
        {
            string dir=Path.Combine(Path.GetTempPath(),"plot-resources-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try
            {
                string style=Path.Combine(dir,"style.ctb"),device=Path.Combine(dir,"device.pc3"),pmp=Path.Combine(dir,"paper.pmp");
                File.WriteAllText(style,"AAAA");File.WriteAllText(device,"device");File.WriteAllText(pmp,"paper");
                var original=PlotResourceRevision.Compute(new[]{style,device,pmp});
                Assert.Equal(original,PlotResourceRevision.Compute(new[]{pmp,style,device,style}));
                var stamp=File.GetLastWriteTimeUtc(style);File.WriteAllText(style,"BBBB");File.SetLastWriteTimeUtc(style,stamp);
                Assert.NotEqual(original,PlotResourceRevision.Compute(new[]{style,device,pmp}));
                File.Delete(pmp);Assert.Throws<FileNotFoundException>(()=>PlotResourceRevision.Compute(new[]{style,device,pmp}));
            }
            finally{Directory.Delete(dir,true);}
        }
    }
}
