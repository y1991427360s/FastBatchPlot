using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FastBatchPlot.Core.Printing;
using FastBatchPlot.Core.Models;
using Xunit;

namespace FastBatchPlot.Tests
{
    public sealed class ZwPdfMediaFilesTests : IDisposable
    {
        private readonly string dir=Path.Combine(Path.GetTempPath(),"ZwPdfMediaTests-"+Guid.NewGuid().ToString("N"));
        private string Source=>Path.Combine(dir,"source.pc5");
        private string Temporary=>Path.Combine(dir,"private");
        private const string Configuration="[Meta]\r\nPrinterType=2\r\nSource=\r\nsource_entry=\r\npmp_filepath=\r\npaper_name=Old\r\n"+
            "[res_color_mem]\r\nresolution_x=500\r\nresolution_y=500\r\ntruetype_as_text=1\r\n"+
            "[Standard]\r\nDeviceName=PDF\r\nDriverName=DWG to PDF\r\nDriverPath=ZwPDFDriver.dll\r\nDriverCfgPath=PDF.ini\r\nPortName=FILE:\r\n"+
            "[Port]\r\nplot_to_file=1\r\ndmDriverExtra=0\r\nprivatedata=\r\n[Retain]\r\npre_init=0\r\npost_init=0\r\ntermination=\r\n[Caps]\r\ndefinepapersize_caps=1\r\n";
        public ZwPdfMediaFilesTests(){Directory.CreateDirectory(dir);File.WriteAllText(Source,Configuration,new UTF8Encoding(false));}
        private static Encoding Gbk(){Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);return Encoding.GetEncoding(936,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);}

        [Fact]
        public void DependencyFilesIncludeLinkedPmp()
        {
            Assert.Single(ZwPdfMediaFiles.DependencyFiles(Source));
            using(var first=ZwPdfMediaFiles.Create(Source,420,297,Temporary))
            {
                var paths=ZwPdfMediaFiles.DependencyFiles(first.ConfigurationPath);
                Assert.Contains(first.ConfigurationPath,paths);Assert.Contains(first.PmpPath,paths);Assert.Equal(2,paths.Count);
            }
        }
        [Theory]
        [InlineData(true,"0")]
        [InlineData(false,"1")]
        public void ExplicitPdfOptionsOnlyChangeRequestedFields(bool geometry,string textValue)
        {
            byte[] before=File.ReadAllBytes(Source);var stamp=File.GetLastWriteTimeUtc(Source);
            using(var files=ZwPdfMediaFiles.Create(Source,420,297,Temporary,new PdfOutputOptions{VectorResolutionDpi=1200,TextToGeometry=geometry}))
            {
                string pc5=Gbk().GetString(File.ReadAllBytes(files.ConfigurationPath));
                Assert.Contains("resolution_x=1200\r\n",pc5);Assert.Contains("resolution_y=1200\r\n",pc5);
                Assert.Contains("truetype_as_text="+textValue+"\r\n",pc5);
            }
            Assert.Equal(before,File.ReadAllBytes(Source));Assert.Equal(stamp,File.GetLastWriteTimeUtc(Source));
        }
        [Theory]
        [InlineData(true,"0")][InlineData(false,"1")]
        public void MergeUsesInverseOverwriteFlag(bool merge,string overwrite)
        {
            byte[] original=File.ReadAllBytes(Source);
            using(var files=ZwPdfMediaFiles.Create(Source,420,297,Temporary,new PdfOutputOptions{MergeLines=merge}))
            {
                string pc5=Gbk().GetString(File.ReadAllBytes(files.ConfigurationPath));
                Assert.Contains("\r\nlines_overwrite="+overwrite+"\r\n",pc5);
                Assert.Contains("\r\nresolution_x=500\r\nresolution_y=500\r\n",pc5);
            }
            Assert.Equal(original,File.ReadAllBytes(Source));
        }

