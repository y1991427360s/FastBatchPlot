using System;
using System.Collections.Generic;
using System.IO;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;

namespace FastBatchPlot.Core.Assets
{
    public enum StampSlot { Primary, Registration }
    public sealed class ResolvedStamp
    {
        public StampSlot Slot {get;}
        public StampAsset Asset {get;}
        public ResolvedStamp(StampSlot slot,StampAsset asset){Slot=slot;Asset=asset;}
        public TemplateRegion? Region(PlotFrame frame)=>Slot==StampSlot.Primary?frame.StampRegion:frame.RegistrationStampRegion;
        public StampPlacement? Placement(PlotFrame frame)=>Slot==StampSlot.Primary?frame.StampPlacement:frame.RegistrationStampPlacement;
    }
    public static class StampOutputSet
    {
        public static bool SameSelection(string firstPath,string firstId,string secondPath,string secondId)
            => !string.IsNullOrEmpty(firstId) && string.Equals(firstId,secondId,StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetFullPath(firstPath),Path.GetFullPath(secondPath),StringComparison.OrdinalIgnoreCase);
        public static PlotConfig RegistrationConfig(PlotConfig source)=>new PlotConfig
        {
            PrintStamps=source.PrintStamps && source.PrintRegistrationStamp,
            StampLibraryPath=source.RegistrationStampLibraryPath,StampAssetId=source.RegistrationStampAssetId,
            Stamp=source.RegistrationStamp,StampPermit=source.RegistrationStampPermit
        };
        public static List<ResolvedStamp> ResolveAll(PlotConfig config)=>ResolveAll(config,()=>DateTimeOffset.UtcNow);
        public static List<ResolvedStamp> ResolveAll(PlotConfig config,Func<DateTimeOffset> utcNow)
        {
            var result=new List<ResolvedStamp>();
            if(!config.PrintStamps)return result;
            if(config.PrintPrimaryStamp && config.PrintRegistrationStamp &&
                SameSelection(config.StampLibraryPath,config.StampAssetId,config.RegistrationStampLibraryPath,config.RegistrationStampAssetId))
                throw new InvalidOperationException("主章与注册章不能重复选择同一枚印章。");
            if(config.PrintPrimaryStamp)
            {
                if(!string.IsNullOrEmpty(config.StampAssetId) && config.Stamp==null)throw new InvalidOperationException("主章内容尚未预检。");
                var asset=StampOutputGuard.Resolve(config,utcNow);
                if(asset!=null)result.Add(new ResolvedStamp(StampSlot.Primary,asset));
            }
            if(config.PrintRegistrationStamp)
            {
                if(!string.IsNullOrEmpty(config.RegistrationStampAssetId) && config.RegistrationStamp==null)throw new InvalidOperationException("注册章内容尚未预检。");
                var asset=StampOutputGuard.Resolve(RegistrationConfig(config),utcNow);
                if(asset!=null)
                {
                    if(asset.Details?.Kind!=StampKind.Registration)throw new InvalidOperationException("注册章位置必须选择类别为注册章的资产。");
                    result.Add(new ResolvedStamp(StampSlot.Registration,asset));
                }
            }
            // 第二枚读库和校验可能耗时，返回前重新核验第一枚的期限。
            foreach(var stamp in result)
            {
                StampValidity.EnsureUsable(stamp.Asset,utcNow());
                var encrypted=stamp.Slot==StampSlot.Primary?config.Stamp:config.RegistrationStamp;
                var permit=stamp.Slot==StampSlot.Primary?config.StampPermit:config.RegistrationStampPermit;
                if(encrypted?.Protection!=null)permit!.GetAsset(encrypted,utcNow);
            }
            return result;
        }
    }
}
