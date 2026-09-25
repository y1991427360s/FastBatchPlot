using System;
using System.IO;
using System.Runtime.Serialization;

namespace FastBatchPlot.Core.Assets
{
    public enum StampKind { Issue=0, Registration=1, Other=2 }
    public enum StampSizing { FitRegion=0, PhysicalSize=1 }
    [DataContract]
    public sealed class StampDetails
    {
        [DataMember(IsRequired=true)] public StampKind Kind {get;set;}
        [DataMember(IsRequired=true)] public StampSizing Sizing {get;set;}
        [DataMember(IsRequired=true)] public double WidthMm {get;set;}
        [DataMember(IsRequired=true)] public double HeightMm {get;set;}
        [DataMember(IsRequired=true)] public long ValidUntilUtcTicks {get;set;}
        public StampDetails Copy()=>(StampDetails)MemberwiseClone();
        public void Validate()
        {
            if(!Enum.IsDefined(typeof(StampKind),Kind)||!Enum.IsDefined(typeof(StampSizing),Sizing))
                throw new InvalidDataException("印章类别或尺寸模式无效。");
            if(Sizing==StampSizing.FitRegion)
            {
                if(WidthMm!=0||HeightMm!=0)throw new InvalidDataException("适应区域模式不能同时指定固定纸面尺寸。");
            }
            else if(double.IsNaN(WidthMm)||double.IsNaN(HeightMm)||WidthMm<0.1||HeightMm<0.1||WidthMm>1000||HeightMm>1000)
                throw new InvalidDataException("印章纸面宽高必须为0.1至1000毫米。");
            if(ValidUntilUtcTicks<0||ValidUntilUtcTicks>DateTime.MaxValue.Ticks)
                throw new InvalidDataException("印章有效期无效。");
        }
    }
    public static class StampValidity
    {
        public static void EnsureUsable(StampAsset asset,DateTimeOffset now)
        {
            asset.Details?.Validate();
            long end=asset.Details?.ValidUntilUtcTicks??0;
            if(end!=0 && now.UtcDateTime.Ticks>=end)
                throw new InvalidOperationException("印章本身已到期，未提交出图。请先更新印章属性；授权期限不会延长印章有效期。");
        }
    }
}
