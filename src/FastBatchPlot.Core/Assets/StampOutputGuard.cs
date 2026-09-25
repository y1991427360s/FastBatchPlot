using System;
using System.Linq;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Assets
{
    public static class StampOutputGuard
    {
        /// <summary>每页在接触打印引擎前检查共享库及有效期；返回只用于本页临时插图的副本。</summary>
        public static StampAsset? Resolve(PlotConfig config)=>Resolve(config,()=>DateTimeOffset.UtcNow);
        public static StampAsset? Resolve(PlotConfig config,DateTimeOffset now)=>Resolve(config,()=>now);
        public static StampAsset? Resolve(PlotConfig config,Func<DateTimeOffset> utcNow)
        {
            if(!config.PrintStamps || config.Stamp==null)return null;
            var selected=config.Stamp;
            selected.Validate();
            if(string.IsNullOrWhiteSpace(config.StampLibraryPath)||config.StampAssetId!=selected.Id)
                throw new InvalidOperationException("印章缺少当前库和选择信息，未提交本页。");
            var current=StampLibraryStore.Load(config.StampLibraryPath).Assets.SingleOrDefault(a=>a.Id==selected.Id);
            if(current==null || StampAuthorization.Fingerprint(current)!=StampAuthorization.Fingerprint(selected))
                throw new InvalidOperationException("印章已从共享库移除或内容/授权发生变化，请重新选择；未提交本页。");
            StampValidity.EnsureUsable(current,utcNow());
            if(current.Protection==null)return current.Copy();
            return (config.StampPermit??throw new InvalidOperationException("印章尚未授权，未提交本页。"))
                .GetAsset(current,utcNow);
        }
    }
}
