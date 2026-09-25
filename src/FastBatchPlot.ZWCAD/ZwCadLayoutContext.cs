using System;
using System.Collections.Generic;
using System.Linq;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;

namespace FastBatchPlot.ZWCAD
{
    /// <summary>仅在持有文档锁期间使用；来源使用 BTR handle，名称只用于 CAD 的布局切换。</summary>
    internal sealed class ZwCadLayoutContext : IDisposable
    {
        private readonly Document _document;
        private readonly string _originalLayout;
        private readonly ObjectId _originalSpace;
        private readonly string _targetLayout;
        private readonly ObjectId _targetSpace;
        private readonly ViewTableRecord _originalView;
        private bool _activationAttempted;
        private readonly bool _originalFloating;
        private bool _switchedToPaperSpace;
        private bool _layoutSwitchAttempted;
        private bool _disposed;

        public static IEnumerable<Layout> ReadLayouts(Database database, Transaction transaction)
        {
            var dictionary = (DBDictionary)transaction.GetObject(database.LayoutDictionaryId, OpenMode.ForRead);
            foreach (DBDictionaryEntry entry in dictionary)
            {
                var layout = transaction.GetObject(entry.Value, OpenMode.ForRead) as Layout;
                if (layout != null && !layout.IsErased && !layout.BlockTableRecordId.IsNull
                    && !layout.BlockTableRecordId.IsErased) yield return layout;
            }
        }

        public ZwCadLayoutContext(Document document, PlotFrame frame)
        {
            _document = document;
            if (string.IsNullOrEmpty(frame.SourceDocumentId) || string.IsNullOrEmpty(frame.SourceLayoutId)
                || !string.Equals(frame.SourceDocumentId, DocumentSessionIdentity.Get(document), StringComparison.Ordinal))
                throw new InvalidOperationException("图框不属于当前活动文档，请切回源图或重新搜索图框。");
            EnsureActiveDocument();
            _originalFloating = InFloatingViewport();
            using (var transaction = document.Database.TransactionManager.StartTransaction())
            {
                var layouts = ReadLayouts(document.Database, transaction).ToList();
                var target = layouts.FirstOrDefault(layout =>
                    string.Equals(layout.BlockTableRecordId.Handle.ToString(), frame.SourceLayoutId, StringComparison.Ordinal));
                if (target == null) throw new InvalidOperationException("图框来源布局已删除或不可访问，请重新搜索图框。");
                _targetLayout = target.LayoutName;
                _targetSpace = target.BlockTableRecordId;
                _originalLayout = LayoutManager.Current.CurrentLayout;
                // 在布局的浮动视口中时 CurrentSpaceId 为模型空间；恢复目标记为该布局的图纸空间。
                _originalSpace = _originalFloating
                    ? (layouts.FirstOrDefault(l => string.Equals(l.LayoutName, _originalLayout, StringComparison.Ordinal))?.BlockTableRecordId ?? document.Database.CurrentSpaceId)
                    : document.Database.CurrentSpaceId;
                transaction.Commit();
            }
            _originalView = document.Editor.GetCurrentView();
        }

        public void Activate()
        {
            EnsureActiveDocument();
            // 在 setter 前标记，使切换部分成功但随后抛错也会恢复。
            _activationAttempted = true;
            // 布局保存时激活了视口（很常见），切过去后先回到图纸空间，结束后再恢复。
            ExitFloatingViewport();
            if (!IsAtLayout(_targetLayout, _targetSpace))
            {
                _layoutSwitchAttempted = true;
                LayoutManager.Current.CurrentLayout = _targetLayout;
            }
            ExitFloatingViewport();
            if (!IsAtLayout(_targetLayout, _targetSpace))
                throw new InvalidOperationException("CAD 未能切换到图框来源空间，已停止打印。");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (!_activationAttempted) return;
                EnsureActiveDocument();
                if (!IsAtLayout(_originalLayout, _originalSpace))
                {
                    _layoutSwitchAttempted = true;
                    LayoutManager.Current.CurrentLayout = _originalLayout;
                    ExitFloatingViewport();
                }
                if (!IsAtLayout(_originalLayout, _originalSpace))
                    throw new InvalidOperationException("原空间校验不一致。");
                // 用户原本在浮动视口内工作时先恢复该状态；原视图是视口内的模型视图，必须在视口中恢复。
                if (_originalFloating && (_switchedToPaperSpace || _layoutSwitchAttempted) && !InFloatingViewport())
                    _document.Editor.SwitchToModelSpace();
                // 同布局输出未改视图；重复写入相同视图也会触发数据库修改事件。
                if (_layoutSwitchAttempted) _document.Editor.SetCurrentView(_originalView);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("打印后恢复原布局或视图失败，未提交输出文件：" + ex.Message, ex);
            }
            finally { _originalView.Dispose(); }
        }

        private bool IsAtLayout(string layoutName, ObjectId space)
            => string.Equals(LayoutManager.Current.CurrentLayout, layoutName, StringComparison.Ordinal)
                && _document.Database.CurrentSpaceId == space;

        private void EnsureActiveDocument()
        {
            if (!ReferenceEquals(Application.DocumentManager.MdiActiveDocument, _document))
                throw new InvalidOperationException("活动文档已改变，不能在其他文档继续切换布局或打印。");
        }

        private static bool InFloatingViewport() =>
            Convert.ToInt32(Application.GetSystemVariable("TILEMODE")) == 0 &&
            Convert.ToInt32(Application.GetSystemVariable("CVPORT")) != 1;

        private void ExitFloatingViewport()
        {
            if (!InFloatingViewport()) return;
            _document.Editor.SwitchToPaperSpace();
            _switchedToPaperSpace = true;
            if (InFloatingViewport()) throw new NotSupportedException("无法退出布局中的浮动视口，请手动双击视口外后重试。");
        }
    }
}
