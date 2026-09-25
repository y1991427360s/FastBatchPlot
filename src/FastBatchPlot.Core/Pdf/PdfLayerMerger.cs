using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.Advanced;

namespace FastBatchPlot.Core.Pdf
{
    /// <summary>页面导入完成后合并文档级可选内容，复用导入表中的 OCG 对象，避免改变 CAD 图层显示。</summary>
    internal sealed class PdfLayerMerger
    {
        private readonly PdfDocument output;
        private readonly PdfArray groups, on, off, order, locked, radio, automatic;
        private readonly List<SourceConfiguration> sources = new List<SourceConfiguration>();
        private string? intent;
        private string? listMode;
        private int nodes;

        public PdfLayerMerger(PdfDocument output)
        {
            this.output=output;
            groups=new PdfArray(output);on=new PdfArray(output);off=new PdfArray(output);order=new PdfArray(output);
            locked=new PdfArray(output);radio=new PdfArray(output);automatic=new PdfArray(output);
        }

        public void Add(PdfDocument input,string title)
        {
            var properties=Dictionary(input.Internals.Catalog.Elements["/OCProperties"],false);
            if(properties==null)
            {
                // 缺少目录却引用 OCG 的文件不能当普通 PDF 合并。
                if(input.Internals.GetAllObjects().OfType<PdfDictionary>().Any(d=>d.Elements.GetName("/Type")=="/OCG" && Mapped(output,d)!=null))
                    throw new InvalidDataException("源 PDF 使用图层但缺少 OCProperties，不能保证合并后显示一致。");
                return;
            }
            Allowed(properties,"/OCGs","/D","/Configs");
            var declared=Array(properties.Elements["/OCGs"],true)!;
            if(declared.Elements.Count==0)throw new InvalidDataException("PDF 图层清单为空。");
            var defaults=Dictionary(properties.Elements["/D"],true)!;
            var copier=new Copier(output);
            var source=new SourceConfiguration { Title=title };
            foreach(var item in declared.Elements)
            {
                var group=Dictionary(item,true)!;
                if(!group.IsIndirect || group.Elements.GetName("/Type")!="/OCG" || !source.Groups.Add(group))
                    throw new InvalidDataException("PDF 图层清单含无效或重复 OCG。");
                var mapped=copier.Copy(item);
                source.OutputGroups.Add(mapped);
                groups.Elements.Add(mapped);
            }
            foreach(var group in input.Internals.GetAllObjects().OfType<PdfDictionary>())
                if(group.Elements.GetName("/Type")=="/OCG" && Mapped(output,group)!=null && !source.Groups.Contains(group))
                    throw new InvalidDataException("页面引用了未登记的 PDF 图层，不能安全合并。");
            ValidateConfiguration(defaults,source.Groups);
            string sourceIntent=Intent(defaults);
            if(intent!=null && intent!=sourceIntent)
                throw new InvalidDataException("各 PDF 的图层 Intent 不一致，不能在同一默认配置中保留显示行为。");
            intent=sourceIntent;
            string mode=defaults.Elements.GetName("/ListMode");
            if(mode=="")mode="/AllPages";
            if(mode!="/AllPages"&&mode!="/VisiblePages")throw new InvalidDataException("PDF 图层列表模式无效。");
            if(listMode!=null&&listMode!=mode)throw new InvalidDataException("PDF 图层列表模式不一致，不能安全合并。");
            listMode=mode;
            source.Default=Dictionary(copier.Copy(defaults),true)!;
            ScopeUsage(source.Default,source.OutputGroups);
            var extras=Array(properties.Elements["/Configs"],false);
            if(extras!=null)
                foreach(var item in extras.Elements)
                {
                    var extra=Dictionary(item,true)!;
                    ValidateConfiguration(extra,source.Groups);
                    if(Intent(extra)!=sourceIntent)throw new InvalidDataException("PDF 备用图层配置使用不同 Intent，不能安全合并。");
                    var cloned=Dictionary(copier.Copy(extra),true)!;
                    ScopeUsage(cloned,source.OutputGroups);
                    source.Alternates.Add(cloned);
                }
            sources.Add(source);
            AppendConfiguration(source.Default,source.OutputGroups,on,off,locked,radio,automatic);
            var branch=new PdfArray(output);branch.Elements.Add(Text(title));
            var sourceOrder=Array(source.Default.Elements["/Order"],false);
            if(sourceOrder!=null)foreach(var item in sourceOrder.Elements)branch.Elements.Add(item);
            else foreach(var item in source.OutputGroups)branch.Elements.Add(item);
            order.Elements.Add(branch);
        }

