using System;
using System.IO;
using System.Linq;
using System.Text;
using FastBatchPlot.Core.Printing;
using FastBatchPlot.Core.Models;
using Xunit;

namespace FastBatchPlot.Tests
{
    public sealed class AcadPdfMediaFilesTests : IDisposable
    {
        private readonly string root=Path.Combine(Path.GetTempPath(),"AcadMedia-"+Guid.NewGuid().ToString("N"));
        private string Source=>Path.Combine(root,"source.pc3");
        private string Temporary=>Path.Combine(root,"private");
        private const string Fixture="meta{\ncanonical_family_name=\"Autodesk ePlot\ncanonical_model_name=\"pdf\ndriver_type=3\nfile_only=TRUE\nconfig_autospool=FALSE\ndriver_pathname=\"C:\\CAD\\drv\\pdfplot14.hdi\nshort_net_name=\"\nfriendly_net_name=\"\nconfig_description=\"中文配置{测试}=保留\n}\nio{\ntype=2\nplot_to_file=TRUE\npathname=\"\n}\nmedia{\nselection_method=2\nsize{\nname=\"old\n}\n}\ncustom{\n0{\nname=\"Hardcopy_Resolution\nvalue=600\n}\n}\n";
        public AcadPdfMediaFilesTests(){Directory.CreateDirectory(root);File.WriteAllBytes(Source,PiaConfiguration.Parse(Fixture).Encode());}

        [Fact]
        public void DependencyFilesIncludeLinkedPmp()
        {
            Assert.Single(AcadPdfMediaFiles.DependencyFiles(Source));
            using(var first=AcadPdfMediaFiles.Create(Source,420,297,Temporary))
            {
                var paths=AcadPdfMediaFiles.DependencyFiles(first.ConfigurationPath);
                Assert.Contains(first.ConfigurationPath,paths);Assert.Contains(first.PmpPath,paths);Assert.Equal(2,paths.Count);
            }
        }
        private void WritePdfFixture()
        {
            var source=PiaConfiguration.Parse(Fixture);
            var custom=source.Root.Child("custom");int index=1;
            foreach(var entry in new[]{"Raster_Limit=400","Monochrome_Raster_Limit=400","Custom_Raster_Resolution=FALSE","Custom_Monochrome_Resolution=FALSE","All_As_Geometry=FALSE","Include_Layer=TRUE","Resolution=14"})
            {
                var parts=entry.Split('=');var node=custom.Add((index++).ToString());node.SetText("name",parts[0]);node.SetRaw("value",parts[1]);
            }
            var color=source.Root.Add("res_color_mem");color.SetRaw("lines_overwrite","TRUE");
            var resolution=color.Add("resolution");
            foreach(string name in new[]{"phys_resolution_x","phys_resolution_y","effective_resolution_x","effective_resolution_y"})resolution.SetRaw(name,"600.0");
            File.WriteAllBytes(Source,source.Encode());
        }
        private static string Option(PiaConfiguration source,string name)=>source.Root.Child("custom").Children.Values.Single(node=>node.Text("name")==name).Raw("value");
        [Fact]
        public void ExplicitPdfOptionsAreWrittenWithoutChangingSourceOrResolutionEnum()
        {
            WritePdfFixture();byte[] original=File.ReadAllBytes(Source);var modified=File.GetLastWriteTimeUtc(Source);
            using(var files=AcadPdfMediaFiles.Create(Source,420,297,Temporary,new PdfOutputOptions{VectorResolutionDpi=2400,RasterResolutionDpi=600,TextToGeometry=true,IncludeLayers=false}))
            {
                var pdf=PiaConfiguration.Read(files.ConfigurationPath);
                Assert.Equal("2400",Option(pdf,"Hardcopy_Resolution"));Assert.Equal("14",Option(pdf,"Resolution"));
                Assert.Equal("600",Option(pdf,"Raster_Limit"));Assert.Equal("600",Option(pdf,"Monochrome_Raster_Limit"));
                Assert.Equal("TRUE",Option(pdf,"Custom_Raster_Resolution"));Assert.Equal("TRUE",Option(pdf,"Custom_Monochrome_Resolution"));
                Assert.Equal("TRUE",Option(pdf,"All_As_Geometry"));Assert.Equal("FALSE",Option(pdf,"Include_Layer"));
                Assert.All(pdf.Root.Child("res_color_mem").Child("resolution").Values,pair=>Assert.Equal("2400.0",pair.Value));
            }
            Assert.Equal(original,File.ReadAllBytes(Source));Assert.Equal(modified,File.GetLastWriteTimeUtc(Source));
        }
        [Fact]
        public void FollowingDriverPreservesAllCustomPdfOptions()
        {
            WritePdfFixture();var original=PiaConfiguration.Read(Source);
            using(var files=AcadPdfMediaFiles.Create(Source,420,297,Temporary,new PdfOutputOptions()))
            {
                var pdf=PiaConfiguration.Read(files.ConfigurationPath);
                foreach(var node in original.Root.Child("custom").Children.Values)Assert.Equal(node.Raw("value"),Option(pdf,node.Text("name")));
                Assert.Equal(original.Root.Child("res_color_mem").Raw("lines_overwrite"),pdf.Root.Child("res_color_mem").Raw("lines_overwrite"));
            }
        }
        [Fact]
        public void MissingMergeAndCustomFieldsAreRejectedBeforeCreation()
        {
            Assert.Throws<InvalidDataException>(()=>AcadPdfMediaFiles.Create(Source,420,297,Temporary,new PdfOutputOptions{MergeLines=false}));
            Assert.Throws<InvalidDataException>(()=>AcadPdfMediaFiles.Create(Source,420,297,Temporary,new PdfOutputOptions{IncludeLayers=false}));
            Assert.False(Directory.Exists(Temporary));
        }

