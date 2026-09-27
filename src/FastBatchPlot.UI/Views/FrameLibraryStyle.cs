using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FastBatchPlot.UI.Views
{
    /// <summary>图框信息库各窗口的共用外观（仿原版绿色图标按钮）与 CAD 点选时的窗口隐藏。</summary>
    internal static class FrameLibraryStyle
    {
        private static readonly Color Green = Color.FromArgb(0, 128, 64);
        public static readonly Color Accent = Color.FromArgb(0, 70, 170);
        public static readonly Image ActionIcon = Draw(g =>
        {
            using (var brush = new SolidBrush(Green))
                g.FillPolygon(brush, new[] { new PointF(3, 21), new PointF(11, 3), new PointF(14, 3), new PointF(21, 21), new PointF(17, 21), new PointF(12.2f, 9), new PointF(7, 21) });
            using (var pen = new Pen(Green, 2.2f)) g.DrawLine(pen, 8.5f, 15.5f, 18, 13);
        });
        public static readonly Image OkIcon = Draw(g => { using (var pen = new Pen(Green, 3.4f)) g.DrawLines(pen, new[] { new PointF(3, 12), new PointF(9.5f, 18.5f), new PointF(21, 5) }); });
        public static readonly Image CancelIcon = Draw(g =>
        {
            using (var pen = new Pen(Green, 3.4f)) { g.DrawLine(pen, 5, 5, 19, 19); g.DrawLine(pen, 19, 5, 5, 19); }
        });
        public static readonly Image CopyIcon = Draw(g =>
        {
            using (var fill = new SolidBrush(Color.FromArgb(214, 236, 222)))
            using (var pen = new Pen(Green, 1.6f))
            {
                g.FillRectangle(fill, 3, 3, 12, 15); g.DrawRectangle(pen, 3, 3, 12, 15);
                g.FillRectangle(Brushes.White, 9, 7, 12, 15); g.DrawRectangle(pen, 9, 7, 12, 15);
                g.DrawLine(pen, 12, 12, 18, 12); g.DrawLine(pen, 12, 16, 18, 16);
            }
        });

        private static Image Draw(Action<Graphics> paint)
        {
            var bitmap = new Bitmap(24, 24);
            using (var g = Graphics.FromImage(bitmap)) { g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent); paint(g); }
            return bitmap;
        }

        public static Button CreateButton(string text, Image image)
        {
            var button = new Button { Text = text, Image = image, ImageAlign = ContentAlignment.MiddleLeft, TextImageRelation = TextImageRelation.ImageBeforeText,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlatStyle = FlatStyle.Flat, Padding = new Padding(4, 2, 6, 2), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(222, 238, 228);
            return button;
        }

        /// <summary>在 CAD 中点选期间隐藏当前对话框及其所有者窗口，结束后按原层次恢复。</summary>
        public static T HiddenWhile<T>(Form top, Func<T> interact)
        {
            var hidden = new List<Form>();
            for (Form? form = top; form != null; form = form.Owner)
                if (form.Visible && !form.IsDisposed) hidden.Add(form);
            foreach (var form in hidden) form.Hide();
            try { return interact(); }
            finally
            {
                for (int i = hidden.Count - 1; i >= 0; i--) if (!hidden[i].IsDisposed) hidden[i].Show();
                if (hidden.Contains(top) && !top.IsDisposed) top.Activate();
            }
        }
    }
}