        public void Finish()
        {
            if(sources.Count==0)return;
            if(output.Version<15)output.Version=15;
            var properties=new PdfDictionary(output);
            properties.Elements["/OCGs"]=groups;
            properties.Elements["/D"]=BuildDefault(on,off,locked,radio,automatic);
            var alternatives=new PdfArray(output);
            // 每个备用配置只切换其来源图纸；其他图纸保留各自默认状态。
            foreach(var selected in sources)
                foreach(var alternate in selected.Alternates)
                {
                    var altOn=new PdfArray(output);var altOff=new PdfArray(output);var altLocked=new PdfArray(output);
                    var altRadio=new PdfArray(output);var altAutomatic=new PdfArray(output);
                    foreach(var source in sources)
                        AppendConfiguration(source==selected?alternate:source.Default,source.OutputGroups,altOn,altOff,altLocked,altRadio,altAutomatic);
                    var combined=BuildDefault(altOn,altOff,altLocked,altRadio,altAutomatic);
                    string alternateMode=alternate.Elements.GetName("/ListMode");
                    if(alternateMode!="")combined.Elements.SetName("/ListMode",alternateMode);
                    var altOrder=new PdfArray(output);
                    foreach(var source in sources)
                    {
                        var branch=new PdfArray(output);branch.Elements.Add(Text(source.Title));
                        var selectedOrder=Array((source==selected?alternate:source.Default).Elements["/Order"],false);
                        foreach(var item in selectedOrder==null?source.OutputGroups:(IEnumerable<PdfItem>)selectedOrder.Elements)branch.Elements.Add(item);
                        altOrder.Elements.Add(branch);
                    }
                    combined.Elements["/Order"]=altOrder;
                    combined.Elements["/Name"]=Text(selected.Title+" - "+alternate.Elements.GetString("/Name"));
                    alternatives.Elements.Add(combined);
                }
            if(alternatives.Elements.Count>0)properties.Elements["/Configs"]=alternatives;
            output.Internals.Catalog.Elements["/OCProperties"]=properties;
        }

        private PdfDictionary BuildDefault(PdfArray enabled,PdfArray disabled,PdfArray fixedGroups,PdfArray radioGroups,PdfArray application)
        {
            var config=new PdfDictionary(output);
            config.Elements["/Name"]=Text("合并图纸图层");config.Elements.SetName("/BaseState","/ON");
            config.Elements["/ON"]=enabled;config.Elements["/OFF"]=disabled;config.Elements["/Order"]=order;
            config.Elements["/Intent"]=sources[0].Default.Elements["/Intent"]??new PdfName("/View");
            config.Elements.SetName("/ListMode",listMode!);
            if(fixedGroups.Elements.Count>0)config.Elements["/Locked"]=fixedGroups;
            if(radioGroups.Elements.Count>0)config.Elements["/RBGroups"]=radioGroups;
            if(application.Elements.Count>0)config.Elements["/AS"]=application;
            return config;
        }

        private static void AppendConfiguration(PdfDictionary config,List<PdfItem> sourceGroups,
            PdfArray enabled,PdfArray disabled,PdfArray fixedGroups,PdfArray radioGroups,PdfArray application)
        {
            bool baseOn=config.Elements.GetName("/BaseState")!="/OFF";
            var explicitOn=Objects(Array(config.Elements["/ON"],false));
            var explicitOff=Objects(Array(config.Elements["/OFF"],false));
            foreach(var group in sourceGroups)
            {
                var obj=Resolve(group);
                bool state=explicitOff.Contains(obj)?false:explicitOn.Contains(obj)?true:baseOn;
                (state?enabled:disabled).Elements.Add(group);
            }
            Append(config,"/Locked",fixedGroups);Append(config,"/RBGroups",radioGroups);Append(config,"/AS",application);
        }
        private static void Append(PdfDictionary from,string key,PdfArray into)
        {var array=Array(from.Elements[key],false);if(array!=null)foreach(var item in array.Elements)into.Elements.Add(item);}