        private string WriteLinkedPmp(bool absolute=false)
        {
            WritePdfFixture();
            var source=PiaConfiguration.Read(Source);var pmp=new PiaConfiguration();pmp.Root.SetChild("meta",source.Root.Child("meta"));
            var udm=pmp.Root.Add("udm");var calibration=udm.Add("calibration");calibration.SetRaw("_x","0.95");calibration.SetRaw("_y","1.05");
            var media=udm.Add("media");media.SetRaw("size_max_x","5000.0");
            var size=media.Add("size").Add("2");size.SetText("name","OldPaper");size.SetText("localized_name","原有图幅");size.SetText("media_description_name","OldDescription");size.SetRaw("media_group","15");
            var description=media.Add("description").Add("4");description.SetText("name","OldDescription");description.SetRaw("media_bounds_urx","420.0");
            var mod=pmp.Root.Add("mod");var color=mod.Add("res_color_mem");color.SetRaw("lines_overwrite","TRUE");color.Add("resolution").SetRaw("phys_resolution_x","600.0");
            var option=mod.Add("custom").Add("7");option.SetText("name","Include_Layer");option.SetRaw("value","TRUE");
            pmp.Root.Add("del").Add("media").SetRaw("unchanged_caps","5");
            pmp.Root.Add("extra").SetText("note","保留未知字段");
            string folder=Path.Combine(root,"PMP Files");Directory.CreateDirectory(folder);string path=Path.Combine(folder,"original.pmp");
            File.WriteAllBytes(path,pmp.Encode());
            source.Root.Child("meta").SetText("user_defined_model_pathname",absolute?path:"original.pmp");
            source.Root.Child("meta").SetText("user_defined_model_basename","original.pmp");File.WriteAllBytes(Source,source.Encode());
            return path;
        }

