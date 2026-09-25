using System;
using System.Collections.Generic;
using System.Linq;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Planning
{
    /// <summary>显式图层映射，不从名称片段推断签章内容，不使用通配符。</summary>
    public static class PlotLayerVisibility
    {
        public static void Validate(string signature, string stamp)
        {
            ValidateName(signature);
            ValidateName(stamp);
            if (string.Equals(signature, stamp, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("签名与印章不能映射到同一图层，否则无法分别控制输出。");
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name != name.Trim() ||
                name.Any(char.IsControl) || name.IndexOfAny(new[] { '*', '?', '\\', '/', ':', ';', '"', '=', '<', '>' }) >= 0)
                throw new ArgumentException("签章映射须填写完整图层名，不能包含通配符或首尾空格。");
            if (string.Equals(name, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Defpoints", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("不能把 0 或 Defpoints 通用图层作为签章专用层。");
        }

        public static HashSet<string> LayersToSuppress(PlotConfig config)
        {
            Validate(config.SignatureLayerName, config.StampLayerName);
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!config.PrintSignatures) result.Add(config.SignatureLayerName);
            if (!config.PrintStamps) result.Add(config.StampLayerName);
            return result;
        }
    }
}