        private void ScopeUsage(PdfDictionary config,List<PdfItem> sourceGroups)
        {
            var rules=Array(config.Elements["/AS"],false);
            if(rules==null)return;
            foreach(var entry in rules.Elements)
            {
                var rule=Dictionary(entry,true)!;
                if(rule.Elements["/OCGs"]==null)
                {
                    var scoped=new PdfArray(output);
                    foreach(var group in sourceGroups)scoped.Elements.Add(group);
                    rule.Elements["/OCGs"]=scoped;
                }
            }
        }

        private void ValidateConfiguration(PdfDictionary config,HashSet<PdfItem> declared)
        {
            Allowed(config,"/Name","/Creator","/BaseState","/ON","/OFF","/Intent","/AS","/Order","/ListMode","/RBGroups","/Locked");
            var state=config.Elements.GetName("/BaseState");
            if(state!=""&&state!="/ON"&&state!="/OFF")throw new InvalidDataException("PDF 图层配置 BaseState 无法确定，不能安全合并。");
            string mode=config.Elements.GetName("/ListMode");
            if(mode!=""&&mode!="/AllPages"&&mode!="/VisiblePages")throw new InvalidDataException("PDF 图层列表模式无效。");
            foreach(string key in new[]{"/ON","/OFF","/Locked"})ValidateGroupList(Array(config.Elements[key],false),declared);
            var enabled=Objects(Array(config.Elements["/ON"],false));
            if(enabled.Overlaps(Objects(Array(config.Elements["/OFF"],false))))throw new InvalidDataException("PDF 图层同时出现在 ON 和 OFF 中。");
            var rb=Array(config.Elements["/RBGroups"],false);
            if(rb!=null)foreach(var group in rb.Elements)ValidateGroupList(Array(group,true),declared);
            var application=Array(config.Elements["/AS"],false);
            if(application!=null)foreach(var item in application.Elements)
            {
                var rule=Dictionary(item,true)!;Allowed(rule,"/Event","/Category","/OCGs");
                string eventName=rule.Elements.GetName("/Event");
                if(eventName!="/View"&&eventName!="/Print"&&eventName!="/Export")throw new InvalidDataException("PDF 图层自动状态事件无效。");
                var categories=Array(rule.Elements["/Category"],true)!;
                if(categories.Elements.Count==0||categories.Elements.Any(c=>!(Resolve(c) is PdfName)))throw new InvalidDataException("PDF 图层自动状态类别无效。");
                // 缺 OCGs 的使用规则作用于全文；合并时必须限定原文档范围。
                ValidateGroupList(Array(rule.Elements["/OCGs"],false),declared);
            }
            ValidateOrder(config.Elements["/Order"],declared,new HashSet<PdfItem>(),0);
            Intent(config);
        }
        private void ValidateOrder(PdfItem? item,HashSet<PdfItem> declared,HashSet<PdfItem> path,int depth)
        {
            if(item==null)return;
            item=Resolve(item);
            if(++nodes>100000||depth>64)throw new InvalidDataException("PDF 图层配置过大或嵌套过深。");
            if(item is PdfString)return;
            if(item is PdfDictionary){if(!declared.Contains(item))throw new InvalidDataException("PDF 图层顺序引用未知图层。");return;}
            var array=Array(item,true)!;
            if(!path.Add(array))throw new InvalidDataException("PDF 图层顺序存在循环。");
            foreach(var child in array.Elements)ValidateOrder(child,declared,path,depth+1);
            path.Remove(array);
        }
        private static string Intent(PdfDictionary config)
        {
            var item=config.Elements["/Intent"];
            if(item==null)return "/View";
            item=Resolve(item);
            if(item is PdfName name)return name.Value;
            var array=Array(item,true)!;
            var names=new List<string>();
            foreach(var entry in array.Elements){if(!(Resolve(entry) is PdfName n))throw new InvalidDataException("PDF 图层 Intent 无效。");names.Add(n.Value);}
            return string.Join("|",names.Distinct().OrderBy(n=>n,StringComparer.Ordinal));
        }
        private static void ValidateGroupList(PdfArray? array,HashSet<PdfItem> declared)
        {if(array!=null)foreach(var item in array.Elements)if(!declared.Contains(Resolve(item)))throw new InvalidDataException("PDF 图层状态引用未知图层。");}
        private static HashSet<PdfItem> Objects(PdfArray? array)=>array==null?new HashSet<PdfItem>():new HashSet<PdfItem>(array.Elements.Select(Resolve));
        private static PdfItem Resolve(PdfItem item)=>item is PdfReference reference?reference.Value:item;
        private static PdfString Text(string text)=>new PdfString(text,PdfStringEncoding.Unicode);
        private static PdfObject? Mapped(PdfDocument output,PdfObject source)
        {
            // 此版本公开 API 在对象尚未被页面导入时抛 KeyNotFoundException，而非返回 null。
            try{return output.Internals.MapExternalObject(source);}catch(KeyNotFoundException){return null;}
        }
        private static PdfDictionary? Dictionary(PdfItem? item,bool required)
        {if(item==null&&!required)return null;return item!=null&&Resolve(item) is PdfDictionary d?d:throw new InvalidDataException("PDF 图层配置字典无效。");}
        private static PdfArray? Array(PdfItem? item,bool required)
        {if(item==null&&!required)return null;return item!=null&&Resolve(item) is PdfArray a?a:throw new InvalidDataException("PDF 图层配置数组无效。");}
        private static void Allowed(PdfDictionary dictionary,params string[] keys)
        {foreach(string key in dictionary.Elements.Keys)if(!keys.Contains(key))throw new InvalidDataException("暂不能安全保留 PDF 图层配置字段："+key);}
        private sealed class SourceConfiguration
        {
            public string Title="";
            public HashSet<PdfItem> Groups=new HashSet<PdfItem>();
            public List<PdfItem> OutputGroups=new List<PdfItem>();
            public PdfDictionary Default=null!;
            public List<PdfDictionary> Alternates=new List<PdfDictionary>();
        }