        [Theory]
        [InlineData(false)][InlineData(true)]
        public void LinkedPmpPreservesCalibrationAndAppendsBeyondBothIndexSets(bool absolute)
        {
            string path=WriteLinkedPmp(absolute);byte[] before=File.ReadAllBytes(path),pc3=File.ReadAllBytes(Source);var stamp=File.GetLastWriteTimeUtc(path);
            using(var files=AcadPdfMediaFiles.Create(Source,634.25,301.125,Temporary))
            {
                var result=PiaConfiguration.Read(files.PmpPath);var udm=result.Root.Child("udm");var media=udm.Child("media");
                Assert.Equal("0.95",udm.Child("calibration").Raw("_x"));Assert.Equal("1.05",udm.Child("calibration").Raw("_y"));
                Assert.Equal("原有图幅",media.Child("size").Child("2").Text("localized_name"));
                Assert.Equal("OldDescription",media.Child("description").Child("4").Text("name"));
                Assert.Equal(files.MediaName,media.Child("size").Child("5").Text("name"));
                Assert.Equal("634.250000",media.Child("description").Child("5").Raw("media_bounds_urx"));
                Assert.Equal("TRUE",result.Root.Child("mod").Child("res_color_mem").Raw("lines_overwrite"));
                Assert.Equal("5",result.Root.Child("del").Child("media").Raw("unchanged_caps"));
                Assert.Equal("保留未知字段",result.Root.Child("extra").Text("note"));
                Assert.Equal(files.PmpPath,result.Root.Child("meta").Text("user_defined_model_pathname"));
            }
            Assert.Equal(before,File.ReadAllBytes(path));Assert.Equal(pc3,File.ReadAllBytes(Source));Assert.Equal(stamp,File.GetLastWriteTimeUtc(path));
            Assert.Empty(Directory.EnumerateFileSystemEntries(Temporary));
        }

        [Fact]
        public void ExplicitOptionsAlsoUpdateInheritedOverrides()
        {
            string path=WriteLinkedPmp();byte[] before=File.ReadAllBytes(path);
            using(var files=AcadPdfMediaFiles.Create(Source,420,297,Temporary,new PdfOutputOptions{VectorResolutionDpi=1200,MergeLines=true,IncludeLayers=false}))
            {
                var mod=PiaConfiguration.Read(files.PmpPath).Root.Child("mod");
                Assert.Equal("FALSE",mod.Child("res_color_mem").Raw("lines_overwrite"));
                Assert.Equal("1200.0",mod.Child("res_color_mem").Child("resolution").Raw("phys_resolution_x"));
                Assert.Equal("FALSE",mod.Child("custom").Child("7").Raw("value"));
            }
            Assert.Equal(before,File.ReadAllBytes(path));
        }

        [Theory]
        [InlineData("driver")][InlineData("calibration")][InlineData("description")][InlineData("index")][InlineData("basename")][InlineData("ambiguous")][InlineData("deleted")]
        public void IncompatibleLinkedPmpFailsBeforeCreatingPrivateFiles(string fault)
        {
            string path=WriteLinkedPmp();var pmp=PiaConfiguration.Read(path);
            if(fault=="driver")pmp.Root.Child("meta").SetText("canonical_model_name","png");
            if(fault=="calibration")pmp.Root.Child("udm").Child("calibration").SetRaw("_x","NaN");
            if(fault=="description")pmp.Root.Child("udm").Child("media").Child("size").Child("2").SetText("media_description_name","Missing");
            if(fault=="index")pmp.Root.Child("udm").Child("media").Child("size").Add("02");
            if(fault=="deleted")pmp.Root.Child("del").Add("res_color_mem").SetRaw("lines_overwrite","TRUE");
            File.WriteAllBytes(path,pmp.Encode());
            if(fault=="ambiguous")File.Copy(path,Path.Combine(root,"original.pmp"));
            if(fault=="basename")
            {var source=PiaConfiguration.Read(Source);source.Root.Child("meta").SetText("user_defined_model_basename","other.pmp");File.WriteAllBytes(Source,source.Encode());}
            Assert.Throws<InvalidDataException>(()=>AcadPdfMediaFiles.Create(Source,420,297,Temporary,new PdfOutputOptions{MergeLines=true}));
            Assert.False(Directory.Exists(Temporary));
        }

