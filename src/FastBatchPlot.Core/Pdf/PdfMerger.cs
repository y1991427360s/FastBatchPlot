using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace FastBatchPlot.Core.Pdf
{
    public sealed class PdfMergeProgress
    {
        public int CompletedPages { get; }
        public int TotalFiles { get; }
        public bool ReadyToCommit { get; }
        public PdfMergeProgress(int completedPages, int totalFiles, bool readyToCommit = false)
        { CompletedPages = completedPages; TotalFiles = totalFiles; ReadyToCommit = readyToCommit; }
    }
    public class PdfMergeItem
    {
        public string FilePath { get; set; } = string.Empty;
        public string BookmarkTitle { get; set; } = string.Empty;

        public PdfMergeItem() { }

        public PdfMergeItem(string filePath, string bookmarkTitle)
        {
            FilePath = filePath;
            BookmarkTitle = bookmarkTitle;
        }
    }

    /// <summary>
    /// 基于 PdfSharpCore 合并 PDF 并为每个输入生成一级书签。
    /// </summary>
    public static class PdfMerger
    {
        /// <summary>
        /// 合并多个PDF文件并生成左侧书签导航
        /// </summary>
        public static bool MergePdfFiles(IList<PdfMergeItem> items, string outputPdfPath, out string errorMessage, bool overwrite = true)
            => MergePdfFiles(items, outputPdfPath, CancellationToken.None, out errorMessage, overwrite);

        /// <summary>仅处理文件，可在线程池调用。取消通过 OperationCanceledException 区别于失败；输入文件永不删除。</summary>
        public static bool MergePdfFiles(IList<PdfMergeItem> items, string outputPdfPath, CancellationToken cancellationToken,
            out string errorMessage, bool overwrite = true, IProgress<PdfMergeProgress>? progress = null)
        {
            errorMessage = string.Empty;
            cancellationToken.ThrowIfCancellationRequested();

            if (items == null || items.Count == 0)
            {
                errorMessage = "没有需要合并的PDF文件。";
                return false;
            }

            string? temporaryOutput = null;
            try
            {
                outputPdfPath = Path.GetFullPath(outputPdfPath);
                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.Equals(Path.GetFullPath(item.FilePath), outputPdfPath, StringComparison.OrdinalIgnoreCase))
                    {
                        errorMessage = "合并输出不能与任何输入文件相同。";
                        return false;
                    }
                }
                string? outDir = Path.GetDirectoryName(outputPdfPath);
                if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                {
                    Directory.CreateDirectory(outDir);
                }

                temporaryOutput = Path.Combine(outDir!, ".batchplot-" + Guid.NewGuid().ToString("N") + ".tmp.pdf");
                using (var outputDocument = new PdfDocument())
                {
                    var layers = new PdfLayerMerger(outputDocument);
                    int completedPages = 0;
                    // 设置页面模式为显示书签大纲
                    outputDocument.PageMode = PdfPageMode.UseOutlines;

                    for (int i = 0; i < items.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var item = items[i];
                        if (!File.Exists(item.FilePath))
                        {
                            errorMessage = $"源PDF文件不存在：{item.FilePath}";
                            return false;
                        }

                        using (var inputDocument = PdfReader.Open(item.FilePath, PdfDocumentOpenMode.Import))
                        {
                            int pageCount = inputDocument.PageCount;
                            if (pageCount == 0) throw new InvalidDataException($"源PDF没有页面：{item.FilePath}");
                            PdfPage? firstAddedPage = null;

                            for (int p = 0; p < pageCount; p++)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                var page = inputDocument.Pages[p];
                                var addedPage = outputDocument.AddPage(page);
                                if (firstAddedPage == null)
                                {
                                    firstAddedPage = addedPage;
                                }
                                progress?.Report(new PdfMergeProgress(++completedPages, items.Count));
                                cancellationToken.ThrowIfCancellationRequested();
                            }

                            // 添加书签
                            string title = string.IsNullOrWhiteSpace(item.BookmarkTitle)
                                ? $"第 {i + 1} 页 ({Path.GetFileNameWithoutExtension(item.FilePath)})"
                                : item.BookmarkTitle;

                            if (firstAddedPage != null)
                            {
                                var outline = outputDocument.Outlines.Add(title, firstAddedPage, true);
                            }
                            layers.Add(inputDocument, title);
                        }
                    }

                    // 保存合并后的文件
                    cancellationToken.ThrowIfCancellationRequested();
                    layers.Finish();
                    outputDocument.Save(temporaryOutput);
                    progress?.Report(new PdfMergeProgress(completedPages, items.Count, readyToCommit: true));
                }

                // Save 不能中断，但只写本次临时文件；取消后不发布或替换目标。
                cancellationToken.ThrowIfCancellationRequested();
                if (overwrite) CommitFile(temporaryOutput, outputPdfPath);
                else File.Move(temporaryOutput, outputPdfPath);

                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                errorMessage = $"PDF合并发生异常: {ex.Message}";
                return false;
            }
            finally
            {
                if (temporaryOutput != null && File.Exists(temporaryOutput))
                    try { File.Delete(temporaryOutput); } catch { }
            }
        }

        /// <summary>
        /// 批量旋转指定PDF文件的所有页面
        /// </summary>
        public static bool RotatePdf(string pdfPath, int rotationDegrees, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (rotationDegrees % 90 != 0)
            {
                errorMessage = "PDF旋转角度必须为90的整数倍。";
                return false;
            }
            if (!File.Exists(pdfPath))
            {
                errorMessage = $"PDF文件不存在: {pdfPath}";
                return false;
            }

            string? tempPath = null;
            try
            {
                pdfPath = Path.GetFullPath(pdfPath);
                tempPath = Path.Combine(Path.GetDirectoryName(pdfPath)!, ".batchplot-" + Guid.NewGuid().ToString("N") + ".tmp.pdf");

                using (var doc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Modify))
                {
                    foreach (PdfPage page in doc.Pages)
                    {
                        page.Rotate = ((page.Rotate + rotationDegrees % 360) % 360 + 360) % 360;
                    }
                    doc.Save(tempPath);
                }

                CommitFile(tempPath, pdfPath);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"旋转PDF异常: {ex.Message}";
                return false;
            }
            finally
            {
                if (tempPath != null && File.Exists(tempPath))
                    try { File.Delete(tempPath); } catch { }
            }
        }

        private static void CommitFile(string source, string target)
        {
            // 临时文件与目标同目录，失败时不通过复制截断原目标。
            if (File.Exists(target)) File.Replace(source, target, null);
            else File.Move(source, target);
        }
    }
}