        private sealed class Copier
        {
            private readonly PdfDocument output;
            private readonly Dictionary<PdfObject,PdfObject> copied=new Dictionary<PdfObject,PdfObject>();
            private int count;
            public Copier(PdfDocument output){this.output=output;}
            public PdfItem Copy(PdfItem item,int depth=0)
            {
                if(++count>100000||depth>64)throw new InvalidDataException("PDF 图层对象过大或嵌套过深。");
                bool reference=item is PdfReference;
                item=Resolve(item);
                if(!(item is PdfObject obj))return item.Clone();
                if(obj.IsIndirect)
                {
                    var imported=Mapped(output,obj);
                    if(imported!=null)return imported.Reference;
                }
                if(copied.TryGetValue(obj,out var known))return known.IsIndirect?known.Reference:known;
                PdfObject result;
                if(obj is PdfDictionary dict)
                {
                    if(dict.Stream!=null)throw new InvalidDataException("PDF 图层配置不能包含数据流。");
                    result=new PdfDictionary(output);
                }
                else if(obj is PdfArray)result=new PdfArray(output);
                else result=obj.Clone();
                if(reference||obj.IsIndirect)output.Internals.AddObject(result);
                copied.Add(obj,result);
                if(obj is PdfDictionary from && result is PdfDictionary to)
                    foreach(var key in from.Elements.Keys)to.Elements[key]=Copy(from.Elements[key],depth+1);
                else if(obj is PdfArray fromArray && result is PdfArray toArray)
                    foreach(var entry in fromArray.Elements)toArray.Elements.Add(Copy(entry,depth+1));
                return result.IsIndirect?result.Reference:result;
            }
        }
    }
}
