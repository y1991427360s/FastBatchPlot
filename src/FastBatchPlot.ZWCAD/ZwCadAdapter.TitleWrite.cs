using System;
using System.Collections.Generic;
using System.Linq;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.Geometry;

namespace FastBatchPlot.ZWCAD
{
    public partial class ZwCadAdapter : ICadTitleWriteHost
    {
        public TitleWritePlan PrepareTitleWrite(PlotFrame frame,TitleBlockTemplate template,IDictionary<TitleField,string> values)
            =>TitleWritePlanner.Create(frame,template,values,CollectTemplateTexts(frame));

        private static void DescribeWriteTarget(Entity entity,TemplateText text)
        {
            var tr=entity.Database.TransactionManager.TopTransaction;
            text.Handle=entity.Handle.ToString();text.Owner=entity.OwnerId.Handle.ToString();
            text.Kind=entity is AttributeReference a?(a.IsMTextAttribute?"MTextAttribute":"Attribute"):entity is MText?"MText":"Text";
            text.RawText=entity is MText mt?mt.Contents:((DBText)entity).TextString;
            if(entity is AttributeReference attr&&attr.IsMTextAttribute)
                using(var value=attr.MTextAttribute)text.RawText=value.Contents;
            var owner=tr.GetObject(entity.OwnerId,OpenMode.ForRead);
            bool independent=owner is BlockTableRecord space&&space.IsLayout;
            if(owner is BlockReference block)
                independent=tr.GetObject(block.OwnerId,OpenMode.ForRead) is BlockTableRecord parent&&parent.IsLayout;
            text.WriteBlockReason=independent?"":"共享块定义或嵌套属性，修改会影响其他块实例，暂不写回。";
            if(entity is AttributeDefinition)text.WriteBlockReason="常量属性属于共享块定义，暂不写回。";
            if(entity.HasFields)text.WriteBlockReason="目标包含 CAD 字段，不能用普通文本覆盖。";
            if(((LayerTableRecord)tr.GetObject(entity.LayerId,OpenMode.ForRead)).IsLocked)text.WriteBlockReason="目标图层已锁定。";
            if(owner is BlockReference parentBlock&&((LayerTableRecord)tr.GetObject(parentBlock.LayerId,OpenMode.ForRead)).IsLocked)
                text.WriteBlockReason="图框所在图层已锁定。";
        }
        public void ApplyTitleWrites(IReadOnlyList<TitleWritePlan> plans,bool undo=false)
        {
            TitleWritePlanner.ValidateBatch(plans);
            var doc=Application.DocumentManager.MdiActiveDocument;
            if(doc==null || plans.Any(p=>p.Source.SourceDocumentId!=DocumentSessionIdentity.Get(doc)))
                throw new InvalidOperationException("当前文档不是写回来源文档。");
            using(doc.LockDocument())
            using(var tr=doc.Database.TransactionManager.StartTransaction())
            {
                if(!undo)
                    foreach(var plan in plans)
                    {
                        var fresh=PrepareTitleWrite(plan.Source,plan.Template,new Dictionary<TitleField,string>(plan.Values.ToDictionary(p=>p.Key,p=>p.Value)));
                        if(!TitleWritePlanner.SameTargets(plan,fresh))throw new InvalidOperationException("文字内容、定位或模板匹配已变化，请重新预览。");
                    }
                // 所有目标完成校验后才进入写阶段；任一失败由同一事务回滚整批。
                var targets=new List<Tuple<Entity,TitleTextChange>>();
                foreach(var plan in plans)
                {
                    OpenTemplateBlock(doc,tr,plan.Source);
                    foreach(var change in plan.Changes)
                    {
                        var entity=tr.GetObject(doc.Database.GetObjectId(false,new Handle(long.Parse(change.Handle,System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture)),0),OpenMode.ForRead) as Entity;
                        if(entity==null||entity.IsErased)throw new InvalidOperationException("写回目标已删除。");
                        var now=new TemplateText();DescribeWriteTarget(entity,now);
                        if(now.Owner!=change.Owner||now.Kind!=change.Kind||now.RawText!=(undo?change.After:change.Before)||now.WriteBlockReason.Length>0)
                            throw new InvalidOperationException("目标身份、旧值或可写状态已变化，整批未修改。");
                        targets.Add(Tuple.Create(entity,change));
                    }
                }
                foreach(var target in targets)
                {
                    var entity=target.Item1;var change=target.Item2;string value=undo?change.Before:change.After;
                    entity.UpgradeOpen();
                    if(entity is AttributeReference attr&&attr.IsMTextAttribute)
                    {using(var mt=attr.MTextAttribute){mt.Contents=value;attr.MTextAttribute=mt;}attr.UpdateMTextAttribute();}
                    else if(entity is MText mt)mt.Contents=value;
                    else ((DBText)entity).TextString=value;
                    // AdjustAlignment 可能依赖工作数据库，但这里始终为当前文档数据库。
                    if(entity is DBText text)text.AdjustAlignment(doc.Database);
                    var verify=new TemplateText();DescribeWriteTarget(entity,verify);
                    if(verify.RawText!=value)throw new InvalidOperationException("宿主未保留完整文字值，整批已回滚。");
                }
                tr.Commit();
            }
        }
    }
}