        [Theory]
        [InlineData(true,"1")][InlineData(false,"0")]
        public void RasterAndLayersUseIndependentNativeFields(bool layers,string value)
        {
            byte[] original=File.ReadAllBytes(Source);
            using(var files=ZwPdfMediaFiles.Create(Source,420,297,Temporary,new PdfOutputOptions{RasterResolutionDpi=300,IncludeLayers=layers}))
            {
                string pc5=Gbk().GetString(File.ReadAllBytes(files.ConfigurationPath));
                Assert.Contains("\r\nresolution_x=500\r\nresolution_y=500\r\n",pc5);
                Assert.Contains("\r\nraster_resolution_x=300\r\nraster_resolution_y=300\r\n",pc5);
                Assert.Contains("\r\nlayerinclude="+value+"\r\n",pc5);
                Assert.Contains("truetype_as_text=1\r\n",pc5);
            }
            Assert.Equal(original,File.ReadAllBytes(Source));
            Assert.Empty(Directory.EnumerateFileSystemEntries(Temporary));
        }

        [Theory]
        [InlineData(300,600,null)]
        [InlineData(null,600,null)]
        [InlineData(300,null,"raster_resolution_x=150\r\nraster_resolution_y=600\r\n")]
        public void RasterAboveEitherVectorAxisIsRejected(int? vector,int? raster,string? extra)
        {
            File.WriteAllText(Source,Configuration.Replace("truetype_as_text=1",(extra??"")+"truetype_as_text=1"),new UTF8Encoding(false));
            Assert.Throws<ArgumentException>(()=>ZwPdfMediaFiles.Create(Source,420,297,Temporary,new PdfOutputOptions{VectorResolutionDpi=vector,RasterResolutionDpi=raster}));
            Assert.False(Directory.Exists(Temporary));
        }

        [Theory]
        [InlineData("resolution_y=500","resolution_y=bad")]
        [InlineData("resolution_y=500","resolution_y=0")]
        [InlineData("resolution_y=500","")]
        public void RasterOverrideChecksInheritedVectorResolution(string before,string after)
        {
            File.WriteAllText(Source,Configuration.Replace(before,after),new UTF8Encoding(false));
            Assert.Throws<InvalidDataException>(()=>ZwPdfMediaFiles.Create(Source,420,297,Temporary,new PdfOutputOptions{RasterResolutionDpi=300}));
            Assert.False(Directory.Exists(Temporary));
        }

        [Fact]
        public void FollowDriverPreservesRasterLayerAndUnknownFields()
        {
            string content=Configuration.Replace("truetype_as_text=1","truetype_as_text=1\r\nraster_resolution_x=400\r\nraster_resolution_y=300\r\ncustom_quality=9")
                .Replace("pre_init=0","layerinclude=0\r\npre_init=0");
            File.WriteAllText(Source,content,new UTF8Encoding(false));
            using(var files=ZwPdfMediaFiles.Create(Source,420,297,Temporary,PdfOutputOptions.FollowDriver))
            {
                string generated=Gbk().GetString(File.ReadAllBytes(files.ConfigurationPath));
                Assert.Equal(content.Substring(content.IndexOf("[res_color_mem]",StringComparison.Ordinal)),
                    generated.Substring(generated.IndexOf("[res_color_mem]",StringComparison.Ordinal)));
            }
        }

        [Fact]
        public void ZeroRasterDefaultRemainsDriverControlledWhenVectorChanges()
        {
            File.WriteAllText(Source,Configuration.Replace("truetype_as_text=1","truetype_as_text=1\r\nraster_resolution_x=0\r\nraster_resolution_y=0"),new UTF8Encoding(false));
            using(var files=ZwPdfMediaFiles.Create(Source,420,297,Temporary,new PdfOutputOptions{VectorResolutionDpi=1200}))
            {
                string generated=Gbk().GetString(File.ReadAllBytes(files.ConfigurationPath));
                Assert.Contains("raster_resolution_x=0\r\nraster_resolution_y=0\r\n",generated);
                Assert.Contains("resolution_x=1200\r\nresolution_y=1200\r\n",generated);
            }
        }
        [Theory]
        [InlineData(0)][InlineData(-1)][InlineData(9601)]
        public void InvalidPdfResolutionNeverCreatesFiles(int dpi)
        {
            Assert.Throws<ArgumentException>(()=>ZwPdfMediaFiles.Create(Source,420,297,Temporary,new PdfOutputOptions{VectorResolutionDpi=dpi}));
            Assert.False(Directory.Exists(Temporary));
        }