        [Theory]
        [InlineData(true,"FALSE")][InlineData(false,"TRUE")]
        public void MergeFlagUsesInverseOverwriteAndPreservesOtherSettings(bool merge,string expected)
        {
            WritePdfFixture();byte[] original=File.ReadAllBytes(Source);
            using(var files=AcadPdfMediaFiles.Create(Source,420,297,Temporary,new PdfOutputOptions{MergeLines=merge}))
            {
                var generated=PiaConfiguration.Read(files.ConfigurationPath);
                Assert.Equal(expected,generated.Root.Child("res_color_mem").Raw("lines_overwrite"));
                Assert.Equal("600",Option(generated,"Hardcopy_Resolution"));
                Assert.Equal("TRUE",Option(generated,"Include_Layer"));
                Assert.Equal("FALSE",Option(generated,"All_As_Geometry"));
            }
            Assert.Equal(original,File.ReadAllBytes(Source));
        }

        [Fact]
        public void UnknownOverwriteValueCannotBeSilentlyReinterpreted()
        {
            WritePdfFixture();var source=PiaConfiguration.Read(Source);
            source.Root.Child("res_color_mem").SetRaw("lines_overwrite","2");File.WriteAllBytes(Source,source.Encode());
            Assert.Throws<InvalidDataException>(()=>AcadPdfMediaFiles.Create(Source,420,297,Temporary,new PdfOutputOptions{MergeLines=true}));
            Assert.False(Directory.Exists(Temporary));
        }

        [Fact]
        public void EncodedContainerRoundTripsChineseStringsAndNumericSuffixes()
        {
            var doc=PiaConfiguration.Parse(Fixture+"numeric{\nvalue=5.7937498093 (    P,PL%T 8_0)\n}\n");
            var bytes=doc.Encode();var restored=PiaConfiguration.Decode(bytes);
            Assert.Equal("中文配置{测试}=保留",restored.Root.Child("meta").Text("config_description"));
            Assert.Equal("5.7937498093 (    P,PL%T 8_0)",restored.Root.Child("numeric").Raw("value"));
            Assert.Equal((uint)(bytes.Length-60),BitConverter.ToUInt32(bytes,56));
        }
        [Fact]
        public void PlainContainerReadsWithoutInventingClosingQuotes()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var parsed=PiaConfiguration.Decode(Encoding.GetEncoding(936).GetBytes("PIAFILEVERSION_2.0,PC3VER1\r\n"+Fixture));
            Assert.Equal("中文配置{测试}=保留",parsed.Root.Child("meta").Text("config_description"));
        }
        [Theory]
        [InlineData(0)] [InlineData(48)] [InlineData(52)] [InlineData(56)] [InlineData(63)]
        public void CorruptHeadersLengthsOrPayloadAreRejected(int index)
        {
            var bytes=File.ReadAllBytes(Source);bytes[index]^=0x7f;
            Assert.ThrowsAny<Exception>(()=>PiaConfiguration.Decode(bytes));
        }
        [Theory]
        [InlineData("meta{\na=1\na=2\n}\n")]
        [InlineData("meta{\na=1\n")]
        [InlineData("}\n")]
        [InlineData("meta{\na=1\na{\n}\n}\n")]
        [InlineData("meta{\nx=1\n}\nmeta{\n}\n")]
        [InlineData("meta{\nx=1\0\n}\n")]
        public void AmbiguousSyntaxIsRejected(string text)=>Assert.Throws<InvalidDataException>(()=>PiaConfiguration.Parse(text));

