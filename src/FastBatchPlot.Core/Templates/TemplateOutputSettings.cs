using System;
using System.Collections.Generic;
using System.Linq;
using FastBatchPlot.Core.Export;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Templates
{
    public static class TemplateOutputSettings
    {
        public static TitleBlockTemplate? Match(PlotFrame frame,TitleTemplateLibrary library)
            // 同一块名可对应多个尺寸模板（动态块），优先使用识别时选中的模板。
            => frame.Type!=FrameType.BlockReference?null:
                library.Templates.FirstOrDefault(t=>!string.IsNullOrEmpty(frame.TemplateId)&&t.Id==frame.TemplateId)
                ?? library.Templates.FirstOrDefault(t=>string.Equals(t.BlockName.Trim(),frame.SourceBlockName,StringComparison.OrdinalIgnoreCase));
        public static string Naming(PlotFrame frame,TitleTemplateLibrary library,string fallback)
        {var t=Match(frame,library);return string.IsNullOrWhiteSpace(t?.NamingTemplate)?fallback:t!.NamingTemplate!;}
        public static List<KeyValuePair<string,CatalogOptions>> CatalogChoices(IEnumerable<PlotFrame> frames,TitleTemplateLibrary library,CatalogOptions fallback)
        {
            var choices=new List<KeyValuePair<string,CatalogOptions>>{new KeyValuePair<string,CatalogOptions>("通用目录设置",fallback.Copy())};
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var frame in frames)
            {
                var t=Match(frame,library);
                if(t?.Catalog!=null&&seen.Add(t.Id))choices.Add(new KeyValuePair<string,CatalogOptions>("图框模板："+t.Name,t.Catalog.Copy()));
            }
            return choices;
        }
    }
}