        [Theory]
        [InlineData("DWG to PDF", 500)]
        [InlineData("ZWCAD PDF(General Documentation)", 1000)]
        [InlineData("ZWCAD PDF(High Quality Print)", 2000)]
        [InlineData("ZWCAD PDF(Smallest File)", 300)]
        [InlineData("ZWCAD PDF(Web and Mobile)", 300)]
        public void OfficialPresetNamesKeepTheirDriverOptions(string driver, int dpi)
        {
            string content = Configuration.Replace("DriverName=DWG to PDF", "DriverName=" + driver)
                .Replace("resolution_x=500", "resolution_x=" + dpi).Replace("resolution_y=500", "resolution_y=" + dpi)
                .Replace("truetype_as_text=1", "truetype_as_text=0\r\nlines_overwrite=0");
            File.WriteAllText(Source, content, new UTF8Encoding(false));
            byte[] before = File.ReadAllBytes(Source);
            using (var files = ZwPdfMediaFiles.Create(Source, 634.25, 301.125, Temporary))
            {
                string generated = Gbk().GetString(File.ReadAllBytes(files.ConfigurationPath));
                Assert.Contains("DriverName=" + driver + "\r\n", generated);
                Assert.Contains("resolution_x=" + dpi + "\r\n", generated);
                Assert.Contains("resolution_y=" + dpi + "\r\n", generated);
                Assert.Contains("truetype_as_text=0\r\nlines_overwrite=0\r\n", generated);
            }
            Assert.Equal(before, File.ReadAllBytes(Source));
        }

        [Theory]
        [InlineData("calibration.pmp")]
        [InlineData("missing.pmp")]
        [InlineData("C:\\Plotters\\calibration.pmp")]
        public void ExistingPmpCannotBeSilentlyReplaced(string reference)
        {
            string pmp = Path.Combine(dir, "calibration.pmp");
            File.WriteAllText(pmp, "[Meta]\r\ncalibration_x=0.95\r\ncalibration_y=1.05\r\n");
            byte[] calibration = File.ReadAllBytes(pmp);
            File.WriteAllText(Source, Configuration.Replace("pmp_filepath=", "pmp_filepath=" + reference), new UTF8Encoding(false));
            byte[] before = File.ReadAllBytes(Source);
            Assert.Contains("PMP", Assert.Throws<InvalidDataException>(() => ZwPdfMediaFiles.Create(Source, 420, 297, Temporary)).Message);
            Assert.Equal(before, File.ReadAllBytes(Source)); Assert.Equal(calibration, File.ReadAllBytes(pmp));
            Assert.False(Directory.Exists(Temporary));
        }

