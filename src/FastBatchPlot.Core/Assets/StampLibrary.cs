using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FastBatchPlot.Core.Assets
{
    [DataContract]
    public sealed class StampAsset
    {
        static StampAsset(){FastBatchPlot.Core.Common.LegacyDependencyResolution.EnsureInitialized();}
        public const int MaximumPngBytes = 8 * 1024 * 1024;
        [DataMember(IsRequired=true)] public string Id { get; set; } = Guid.NewGuid().ToString("N");
        [DataMember(IsRequired=true)] public string Name { get; set; } = "";
        [DataMember(IsRequired=true)] public string PngBase64 { get; set; } = "";
        [DataMember(IsRequired=true)] public int PixelWidth { get; set; }
        [DataMember(IsRequired=true)] public int PixelHeight { get; set; }
        [DataMember(EmitDefaultValue=false)] public StampProtection? Protection {get;set;}
        [DataMember(EmitDefaultValue=false)] public StampDetails? Details {get;set;}
        public StampAsset Copy() => new StampAsset { Id=Id, Name=Name, PngBase64=PngBase64, PixelWidth=PixelWidth, PixelHeight=PixelHeight, Protection=Protection?.Copy(), Details=Details?.Copy() };
        public override string ToString() => Name;

        public static StampAsset Import(string name, byte[] png)
        {
            using(FastBatchPlot.Core.Common.LegacyDependencyResolution.BeginImageOperation())return ImportPng(name,png);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static StampAsset ImportPng(string name,byte[] png)
        {
            using (var image = Decode(png))
            using (var output = new MemoryStream())
            {
                image.SaveAsPng(output);
                if (output.Length > MaximumPngBytes) throw new InvalidDataException("印章 PNG 超过 8MB。");
                var asset = new StampAsset { Name=name, PngBase64=Convert.ToBase64String(output.ToArray()),
                    PixelWidth=image.Width, PixelHeight=image.Height };
                asset.ValidateMetadata();
                return asset;
            }
        }

        public void Validate()
        {
            ValidateMetadata();
            if(Protection!=null){Protection.Validate();return;}
            byte[] png;
            try { png = Convert.FromBase64String(PngBase64); }
            catch (FormatException ex) { throw new InvalidDataException("印章 PNG 编码损坏。", ex); }
            using(FastBatchPlot.Core.Common.LegacyDependencyResolution.BeginImageOperation())ValidatePngContent(png);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ValidatePngContent(byte[] png)
        {
            using (var image = Decode(png))
                if (image.Width != PixelWidth || image.Height != PixelHeight)
                    throw new InvalidDataException("印章尺寸与图片内容不一致。");
        }

        private void ValidateMetadata()
        {
            Details?.Validate();
            if(Protection!=null && ((Protection.Version==1 && Details!=null)||(Protection.Version==2 && Details==null)))
                throw new InvalidDataException("印章属性与授权版本不匹配，不能增删未认证字段。");
            if (!Guid.TryParseExact(Id, "N", out _) || string.IsNullOrWhiteSpace(Name) || Name.Length > 128 || Name.Any(char.IsControl))
                throw new InvalidDataException("印章 ID 或名称无效，名称须为1至128字符。");
            try{new UTF8Encoding(false,true).GetByteCount(Name);}
            catch(EncoderFallbackException){throw new InvalidDataException("印章名称包含无效的 Unicode 字符。");}
            if (PngBase64 == null || (Protection==null && PngBase64.Length == 0) || (Protection!=null && PngBase64.Length!=0) || PngBase64.Length > (MaximumPngBytes + 2) / 3 * 4 ||
                PixelWidth < 1 || PixelHeight < 1 || (long)PixelWidth * PixelHeight > 16000000)
                throw new InvalidDataException("印章内容无效或超过8MB/1600万像素限制。");
        }

        private static Image<Rgba32> Decode(byte[] png)
        {
            if (png == null || png.Length == 0 || png.Length > MaximumPngBytes)
                throw new InvalidDataException("请选择不超过8MB的透明 PNG 印章。");
            StampPng.Validate(png);
            using (var input = new MemoryStream(png, false))
            {
                var info = Image.Identify(input, out var format);
                if (format?.Name != "PNG" || info == null || info.Width < 1 || info.Height < 1 || (long)info.Width * info.Height > 16000000)
                    throw new InvalidDataException("印章必须是单帧透明 PNG，且不超过1600万像素。");
                input.Position = 0;
                var image = Image.Load<Rgba32>(input);
                try
                {
                    if (image.Frames.Count != 1) throw new InvalidDataException("印章不能使用动画 PNG。");
                    bool transparent=false, visible=false;
                    for (int y=0; y<image.Height && !(transparent && visible); y++)
                        for (int x=0; x<image.Width; x++)
                        {
                            byte alpha=image[x,y].A; transparent |= alpha<255; visible |= alpha>0;
                            if (transparent && visible) break;
                        }
                    if (!transparent || !visible) throw new InvalidDataException("印章须同时包含透明背景和可见内容；不能使用全不透明或全透明图片。");
                    return image;
                }
                catch { image.Dispose(); throw; }
            }
        }
    }

    [DataContract]
    public sealed class StampLibrary
    {
        [DataMember(IsRequired=true)] public int SchemaVersion {get;set;} = 1;
        [DataMember(IsRequired=true)] public List<StampAsset> Assets {get;set;} = new List<StampAsset>();
        // 文件内容摘要只用于检测共享文件并发更新，不是内容授权或防篡改凭证。
        public string Revision {get;set;} = "";
        public StampLibrary Copy() => new StampLibrary { SchemaVersion=SchemaVersion, Revision=Revision, Assets=Assets.Select(a=>a.Copy()).ToList() };
        public void Validate()
        {
            if ((SchemaVersion!=1 && SchemaVersion!=2 && SchemaVersion!=3) || Assets==null || Assets.Count>64) throw new InvalidDataException("印章库结构或版本无效，最多64枚印章。");
            if(SchemaVersion<3 && Assets.Any(a=>a?.Details!=null))throw new InvalidDataException("印章属性需要版本3库，不能降级保存。");
            if(SchemaVersion==1 && Assets.Any(a=>a?.Protection!=null))throw new InvalidDataException("受保护印章需要版本2库，不能降级保存。 ");
            if (Assets.Any(a=>a==null) || Assets.Sum(a=>(long)(a.PngBase64?.Length??0)+(a.Protection?.Ciphertext?.Length??0)) > 30L*1024*1024)
                throw new InvalidDataException("印章库内容无效或编码内容超过30MB。");
            if(Assets.Sum(a=>(long)a.PixelWidth*a.PixelHeight)>64000000)throw new InvalidDataException("印章库总像素超过6400万，请拆分为多个库。");
            var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var asset in Assets)
            {
                asset.Validate();
                if (!ids.Add(asset.Id) || !names.Add(asset.Name.Trim())) throw new InvalidDataException("印章 ID 或名称重复。");
            }
        }
    }
}
