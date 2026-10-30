using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VHDDSwitch.Core;

namespace VHDDSwitch.Desktop
{
    internal static class Fluent
    {
        internal static float Scale = 1;
        internal static readonly Color Background = Color.FromArgb(24, 24, 24);
        internal static readonly Color Sidebar = Color.FromArgb(32, 32, 32);
        internal static readonly Color Card = Color.FromArgb(36, 36, 36);
        internal static readonly Color Border = Color.FromArgb(54, 54, 54);
        internal static readonly Color Text = Color.FromArgb(245, 245, 245);
        internal static readonly Color Muted = Color.FromArgb(166, 166, 173);
        internal static readonly Color Accent = Color.FromArgb(145, 200, 255);
        internal static readonly Color Green = Color.FromArgb(108, 211, 167);
        internal static int S(float value) { return (int)Math.Round(value * Scale); }
        internal static Graphics Begin(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.ScaleTransform(Scale, Scale); return g;
        }
        internal static GraphicsPath Round(RectangleF r, float radius)
        {
            float d = radius * 2; var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
        }
        internal static void Box(Graphics g, RectangleF rect, Color fill, float radius, Color? border = null)
        {
            using (var p = Round(rect, radius))
            { using (var b = new SolidBrush(fill)) g.FillPath(b, p); if (border.HasValue) using (var pen = new Pen(border.Value)) g.DrawPath(pen, p); }
        }
        internal static void Write(Graphics g, string text, float x, float y, float width, float size, Color color, bool bold = false, float height = 28)
        {
            using (var f = new Font("Segoe UI Variable Text", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(color))
            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(text ?? "", f, b, new RectangleF(x, y, Math.Max(1, width), height), sf);
        }
        internal static void Glyph(Graphics g, string glyph, float x, float y, float size, Color color)
        { using (var f = new Font("Segoe Fluent Icons", size, FontStyle.Regular, GraphicsUnit.Pixel)) using (var b = new SolidBrush(color)) g.DrawString(glyph, f, b, x, y); }
        internal static void DiskIcon(Graphics g, float x, float y, Color color)
        {
            Box(g, new RectangleF(x, y, 42, 42), Color.FromArgb(43, 50, 59), 10);
            using (var p = new Pen(color, 1.5f))
            { g.DrawRectangle(p, x + 10, y + 10, 22, 21); g.DrawLine(p, x + 10, y + 25, x + 32, y + 25); g.DrawEllipse(p, x + 25, y + 28, 1, 1); }
        }
        internal static string Capacity(long value)
        { return value >= 1099511627776L ? (value / 1099511627776.0).ToString("0.0") + " TB" : (value / 1073741824.0).ToString("0.0") + " GB"; }
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        internal static void WindowStyle(IntPtr handle)
        {
            // Unsupported attributes return HRESULT on Windows 10; retain native frame.
            int dark = 1, rounded = 2, caption = 0x202020;
            DwmSetWindowAttribute(handle, 20, ref dark, 4);
            DwmSetWindowAttribute(handle, 33, ref rounded, 4);
            DwmSetWindowAttribute(handle, 35, ref caption, 4);
        }
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);
        internal static Icon AppIcon()
        {
            using (var bitmap = new Bitmap(48, 48))
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Box(g, new RectangleF(1, 1, 46, 46), Color.FromArgb(40, 65, 90), 12);
                for (int i = 0; i < 3; i++)
                {
                    Box(g, new RectangleF(11, 10 + i * 10, 26, 7), Color.FromArgb(180, 216, 252), 2);
                    using (var b = new SolidBrush(Color.FromArgb(40, 65, 90))) g.FillEllipse(b, 31, 12 + i * 10, 3, 3);
                }
                IntPtr handle = bitmap.GetHicon();
                try { using (var icon = Icon.FromHandle(handle)) return (Icon)icon.Clone(); }
                finally { DestroyIcon(handle); }
            }
        }
    }
    internal class FluentSurface : Panel
    {
        internal Action<Graphics, float, float> Draw;
        internal FluentSurface() { DoubleBuffered = true; BackColor = Fluent.Background; ResizeRedraw = true; }
        protected override void OnPaint(PaintEventArgs e)
        { base.OnPaint(e); if (Draw != null) Draw(Fluent.Begin(e), Width / Fluent.Scale, Height / Fluent.Scale); }
    }
    internal sealed class FluentButton : Button
    {
        internal bool Primary, Selected, Navigation;
        internal string Symbol;
        bool hover;
        internal FluentButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand; Height = Fluent.S(40); TabStop = true;
            ForeColor = Fluent.Text; BackColor = Fluent.Background;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Fluent.Begin(e); float w = Width / Fluent.Scale, h = Height / Fluent.Scale;
            Color fill = Primary ? Fluent.Accent : (Selected ? Color.FromArgb(49, 49, 49) : Color.FromArgb(43, 43, 43));
            if (Navigation && !Selected) fill = Fluent.Sidebar;
            if (hover && Enabled) fill = Primary ? Color.FromArgb(165, 210, 255) : Color.FromArgb(57, 57, 57);
            Color ink = !Enabled ? Fluent.Muted : Primary ? Color.FromArgb(15, 30, 46) : Fluent.Text;
            if (!Enabled) fill = Color.FromArgb(39, 39, 39);
            Fluent.Box(g, new RectangleF(1, 1, w - 2, h - 2), fill, 6, Primary || Navigation ? fill : Fluent.Border);
            if (Selected) Fluent.Box(g, new RectangleF(1, h / 2 - 8, 3, 16), Fluent.Accent, 1);
            float x = String.IsNullOrEmpty(Symbol) ? 14 : 37;
            if (!String.IsNullOrEmpty(Symbol)) Fluent.Glyph(g, Symbol, 13, h / 2 - 9, 16, ink);
            Fluent.Write(g, Text, x, h / 2 - 10, w - x - 9, 13, ink, Primary);
            if (Focused && ShowFocusCues) using (var p = new Pen(Fluent.Text)) using (var path = Fluent.Round(new RectangleF(4, 4, w - 8, h - 8), 4)) g.DrawPath(p, path);
        }
    }
    internal sealed class DiskCard : FluentSurface
    {
        internal readonly StorageDisk Disk;
        internal bool Expanded;
        readonly FluentButton details;
        internal event EventHandler ExpansionChanged;
        internal DiskCard(StorageDisk disk)
        {
            Disk = disk; Height = Fluent.S(240); Margin = new Padding(0, 0, Fluent.S(14), Fluent.S(14));
            AccessibleRole = AccessibleRole.Grouping;
            AccessibleName = "Disco " + disk.Number + ", " + disk.FriendlyName + (disk.IsOffline ? ", desconectado" : ", en línea");
            AccessibleDescription = String.Join("; ", disk.Volumes.Select(v => v.DriveLetter + ": " + v.Label + ", " + Fluent.Capacity(v.FreeBytes) + " disponibles"));
            details = new FluentButton { Text = "Detalles", Symbol = "\uE70D", Width = Fluent.S(104), Height = Fluent.S(30), AccessibleName = "Mostrar detalles del disco " + disk.Number };
            details.Click += delegate { SetExpanded(!Expanded); if (ExpansionChanged != null) ExpansionChanged(this, EventArgs.Empty); };
            Controls.Add(details); Draw = PaintCard;
        }
        internal void SetExpanded(bool expanded)
        { Expanded = expanded; Height = Fluent.S(expanded ? 332 : 240); details.Text = expanded ? "Ocultar" : "Detalles"; Invalidate(); }
        protected override void OnLayout(LayoutEventArgs e)
        { base.OnLayout(e); if (details != null) details.Location = new Point(Width - Fluent.S(124), Fluent.S(194)); }
        void PaintCard(Graphics g, float w, float h)
        {
            var d = Disk;
            Fluent.Box(g, new RectangleF(1, 1, w - 2, h - 2), Fluent.Card, 9, Fluent.Border);
            Fluent.DiskIcon(g, 20, 20, d.IsOffline ? Fluent.Muted : Fluent.Accent);
            string letters = String.Join(" · ", d.Volumes.Where(v => !String.IsNullOrEmpty(v.DriveLetter)).Select(v => v.DriveLetter + ":"));
            Fluent.Write(g, letters.Length > 0 ? letters : "Disco " + d.Number, 74, 18, w - 96, 23, Fluent.Text, true, 34);
            string labels = String.Join(" · ", d.Volumes.Select(v => v.Label).Where(s => !String.IsNullOrWhiteSpace(s)));
            Fluent.Write(g, String.IsNullOrEmpty(labels) ? "Sin volumen montado" : labels, 74, 48, w - 96, 12, Fluent.Muted);
            Color statusColor = d.Protected ? Fluent.Accent : d.IsOffline ? Fluent.Muted : Fluent.Green;
            Fluent.Box(g, new RectangleF(20, 84, d.Protected ? 100 : 94, 25), Color.FromArgb(42, 49, 46), 5);
            using (var b = new SolidBrush(statusColor)) g.FillEllipse(b, 29, 94, 5, 5);
            Fluent.Write(g, d.Protected ? "Protegido" : d.IsOffline ? "Offline" : "En línea", 41, 87, 76, 12, statusColor);
            Fluent.Write(g, d.BusType + "  /  " + d.PartitionStyle, w - 121, 88, 102, 11, Fluent.Muted);
            long total = d.Volumes.Sum(v => v.TotalBytes), free = d.Volumes.Sum(v => v.FreeBytes);
            double ratio = total > 0 ? Math.Max(0, Math.Min(1, (total - free) / (double)total)) : 0;
            Fluent.Write(g, d.IsOffline ? "Disco desconectado lógicamente" : total > 0 ? Fluent.Capacity(free) + " disponibles" : "Capacidad sin datos", 20, 128, w - 40, 13, Fluent.Text);
            Fluent.Box(g, new RectangleF(20, 156, w - 40, 5), Color.FromArgb(60, 60, 64), 2);
            if (ratio > 0) Fluent.Box(g, new RectangleF(20, 156, Math.Max(5, (float)((w - 40) * ratio)), 5), ratio > .90 ? Color.FromArgb(243, 184, 100) : Fluent.Accent, 2);
            Fluent.Write(g, total > 0 ? (ratio * 100).ToString("0") + "% utilizado de " + Fluent.Capacity(total) : Fluent.Capacity(d.Size) + " de capacidad física", 20, 169, w - 40, 11, Fluent.Muted);
            Fluent.Write(g, "DISCO " + d.Number.ToString("00"), 20, 202, w - 145, 10, Fluent.Muted, true);
            if (Expanded)
            {
                using (var pen = new Pen(Fluent.Border)) g.DrawLine(pen, 20, 239, w - 20, 239);
                Fluent.Write(g, d.FriendlyName, 20, 251, w - 40, 12, Fluent.Text);
                Fluent.Write(g, "Serie  " + d.SerialNumber, 20, 275, w - 40, 11, Fluent.Muted);
                Fluent.Write(g, "Salud  " + d.HealthStatus + "  ·  " + d.OperationalStatus, 20, 298, w - 40, 11, Fluent.Muted);
            }
        }
    }
    internal static class DemoData
    {
        internal static System.Collections.Generic.List<StorageDisk> Disks()
        {
            var result = new System.Collections.Generic.List<StorageDisk>();
            string[] names = { "Archivo", "Proyectos", "Multimedia", "Copias de seguridad", "Biblioteca" };
            for (int i = 0; i < 5; i++)
            {
                var disk = new StorageDisk { Number = i + 2, UniqueId = "DEMO-" + i, SerialNumber = "DEMO 00" + (i + 1), FriendlyName = "JMicron Generic DISK0" + i, BusType = "USB", PartitionStyle = "GPT", HealthStatus = "Healthy", OperationalStatus = "Online", Size = 4000000000000L };
                disk.Volumes.Add(new StorageVolume { DriveLetter = ((char)('O' + i)).ToString(), Label = names[i], FileSystem = "NTFS", TotalBytes = 4000000000000L, FreeBytes = (long)(4000000000000L * new[] { .38, .72, .16, .55, .84 }[i]) });
                result.Add(disk);
            }
            var backup = new StorageDisk { Number = 1, UniqueId = "DEMO-BACKUP", SerialNumber = "DEMO BACKUP", FriendlyName = "TOSHIBA External USB 3.0", BusType = "USB", PartitionStyle = "GPT", HealthStatus = "Healthy", OperationalStatus = "Online", Size = 2000000000000L };
            backup.Volumes.Add(new StorageVolume { DriveLetter = "D", Label = "Backup", FileSystem = "NTFS", TotalBytes = 2000000000000L, FreeBytes = 1200000000000L });
            result.Add(backup); return result;
        }
    }
}