        private string LinkedPmp(string location="PMP Files")
        {
            string folder=Path.Combine(dir,location);Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,"existing.pmp");
            string content="[Meta]\r\npmpfilepath=\r\ndriver_path=\r\ncalibration_x=0.950000\r\ncalibration_y=1.050000\r\n"+
                "mod_num=1\r\ndel_num=1\r\nhide_num=1\r\nuserdef_num=1\r\n[user]\r\npaper_name0=Existing\r\npaper_local_name0=原有图幅\r\n"+
                "size_x0=210.000000\r\nsize_y0=297.000000\r\nllx0=2.000000\r\nlly0=3.000000\r\nurx0=208.000000\r\nury0=294.000000\r\n"+
                "actual_x0=210.000000\r\nactual_y0=297.000000\r\narea0=60528.000000\r\nUnit0=1\r\n"+
                "[modified]\r\noriginal_setting=keep\r\n[deleted]\r\noriginal_setting=keep\r\n[hidden]\r\noriginal_setting=keep\r\n";
            File.WriteAllBytes(path,Gbk().GetBytes(content));
            File.WriteAllBytes(Source,Gbk().GetBytes(Configuration.Replace("pmp_filepath=","pmp_filepath=existing.pmp")));
            return path;
        }

        [Theory]
        [InlineData("PMP Files",false)][InlineData("",false)][InlineData("elsewhere",true)]
        public void LinkedPmpIsCopiedWithCalibrationAndExistingSettings(string location,bool absolute)
        {
            string sourcePmp=LinkedPmp(location);
            if(absolute)File.WriteAllBytes(Source,Gbk().GetBytes(Configuration.Replace("pmp_filepath=","pmp_filepath="+sourcePmp)));
            byte[] pc5Before=File.ReadAllBytes(Source),pmpBefore=File.ReadAllBytes(sourcePmp);
            var stamp=File.GetLastWriteTimeUtc(sourcePmp);
            using(var files=ZwPdfMediaFiles.Create(Source,634.25,301.125,Temporary,new PdfOutputOptions{IncludeLayers=false}))
            {
                string generated=Gbk().GetString(File.ReadAllBytes(files.PmpPath));
                foreach(string line in Gbk().GetString(pmpBefore).Split(new[]{"\r\n"},StringSplitOptions.RemoveEmptyEntries))
                    Assert.Contains(line=="userdef_num=1"?"userdef_num=2":line,generated);
                Assert.Contains("paper_name1="+files.MediaName+"\r\n",generated);
                Assert.Contains("size_x1=634.250000\r\n",generated);Assert.Contains("size_y1=301.125000\r\n",generated);
                Assert.Contains("pmp_filepath="+files.PmpPath+"\r\n",Gbk().GetString(File.ReadAllBytes(files.ConfigurationPath)));
                Assert.Throws<IOException>(()=>{using(var stream=File.OpenWrite(files.PmpPath)){};});
            }
            Assert.Equal(pc5Before,File.ReadAllBytes(Source));Assert.Equal(pmpBefore,File.ReadAllBytes(sourcePmp));
            Assert.Equal(stamp,File.GetLastWriteTimeUtc(sourcePmp));Assert.Empty(Directory.EnumerateFileSystemEntries(Temporary));
        }

        [Theory]
        [InlineData("userdef_num=1","userdef_num=2")]
        [InlineData("userdef_num=1","userdef_num=-1")]
        [InlineData("calibration_x=0.950000","calibration_x=NaN")]
        [InlineData("pmpfilepath=","pmpfilepath=other.pmp")]
        [InlineData("driver_path=","driver_path=other.dll")]
        [InlineData("paper_name0=Existing","paper_name0=Existing\r\nsize_x1=900")]
        public void MalformedLinkedPmpNeverCreatesOutput(string before,string after)
        {
            string path=LinkedPmp();File.WriteAllBytes(path,Gbk().GetBytes(Gbk().GetString(File.ReadAllBytes(path)).Replace(before,after)));
            byte[] original=File.ReadAllBytes(path);
            Assert.Throws<InvalidDataException>(()=>ZwPdfMediaFiles.Create(Source,420,297,Temporary));
            Assert.Equal(original,File.ReadAllBytes(path));Assert.False(Directory.Exists(Temporary));
        }

        [Fact]
        public void AmbiguousLinkedPmpDoesNotGuessSearchOrder()
        {
            string path=LinkedPmp();File.Copy(path,Path.Combine(dir,"existing.pmp"));
            Assert.Contains("多份",Assert.Throws<InvalidDataException>(()=>ZwPdfMediaFiles.Create(Source,420,297,Temporary)).Message);
            Assert.False(Directory.Exists(Temporary));
        }

        [Fact]
        public void EmptyLinkedPmpCanGainItsFirstCustomPaper()
        {
            string path=LinkedPmp();
            File.WriteAllText(path,"[Meta]\r\npmpfilepath=\r\ndriver_path=\r\ncalibration_x=1\r\ncalibration_y=1\r\nmod_num=0\r\ndel_num=0\r\nhide_num=0\r\nuserdef_num=0\r\n",new UTF8Encoding(false));
            using(var files=ZwPdfMediaFiles.Create(Source,420,297,Temporary))
            {
                string generated=Gbk().GetString(File.ReadAllBytes(files.PmpPath));
                Assert.Contains("userdef_num=1\r\n",generated);Assert.Contains("[user]\r\npaper_name0="+files.MediaName,generated);
            }
        }

        [Theory]
        [InlineData("..\\existing.pmp")][InlineData("\\\\server\\plotters\\existing.pmp")][InlineData("C:existing.pmp")]
        public void LinkedPmpDoesNotResolveAmbiguousOrNetworkPaths(string reference)
        {
            File.WriteAllText(Source,Configuration.Replace("pmp_filepath=","pmp_filepath="+reference),new UTF8Encoding(false));
            Assert.Throws<InvalidDataException>(()=>ZwPdfMediaFiles.Create(Source,420,297,Temporary));
            Assert.False(Directory.Exists(Temporary));
        }

        [Fact]
        public void CreatesUniqueSinglePaperPairWithoutChangingSource()
        {
            byte[] original=File.ReadAllBytes(Source);
            using(var first=ZwPdfMediaFiles.Create(Source,630.125,297.5,Temporary))
            using(var second=ZwPdfMediaFiles.Create(Source,630.125,297.5,Temporary))
            {
                Assert.NotEqual(first.ConfigurationPath,second.ConfigurationPath);Assert.NotEqual(first.MediaName,second.MediaName);
                Assert.NotEqual(Path.GetFileName(first.ConfigurationPath),Path.GetFileName(second.ConfigurationPath));
                Assert.NotEqual(Path.GetFileName(first.PmpPath),Path.GetFileName(second.PmpPath));
                Assert.Equal(original,File.ReadAllBytes(Source));
                Assert.All(first.MediaName,ch=>Assert.InRange((int)ch,0,127));
                string pc5=Gbk().GetString(File.ReadAllBytes(first.ConfigurationPath)),pmp=Gbk().GetString(File.ReadAllBytes(first.PmpPath));
                Assert.Contains("pmp_filepath="+first.PmpPath+"\r\n",pc5);
                Assert.Contains("paper_name="+first.MediaName+"\r\n",pc5);
                Assert.Contains("actual_printable_bounds_llx=0.000000\r\n",pc5);
                Assert.Contains("actual_printable_bounds_urx=630.125000\r\n",pc5);
                Assert.Contains("actual_printable_bounds_ury=297.500000\r\n",pc5);
                Assert.Contains("actual_area=187462.187500\r\n",pc5);
                Assert.Contains("resolution_x=500\r\n",pc5);Assert.Contains("truetype_as_text=1\r\n",pc5);
                Assert.Contains("userdef_num=1\r\n",pmp);Assert.Contains("paper_name0="+first.MediaName+"\r\n",pmp);
                Assert.Contains("size_x0=630.125000\r\n",pmp);Assert.Contains("size_y0=297.500000\r\n",pmp);
                Assert.Contains("Unit0=1\r\n",pmp);Assert.DoesNotContain("paper_name1=",pmp);
                Assert.Equal(12,pmp.Split(new[]{"[user]\r\n"},StringSplitOptions.None)[1].Split(new[]{"\r\n"},StringSplitOptions.RemoveEmptyEntries).Length);
                Assert.DoesNotContain("\n",pc5.Replace("\r\n",""));Assert.DoesNotContain("\n",pmp.Replace("\r\n",""));
            }
            Assert.Empty(Directory.EnumerateFileSystemEntries(Temporary));
        }

        [Fact]
        public void ConfigAndPmpStayReadLockedUntilDispose()
        {
            var files=ZwPdfMediaFiles.Create(Source,420,297,Temporary);
            foreach(string path in new[]{files.ConfigurationPath,files.PmpPath})
            {
                using(var read=File.OpenRead(path))Assert.True(read.Length>0);
                Assert.Throws<IOException>(()=>{using(var write=new FileStream(path,FileMode.Open,FileAccess.Write,FileShare.ReadWrite)){};});
            }
            string folder=Path.GetDirectoryName(files.ConfigurationPath)!;
            files.Dispose();files.Dispose();Assert.False(Directory.Exists(folder));Assert.True(File.Exists(Source));
        }

        [Fact]
        public void DisposeDoesNotRecursivelyDeleteUnexpectedFiles()
        {
            var files=ZwPdfMediaFiles.Create(Source,420,297,Temporary);
            string extra=Path.Combine(Path.GetDirectoryName(files.ConfigurationPath)!,"keep.txt");File.WriteAllText(extra,"other data");
            files.Dispose();Assert.Equal("other data",File.ReadAllText(extra));
            Assert.False(File.Exists(files.ConfigurationPath));Assert.False(File.Exists(files.PmpPath));
        }

        [Fact]
        public void CleanupFailureReportsTheOwnedDirectoryWithoutDeletingOtherData()
        {
            var files=ZwPdfMediaFiles.Create(Source,420,297,Temporary);
            string folder=Path.GetDirectoryName(files.ConfigurationPath)!;
            using(var externalReader=new FileStream(files.ConfigurationPath,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                var error=Assert.Throws<IOException>(()=>files.Dispose());
                Assert.Contains(folder,error.Message);
                Assert.True(File.Exists(files.ConfigurationPath));
                Assert.Equal(Configuration,File.ReadAllText(Source));
            }
            // 模拟外部读取解除后由调用者按提示核对残留，不依靠递归清理。
            File.Delete(files.ConfigurationPath);Directory.Delete(folder);
        }

        [Fact]
        public void StrictGbkRoundTripsChineseCommentsAndRejectsInvalidBytes()
        {
            File.WriteAllBytes(Source,Gbk().GetBytes(Configuration.Replace("[Caps]","[Extra]\r\n说明=保留原有选项\r\n[Caps]")));
            using(var files=ZwPdfMediaFiles.Create(Source,420,297,Temporary))
                Assert.Contains("说明=保留原有选项\r\n",Gbk().GetString(File.ReadAllBytes(files.ConfigurationPath)));
            File.WriteAllBytes(Source,Encoding.ASCII.GetBytes(Configuration).Concat(new byte[]{0x81}).ToArray());
            Assert.Throws<InvalidDataException>(()=>ZwPdfMediaFiles.Create(Source,420,297,Temporary));
        }

        [Fact]
        public void InvariantDecimalsIgnoreCurrentCulture()
        {
            var previous=CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("fr-FR");
                using(var files=ZwPdfMediaFiles.Create(Source,420.25,297.75,Temporary))
                    Assert.Contains("size_x0=420.250000",File.ReadAllText(files.PmpPath));
            }
            finally{CultureInfo.CurrentCulture=previous;}
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(0.000000001)]
        public void InvalidOrUnrepresentableSizesDoNotCreateDirectories(double size)
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>ZwPdfMediaFiles.Create(Source,size,297,Temporary));
            Assert.False(Directory.Exists(Temporary));
        }

        [Theory]
        [InlineData("PrinterType=2","PrinterType=1")]
        [InlineData("DeviceName=PDF","DeviceName=Printer")]
        [InlineData("DriverName=DWG to PDF","DriverName=Other PDF")]
        [InlineData("DriverName=DWG to PDF","DriverName=ZWCAD PDF(Unknown)")]
        [InlineData("DriverName=DWG to PDF","DriverName=ZWCAD PDF(High Quality Print) extra")]
        [InlineData("DriverPath=ZwPDFDriver.dll","DriverPath=C:\\Drivers\\ZwPDFDriver.dll")]
        [InlineData("DriverCfgPath=PDF.ini","DriverCfgPath=..\\PDF.ini")]
        [InlineData("PortName=FILE:","PortName=LPT1:")]
        [InlineData("plot_to_file=1","plot_to_file=0")]
        [InlineData("definepapersize_caps=1","definepapersize_caps=0")]
        [InlineData("definepapersize_caps=1","")]
        [InlineData("source_entry=","source_entry=other.pc5")]
        [InlineData("pre_init=0","pre_init=script")]
        [InlineData("privatedata=","privatedata=1234")]
        [InlineData("dmDriverExtra=0","dmDriverExtra=17")]
        [InlineData("DeviceName=PDF","DeviceName=PDF\r\nNetPrinterUSE=1")]
        [InlineData("DeviceName=PDF","DeviceName=PDF\r\nUnknownDriverPath=other.dll")]
        public void UnrecognizedOrExternallyBoundDriversFailClosed(string before,string after)
        {
            File.WriteAllText(Source,Configuration.Replace(before,after),new UTF8Encoding(false));
            Assert.Throws<InvalidDataException>(()=>ZwPdfMediaFiles.Create(Source,420,297,Temporary));
            Assert.False(Directory.Exists(Temporary));
        }

        [Theory]
        [InlineData("\r\n[meta]\r\nPrinterType=2\r\n")]
        [InlineData("\r\n[More]\r\nKey=1\r\nKEY=2\r\n")]
        [InlineData("\r\nnot-an-ini-line\r\n")]
        [InlineData("\r\n[bad\tsection]\r\nvalue=1\r\n")]
        public void DuplicateOrMalformedIniCannotBeReinterpreted(string suffix)
        {
            File.WriteAllText(Source,Configuration+suffix,new UTF8Encoding(false));
            Assert.Throws<InvalidDataException>(()=>ZwPdfMediaFiles.Create(Source,420,297,Temporary));
            Assert.False(Directory.Exists(Temporary));
        }

        [Fact]
        public void NewlineInjectionAndUnencodableTemporaryPathsAreRejected()
        {
            Assert.Throws<ArgumentException>(()=>ZwPdfMediaFiles.Create(Source,420,297,Temporary+"\r\nDriverPath=bad"));
            Assert.Throws<EncoderFallbackException>(()=>ZwPdfMediaFiles.Create(Source,420,297,Temporary+"😀"));
            Assert.False(Directory.Exists(Temporary));
        }

        [Fact]
        public void UnicodeBomIsRejectedBeforeCreatingTemporaryFiles()
        {
            foreach(var encoding in new Encoding[]{new UTF8Encoding(true),Encoding.Unicode,Encoding.BigEndianUnicode})
            {
                File.WriteAllBytes(Source,encoding.GetPreamble().Concat(encoding.GetBytes(Configuration)).ToArray());
                Assert.Throws<InvalidDataException>(()=>ZwPdfMediaFiles.Create(Source,420,297,Temporary));
                Assert.False(Directory.Exists(Temporary));
            }
        }

        [Fact]
        public void OversizedSourcesAndBlockedTemporaryRootsLeaveExistingDataAlone()
        {
            File.WriteAllBytes(Source,new byte[256*1024+1]);
            Assert.Throws<InvalidDataException>(()=>ZwPdfMediaFiles.Create(Source,420,297,Temporary));
            File.WriteAllText(Source,Configuration,new UTF8Encoding(false));File.WriteAllText(Temporary,"do not replace");
            Assert.ThrowsAny<IOException>(()=>ZwPdfMediaFiles.Create(Source,420,297,Temporary));
            Assert.Equal("do not replace",File.ReadAllText(Temporary));Assert.True(File.Exists(Source));
        }
        [Fact]
        public void RoamingPresetWithOmittedDriverNameIsAccepted()
        {
            string roamingConfig = Configuration.Replace("DriverName=DWG to PDF", "DriverName=");
            string roamingPath = Path.Combine(dir, "DWG to PDF.pc5");
            File.WriteAllText(roamingPath, roamingConfig, new UTF8Encoding(false));
            using (var files = ZwPdfMediaFiles.Create(roamingPath, 420, 297, Temporary))
            {
                Assert.True(File.Exists(files.ConfigurationPath));
                Assert.True(File.Exists(files.PmpPath));
            }
        }

        public void Dispose(){Directory.Delete(dir,true);}
    }
}