        [Fact]
        public void GeneratedPairHasExactPaperLinkAndDoesNotAlterSource()
        {
            var before=File.ReadAllBytes(Source);
            using(var first=AcadPdfMediaFiles.Create(Source,634.25,301.125,Temporary))
            using(var second=AcadPdfMediaFiles.Create(Source,297,420,Temporary))
            {
                Assert.NotEqual(Path.GetFileName(first.ConfigurationPath),Path.GetFileName(second.ConfigurationPath));
                var pc3=PiaConfiguration.Read(first.ConfigurationPath);var pmp=PiaConfiguration.Read(first.PmpPath);
                Assert.Equal(first.PmpPath,pc3.Root.Child("meta").Text("user_defined_model_pathname"));
                Assert.Equal(Path.GetFileName(first.PmpPath),pc3.Root.Child("meta").Text("user_defined_model_basename"));
                Assert.Equal(first.MediaName,pc3.Root.Child("media").Child("size").Text("name"));
                var media=pmp.Root.Child("udm").Child("media");var size=media.Child("size").Child("0");var desc=media.Child("description").Child("0");
                Assert.Equal(first.MediaName,size.Text("name"));Assert.Equal(size.Text("media_description_name"),desc.Text("name"));
                Assert.Equal("634.250000",desc.Raw("media_bounds_urx"));Assert.Equal("301.125000",desc.Raw("printable_bounds_ury"));
                Assert.Equal("TRUE",desc.Raw("dimensional"));Assert.Equal("15",size.Raw("media_group"));
                Assert.Single(media.Child("size").Children);Assert.Single(media.Child("description").Children);
                Assert.Equal("600",pc3.Root.Child("custom").Child("0").Raw("value"));
                Assert.Equal("中文配置{测试}=保留",pc3.Root.Child("meta").Text("config_description"));
                foreach(var file in new[]{first.ConfigurationPath,first.PmpPath})
                    Assert.Throws<IOException>(()=>{using(var write=File.Open(file,FileMode.Open,FileAccess.Write)){};});
            }
            Assert.Equal(before,File.ReadAllBytes(Source));Assert.Empty(Directory.GetFileSystemEntries(Temporary));
        }
        [Theory]
        [InlineData(0)] [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
        public void InvalidDimensionsNeverCreatePrivateFiles(double value)
        {Assert.Throws<ArgumentException>(()=>AcadPdfMediaFiles.Create(Source,value,297,Temporary));Assert.False(Directory.Exists(Temporary));}
        [Theory]
        [InlineData("pdfplot14.hdi","raster7.hdi")]
        [InlineData("file_only=TRUE","file_only=FALSE")]
        [InlineData("config_autospool=FALSE","config_autospool=TRUE")]
        [InlineData("canonical_model_name=\"pdf","canonical_model_name=\"png")]
        [InlineData("plot_to_file=TRUE","plot_to_file=FALSE")]
        [InlineData("config_autospool=FALSE","config_autospool=FALSE\nuser_defined_model_pathname=\"original.pmp")]
        [InlineData("pathname=\"\n","pathname=\"C:\\output.pdf\n")]
        public void UnsupportedDriversCannotUseAutomaticPaper(string old,string replacement)
        {
            File.WriteAllBytes(Source,PiaConfiguration.Parse(Fixture.Replace(old,replacement)).Encode());
            Assert.Throws<InvalidDataException>(()=>AcadPdfMediaFiles.Create(Source,420,297,Temporary));Assert.False(Directory.Exists(Temporary));
        }
        [Fact]
        public void CleanupOnlyDeletesOwnedFilesAndReportsLockedResiduals()
        {
            var files=AcadPdfMediaFiles.Create(Source,420,297,Temporary);string dir=Path.GetDirectoryName(files.ConfigurationPath)!;
            string extra=Path.Combine(dir,"keep.txt");File.WriteAllText(extra,"keep");
            using(var reader=File.OpenRead(files.ConfigurationPath))
            {var error=Assert.Throws<IOException>(()=>files.Dispose());Assert.Contains(dir,error.Message);}
            Assert.True(File.Exists(files.ConfigurationPath));Assert.Equal("keep",File.ReadAllText(extra));
        }
        public void Dispose(){Directory.Delete(root,true);}
    }
}
