using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PCCheck
{
    /// <summary>
    /// 直近 60 秒の使用率(%)を折れ線グラフで描く部品
    /// 최근 60초 사용률(%)을 꺾은선 그래프로 그리는 부품
    /// </summary>
    internal class GraphPanel : Control
    {
        private const int MaxPoints = 60;
        private readonly List<double> _values = new List<double>();
        private string _title = "", _valueText = "";
        private Font _bold;

        public Color LineColor { get; set; }
        public string Title { get { return _title; } set { _title = value; Invalidate(); } }
        public string ValueText { get { return _valueText; } set { _valueText = value; Invalidate(); } }

        public GraphPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.White;
            LineColor = Color.FromArgb(0, 120, 215);
        }

        public void Add(double v)
        {
            _values.Add(Math.Max(0, Math.Min(100, v)));
            if (_values.Count > MaxPoints) _values.RemoveAt(0);
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (_bold != null) _bold.Dispose();
            _bold = null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            if (_bold == null) _bold = new Font(Font, FontStyle.Bold);

            int pad = 8;
            int textH = Font.Height + 4;
            // タイトル（左）と現在値（右） / 제목(왼쪽)과 현재 값(오른쪽)
            TextRenderer.DrawText(g, _title, _bold, new Point(pad, pad / 2), ForeColor);
            TextRenderer.DrawText(g, _valueText, _bold, new Rectangle(0, pad / 2, Width - pad, textH), LineColor,
                                  TextFormatFlags.Right | TextFormatFlags.Top);

            var area = new Rectangle(pad, pad / 2 + textH + 2, Width - pad * 2 - 1, Height - textH - pad * 2);
            if (area.Width <= 0 || area.Height <= 0) return;

            // 目盛り線（25% ごと） / 눈금선 (25%마다)
            using (var grid = new Pen(Color.FromArgb(232, 232, 232)))
            {
                for (int i = 1; i < 4; i++)
                {
                    int y = area.Top + area.Height * i / 4;
                    g.DrawLine(grid, area.Left, y, area.Right, y);
                }
            }
            using (var border = new Pen(Color.FromArgb(200, 200, 200))) g.DrawRectangle(border, area);

            if (_values.Count < 2) return;

            // 右端が最新。古い値ほど左へ / 오른쪽 끝이 최신. 오래된 값일수록 왼쪽으로
            var pts = new PointF[_values.Count];
            float step = (float)area.Width / (MaxPoints - 1);
            for (int i = 0; i < _values.Count; i++)
            {
                float x = area.Right - (_values.Count - 1 - i) * step;
                float y = area.Bottom - (float)(_values[i] / 100.0 * area.Height);
                pts[i] = new PointF(x, y);
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;
            var fill = new List<PointF>(pts) { new PointF(pts[pts.Length - 1].X, area.Bottom), new PointF(pts[0].X, area.Bottom) };
            using (var b = new SolidBrush(Color.FromArgb(40, LineColor))) g.FillPolygon(b, fill.ToArray());
            using (var p = new Pen(LineColor, 2f)) g.DrawLines(p, pts);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _bold != null) _bold.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>ちらつき防止のダブルバッファ付き ListView / 깜빡임 방지용 더블 버퍼 ListView</summary>
    internal class BufferedListView : ListView
    {
        public BufferedListView() { DoubleBuffered = true; }
    }
}
