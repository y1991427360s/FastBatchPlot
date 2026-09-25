using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Templates
{
    public sealed class TitleTextChange
    {
        public TitleField Field {get;}
        public string Handle {get;}
        public string Owner {get;}
        public string Kind {get;}
        public string Before {get;}
        public string After {get;}
        public double X {get;}
        public double Y {get;}
        internal TitleTextChange(TitleField field,TemplateText target,string value)
        {Field=field;Handle=target.Handle;Owner=target.Owner;Kind=target.Kind;Before=target.RawText;After=value;X=target.X;Y=target.Y;}
    }
    public sealed class TitleWritePlan
    {
        private readonly PlotFrame source;
        private readonly TitleBlockTemplate template;
        private readonly Dictionary<TitleField,string> values;
        public PlotFrame Source=>CopyFrame(source);
        public TitleBlockTemplate Template {get {var t=TitleTemplateService.Clone(template);t.Id=template.Id;return t;}}
        public IReadOnlyDictionary<TitleField,string> Values=>new ReadOnlyDictionary<TitleField,string>(values);
        public ReadOnlyCollection<TitleTextChange> Changes {get;}
        public ReadOnlyCollection<string> Errors {get;}
        public bool CanApply=>Errors.Count==0;
        internal TitleWritePlan(PlotFrame frame,TitleBlockTemplate t,IDictionary<TitleField,string> desired,List<TitleTextChange> changes,List<string> errors)
        {source=CopyFrame(frame);template=TitleTemplateService.Clone(t);template.Id=t.Id;values=new Dictionary<TitleField,string>(desired);Changes=changes.AsReadOnly();Errors=errors.AsReadOnly();}
        private static PlotFrame CopyFrame(PlotFrame f)=>new PlotFrame{Type=f.Type,SourceDocumentId=f.SourceDocumentId,SourceLayoutId=f.SourceLayoutId,HandleOrId=f.HandleOrId,SourceBlockName=f.SourceBlockName,LayoutName=f.LayoutName,OrderIndex=f.OrderIndex};
    }
    public static class TitleWritePlanner
    {
        public static TitleWritePlan Create(PlotFrame frame,TitleBlockTemplate template,IDictionary<TitleField,string> values,IEnumerable<TemplateText> texts)
        {
            TitleTemplateService.Validate(new TitleTemplateLibrary{Templates=new List<TitleBlockTemplate>{template}});
            var items=texts.ToList();var changes=new List<TitleTextChange>();var errors=new List<string>();var used=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if(items.Any(t=>t==null||!TitleTemplateService.Finite(t.X)||!TitleTemplateService.Finite(t.Y)||t.Text==null||t.RawText==null||t.AttributeTag==null))
                throw new ArgumentException("文字定位数据无效。");
            foreach(var pair in values)
            {
                if(!Enum.IsDefined(typeof(TitleField),pair.Key))throw new ArgumentException("未知标题字段。");
                string label=TitleTemplateService.FieldLabels[(int)pair.Key];
                if(pair.Value==null||pair.Value.Length>4096||pair.Value.Any(c=>char.IsControl(c)&&c!='\n'&&c!='\r')){errors.Add(label+"：值过长或含控制字符。");continue;}
                if(pair.Value.Contains("%<")||pair.Value.Contains("%%")){errors.Add(label+"：新值含 CAD 字段或文字控制码，不能按普通文字写回。");continue;}
                var rule=template.Fields.SingleOrDefault(r=>r.Field==pair.Key);
                if(rule==null){errors.Add(label+"：模板未配置该字段。");continue;}
                var r=rule.Region;bool byTag=!string.IsNullOrWhiteSpace(rule.AttributeTag);
                var matches=items.Where(t=>byTag?string.Equals(t.AttributeTag,rule.AttributeTag.Trim(),StringComparison.OrdinalIgnoreCase):t.X>=r.X1&&t.X<=r.X2&&t.Y>=r.Y1&&t.Y<=r.Y2).ToList();
                if(matches.Count!=1){errors.Add(label+"：匹配 "+matches.Count+" 个对象，写回必须唯一。");continue;}
                var target=matches[0];
                if(target.Kind!="Text"&&target.Kind!="Attribute"&&target.Kind!="MText"&&target.Kind!="MTextAttribute")
                {errors.Add(label+"：不支持的文字对象类型。");continue;}
                if(!used.Add(target.Handle)){errors.Add(label+"：多个字段指向同一对象。");continue;}
                if(!string.IsNullOrEmpty(target.WriteBlockReason)||string.IsNullOrWhiteSpace(target.Handle))
                {errors.Add(label+"："+(target.WriteBlockReason.Length>0?target.WriteBlockReason:"缺少实体身份。"));continue;}
                if(target.Kind!="MText"&&target.Kind!="MTextAttribute"&&(pair.Value.Contains("\n")||pair.Value.Contains("\r")))
                {errors.Add(label+"：单行文字不能写入换行。");continue;}
                string value=target.Kind=="MText"||target.Kind=="MTextAttribute"?EscapeMText(pair.Value):pair.Value;
                // 相同显示文本不清除原有格式；变更的多行文字将替换行内格式。
                if(target.Text==pair.Value)continue;
                changes.Add(new TitleTextChange(pair.Key,target,value));
            }
            return new TitleWritePlan(frame,template,values,changes,errors);
        }
        public static string EscapeMText(string text)=>text.Replace("\\","\\\\").Replace("{","\\{").Replace("}","\\}").Replace("\r\n","\n").Replace("\r","\n").Replace("\n","\\P");
        public static void ValidateBatch(IEnumerable<TitleWritePlan> plans)
        {
            var used=new HashSet<string>(StringComparer.OrdinalIgnoreCase);string? document=null;int count=0;
            foreach(var p in plans)
            {
                count++;if(!p.CanApply)throw new InvalidOperationException("写回预检未通过："+string.Join("；",p.Errors));
                var f=p.Source;if(string.IsNullOrWhiteSpace(f.SourceDocumentId))throw new InvalidOperationException("来源文档身份缺失。");
                if(document!=null&&document!=f.SourceDocumentId)throw new InvalidOperationException("一次写回必须属于同一文档。");document=f.SourceDocumentId;
                foreach(var c in p.Changes)if(!used.Add(c.Handle))throw new InvalidOperationException("不同图框指向同一文字对象，拒绝重复写回。");
            }
            if(count==0)throw new InvalidOperationException("没有写回任务。");
        }
        public static bool SameTargets(TitleWritePlan a,TitleWritePlan b)=>b.CanApply&&a.Changes.Count==b.Changes.Count&&a.Changes.Zip(b.Changes,(x,y)=>
            x.Handle==y.Handle&&x.Owner==y.Owner&&x.Kind==y.Kind&&x.Before==y.Before&&x.After==y.After&&Math.Abs(x.X-y.X)<1e-8&&Math.Abs(x.Y-y.Y)<1e-8).All(v=>v);
    }
}

