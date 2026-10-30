using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using VHDDSwitch.Core;

namespace VHDDSwitch.Desktop
{
    internal static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [STAThread] static int Main(string[] args)
        {
            try
            {
                SetProcessDPIAware();
                using (var g = Graphics.FromHwnd(IntPtr.Zero)) Fluent.Scale = g.DpiX / 96f;
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                bool demo = args.Any(a => a.Equals("--demo", StringComparison.OrdinalIgnoreCase));
                var settings = Settings.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json"));
                Application.Run(new Dashboard(settings, demo)); return 0;
            }
            catch (Exception e) { MessageBox.Show(e.Message, "VHDDSwitch", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
        }
    }
    public sealed class Dashboard : Form
    {
        readonly Settings settings; readonly StorageService storage = new StorageService(); readonly bool demo;
        readonly ComboBox profiles = new ComboBox(); readonly FlowLayoutPanel cards = new FlowLayoutPanel();
        readonly TextBox events = new TextBox(); readonly NotifyIcon tray = new NotifyIcon(); readonly Timer timer = new Timer();
        readonly List<FluentButton> actions = new List<FluentButton>(); readonly List<FluentButton> navigation = new List<FluentButton>();
        readonly List<string> history = new List<string>();
        readonly FluentSurface heading = new FluentSurface(), stats = new FluentSurface(), section = new FluentSurface(), activity = new FluentSurface(), footer = new FluentSurface();
        List<StorageDisk> disks = new List<StorageDisk>(); HashSet<string> previous;
        List<StorageDisk> visible = new List<StorageDisk>();
        bool busy, exiting; string lastUpdate = "En espera";
        FluentButton mount, eject;
        public Dashboard(Settings config) : this(config, false) { }
        public Dashboard(Settings config, bool demonstration)
        {
            settings = config; demo = demonstration;
            Text = "VHDDSwitch" + (demo ? " · Demostración" : "");
            AutoScaleMode = AutoScaleMode.None; Font = new Font("Segoe UI", 10);
            BackColor = Fluent.Background; ForeColor = Fluent.Text;
            MinimumSize = new Size(Fluent.S(1000), Fluent.S(740));
            Size = new Size(Math.Min(Fluent.S(1280), Screen.PrimaryScreen.WorkingArea.Width), Math.Min(Fluent.S(1000), Screen.PrimaryScreen.WorkingArea.Height));
            StartPosition = FormStartPosition.CenterScreen; Icon = Fluent.AppIcon(); DoubleBuffered = true;
            profiles.Items.AddRange(settings.profiles.Cast<object>().ToArray()); profiles.SelectedIndex = 0;
            BuildSidebar(); BuildContent();
            profiles.SelectedIndexChanged += delegate { Render(); };
            var menu = new ContextMenuStrip { BackColor = Fluent.Sidebar, ForeColor = Fluent.Text };
            menu.Items.Add("Abrir VHDDSwitch", null, delegate { Restore(); });
            foreach (var p in settings.profiles)
            {
                var profile = p; var item = new ToolStripMenuItem(p.name);
                item.DropDownItems.Add("Montar", null, async delegate { Restore(); profiles.SelectedItem = profile; await Operate(true); });
                item.DropDownItems.Add("Desmontar", null, async delegate { Restore(); profiles.SelectedItem = profile; await Operate(false); });
                menu.Items.Add(item);
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Salir", null, delegate { if (!busy) { exiting = true; Close(); } else Restore(); });
            tray.Icon = Icon; tray.Text = demo ? "VHDDSwitch · Demostración" : "VHDDSwitch"; tray.ContextMenuStrip = menu; tray.Visible = true;
            tray.DoubleClick += delegate { Restore(); };
            Resize += delegate { if (WindowState == FormWindowState.Minimized && settings.app.minimizeToTray) Hide(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (busy) { e.Cancel = true; return; }
                if (!exiting && !demo && settings.app.minimizeToTray && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            };
            timer.Interval = settings.app.refreshIntervalSeconds * 1000;
            timer.Tick += async delegate { await RefreshDisks(); };
            Shown += async delegate
            {
                if (!demo) { await RefreshDisks(); timer.Start(); }
            };
            if (demo) { disks = DemoData.Disks(); lastUpdate = "Datos simulados"; Log("Vista de demostración. No se consulta ni se modifica ningún disco."); }
            Render();
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Fluent.WindowStyle(Handle); }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Dispose(); tray.Dispose(); profiles.Dispose(); events.Dispose(); if (Icon != null) Icon.Dispose(); }
            base.Dispose(disposing);
        }
        FluentButton MakeButton(string text, string icon, int width, EventHandler click, bool primary = false)
        {
            var b = new FluentButton { Text = text, Symbol = icon, Width = Fluent.S(width), Primary = primary, Margin = new Padding(0, 0, Fluent.S(10), 0), AccessibleName = text };
            b.Click += click; actions.Add(b); return b;
        }
        void BuildSidebar()
        {
            var sidebar = new FluentSurface { Dock = DockStyle.Left, Width = Fluent.S(224), BackColor = Fluent.Sidebar, Padding = new Padding(Fluent.S(12), Fluent.S(116), Fluent.S(12), Fluent.S(130)) };
            sidebar.Draw = delegate(Graphics g, float w, float h)
            {
                Fluent.Box(g, new RectangleF(20, 23, 34, 34), Color.FromArgb(48, 63, 80), 9);
                Fluent.Glyph(g, "\uEDA2", 28, 31, 18, Fluent.Accent);
                Fluent.Write(g, "VHDDSwitch", 66, 28, w - 76, 17, Fluent.Text, true);
                Fluent.Write(g, "ALMACENAMIENTO", 22, 89, w - 44, 10, Fluent.Muted, true);
                using (var p = new Pen(Fluent.Border)) g.DrawLine(p, w - 1, 0, w - 1, h);
                Fluent.Box(g, new RectangleF(16, h - 109, w - 32, 86), Color.FromArgb(38, 38, 38), 8, Fluent.Border);
                Fluent.Glyph(g, "\uE73E", 29, h - 96, 16, Fluent.Green);
                Fluent.Write(g, "Control local", 55, h - 97, w - 74, 12, Fluent.Text, true);
                Fluent.Write(g, demo ? "Modo de demostración" : "Protección del sistema activa", 29, h - 69, w - 51, 11, Fluent.Muted);
                Fluent.Write(g, demo ? "Solo datos simulados" : "Windows · .NET Framework", 29, h - 50, w - 51, 10, Fluent.Muted);
            };
            var list = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Fluent.Sidebar };
            foreach (var p in settings.profiles)
            {
                var profile = p;
                string label = p.id == "orico-5bay" ? "Rack " + p.expectedBays + " bahías" : p.name;
                if (p.id == "toshiba-backup") label = "Toshiba Backup";
                var b = new FluentButton { Text = label, Symbol = p.expectedBays > 1 ? "\uE8F1" : "\uEDA2", Navigation = true, Width = Fluent.S(198), Height = Fluent.S(44), Margin = new Padding(0, 0, 0, Fluent.S(6)), Tag = p, AccessibleName = p.name };
                b.Click += delegate { if (!busy) profiles.SelectedItem = profile; };
                navigation.Add(b); list.Controls.Add(b);
            }
            var spacer = new Panel { Height = Fluent.S(30), Width = Fluent.S(190) }; list.Controls.Add(spacer);
            var locks = new FluentButton { Text = "Archivos ocupados", Symbol = "\uE72E", Navigation = true, Width = Fluent.S(198), Height = Fluent.S(42), Margin = new Padding(0, 0, 0, Fluent.S(6)) };
            locks.Click += async delegate { await InspectLock(); }; actions.Add(locks); list.Controls.Add(locks);
            var log = new FluentButton { Text = "Registro de actividad", Symbol = "\uE9D9", Navigation = true, Width = Fluent.S(198), Height = Fluent.S(42) };
            log.Click += delegate { ShowLog(); }; list.Controls.Add(log);
            sidebar.Controls.Add(list); Controls.Add(sidebar);
        }
        void BuildContent()
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(Fluent.S(28), Fluent.S(18), Fluent.S(28), Fluent.S(12)), BackColor = Fluent.Background };
            int[] heights = { 82, 58, 100, 46, 0, 110, 30 };
            for (int i = 0; i < heights.Length; i++) layout.RowStyles.Add(heights[i] == 0 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.Absolute, Fluent.S(heights[i])));
            heading.Dock = DockStyle.Fill; heading.Margin = Padding.Empty;
            heading.Draw = delegate(Graphics g, float w, float h)
            {
                Fluent.Write(g, "Dispositivos  /  " + (demo ? "Demostración" : "Almacenamiento externo"), 0, 0, w, 11, Fluent.Muted);
                var p = (EnclosureProfile)profiles.SelectedItem;
                Fluent.Write(g, p.name, 0, 24, w, 28, Fluent.Text, true, 42);
                Fluent.Write(g, "Tus discos, en un solo lugar.", 0, 64, w, 12, Fluent.Muted);
            };
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0, Fluent.S(8), 0, 0), BackColor = Fluent.Background };
            mount = MakeButton("Montar todos", "\uE8B5", 146, async delegate { await Operate(true); }, true);
            eject = MakeButton("Desmontar todos", "\uE8CD", 169, async delegate { await Operate(false); });
            toolbar.Controls.Add(mount); toolbar.Controls.Add(eject);
            toolbar.Controls.Add(MakeButton("Actualizar", "\uE72C", 127, async delegate { await RefreshDisks(); }));
            stats.Dock = DockStyle.Fill; stats.Margin = new Padding(0, Fluent.S(5), 0, Fluent.S(7)); stats.Draw = PaintStats;
            section.Dock = DockStyle.Fill; section.Margin = Padding.Empty;
            section.Draw = delegate(Graphics g, float w, float h)
            {
                Fluent.Write(g, "Unidades", 0, 15, 160, 16, Fluent.Text, true);
                int online = visible.Count(d => !d.IsOffline);
                string status = visible.Count == 0 ? "Sin discos detectados" : online + " en línea · " + (visible.Count - online) + " offline";
                Fluent.Write(g, status, Math.Max(170, w - 210), 18, 210, 11, Fluent.Muted);
            };
            cards.Dock = DockStyle.Fill; cards.AutoScroll = true; cards.Margin = Padding.Empty; cards.BackColor = Fluent.Background;
            cards.SizeChanged += delegate { SizeCards(); };
            activity.Dock = DockStyle.Fill; activity.Margin = new Padding(0, Fluent.S(8), 0, 0); activity.Draw = PaintActivity;
            var fullLog = new FluentButton { Text = "Ver registro", Symbol = "\uE8A7", Width = Fluent.S(122), Height = Fluent.S(30) };
            fullLog.Click += delegate { ShowLog(); }; activity.Controls.Add(fullLog);
            activity.Resize += delegate { fullLog.Location = new Point(activity.Width - Fluent.S(137), Fluent.S(13)); };
            footer.Dock = DockStyle.Fill; footer.Margin = Padding.Empty;
            footer.Draw = delegate(Graphics g, float w, float h)
            {
                using (var b = new SolidBrush(demo ? Fluent.Accent : Fluent.Green)) g.FillEllipse(b, 1, 14, 5, 5);
                Fluent.Write(g, busy ? "Actualizando…" : demo ? "Demostración · sin acceso a discos reales" : "Actualización automática cada " + settings.app.refreshIntervalSeconds + " s", 14, 8, w - 170, 10, Fluent.Muted);
                Fluent.Write(g, lastUpdate, Math.Max(0, w - 155), 8, 155, 10, Fluent.Muted);
            };
            layout.Controls.Add(heading, 0, 0); layout.Controls.Add(toolbar, 0, 1); layout.Controls.Add(stats, 0, 2); layout.Controls.Add(section, 0, 3); layout.Controls.Add(cards, 0, 4); layout.Controls.Add(activity, 0, 5); layout.Controls.Add(footer, 0, 6);
            Controls.Add(layout); layout.BringToFront();
        }
        void PaintStats(Graphics g, float w, float h)
        {
            float gap = 12, width = (w - gap * 2) / 3;
            long total = visible.Sum(d => d.Volumes.Sum(v => v.TotalBytes)), free = visible.Sum(d => d.Volumes.Sum(v => v.FreeBytes));
            string[] titles = { "DISCOS DETECTADOS", "CAPACIDAD TOTAL", "ESPACIO DISPONIBLE" };
            string[] values = { visible.Count + " / " + ((EnclosureProfile)profiles.SelectedItem).expectedBays, Fluent.Capacity(total), Fluent.Capacity(free) };
            string[] symbols = { "\uEDA2", "\uE8F1", "\uE9F9" };
            for (int i = 0; i < 3; i++)
            {
                float x = i * (width + gap);
                Fluent.Box(g, new RectangleF(x + 1, 1, width - 2, h - 2), Fluent.Card, 8, Fluent.Border);
                Fluent.Write(g, titles[i], x + 17, 16, width - 33, 10, Fluent.Muted, true);
                Fluent.Write(g, values[i], x + 16, 38, width - 60, 25, Fluent.Text, true, 38);
                Fluent.Glyph(g, symbols[i], x + width - 42, 46, 20, Fluent.Accent);
            }
        }
        void PaintActivity(Graphics g, float w, float h)
        {
            Fluent.Box(g, new RectangleF(1, 1, w - 2, h - 2), Fluent.Card, 8, Fluent.Border);
            Fluent.Write(g, "Actividad reciente", 17, 17, w - 170, 13, Fluent.Text, true);
            var recent = history.Skip(Math.Max(0, history.Count - 2)).ToArray();
            if (recent.Length == 0) Fluent.Write(g, "Las operaciones y avisos aparecerán aquí.", 17, 53, w - 36, 12, Fluent.Muted);
            for (int i = 0; i < recent.Length; i++) Fluent.Write(g, recent[i], 17, 48 + i * 21, w - 36, 11, Fluent.Muted);
        }
        void ShowLog()
        {
            using (var dialog = new Form { Text = "Registro de actividad", StartPosition = FormStartPosition.CenterParent, Size = new Size(Fluent.S(820), Fluent.S(480)), BackColor = Fluent.Background, ForeColor = Fluent.Text, ShowInTaskbar = false, MinimizeBox = false })
            using (var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, BorderStyle = BorderStyle.None, Font = new Font("Cascadia Mono", 10), BackColor = Fluent.Background, ForeColor = Fluent.Text, Text = events.Text })
            { dialog.Padding = new Padding(Fluent.S(20)); dialog.Controls.Add(text); dialog.HandleCreated += delegate { Fluent.WindowStyle(dialog.Handle); }; dialog.ShowDialog(this); }
        }
        void Restore() { Show(); WindowState = FormWindowState.Normal; Activate(); }
        void Log(string text)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(Log), text); return; }
            if (events.TextLength > 60000) events.Clear();
            string line = DateTime.Now.ToString("HH:mm:ss") + "   " + text;
            events.AppendText(line + Environment.NewLine); history.Add(line); if (history.Count > 200) history.RemoveAt(0); activity.Invalidate();
        }
        void Busy(bool value)
        {
            busy = value; profiles.Enabled = !value; foreach (var b in navigation) b.Enabled = !value;
            foreach (var b in actions) b.Enabled = !value; if (!value) UpdateActions(); footer.Invalidate();
        }
        void UpdateActions()
        {
            if (mount != null) mount.Enabled = !busy && visible.Any(d => d.IsOffline && !d.Protected);
            if (eject != null) eject.Enabled = !busy && visible.Any(d => !d.IsOffline && !d.Protected);
            if (mount != null && eject != null) { mount.Primary = mount.Enabled; eject.Primary = !mount.Enabled && eject.Enabled; mount.Invalidate(); eject.Invalidate(); }
        }
        async Task RefreshDisks()
        {
            if (busy) return;
            if (demo) { lastUpdate = "Datos simulados"; Render(); Log("Demostración: vista actualizada sin consultar hardware."); return; }
            Busy(true);
            try
            {
                disks = await Task.Run(() => storage.Discover()); lastUpdate = "Actualizado " + DateTime.Now.ToString("HH:mm:ss"); Render();
                var current = new HashSet<string>(disks.Select(d => d.UniqueId));
                if (previous != null)
                    foreach (var disk in disks.Where(d => d.IsOffline && !previous.Contains(d.UniqueId)))
                        if (settings.profiles.Any(p => p.autoMountOnDetect && p.Matches(disk)))
                            try { await Task.Run(() => storage.Change(disk, true, Log)); } catch (Exception e) { Log("Auto-montaje: " + e.Message); }
                previous = current;
            }
            catch (Exception e) { disks.Clear(); lastUpdate = "No se pudo actualizar"; Render(); Log("Error de detección: " + e.Message); }
            finally { Busy(false); }
        }
        void Render()
        {
            if (profiles.SelectedItem == null) return;
            var p = (EnclosureProfile)profiles.SelectedItem; visible = disks.Where(p.Matches).OrderBy(d => d.Number).ToList();
            foreach (var b in navigation) { b.Selected = b.Tag == p; b.Invalidate(); }
            // Keep expanded details and scroll position during telemetry polling.
            var expanded = new HashSet<string>(cards.Controls.OfType<DiskCard>().Where(c => c.Expanded).Select(c => c.Disk.UniqueId));
            var scroll = cards.AutoScrollPosition;
            cards.SuspendLayout(); foreach (Control c in cards.Controls.Cast<Control>().ToArray()) c.Dispose();
            foreach (var d in visible)
            {
                var card = new DiskCard(d); if (expanded.Contains(d.UniqueId)) card.SetExpanded(true);
                card.ExpansionChanged += delegate { cards.PerformLayout(); }; cards.Controls.Add(card);
            }
            if (visible.Count == 0)
            {
                var empty = new FluentSurface { Height = Fluent.S(230), Width = Math.Max(100, cards.ClientSize.Width - Fluent.S(20)), Margin = Padding.Empty, AccessibleName = "No hay discos coincidentes" };
                empty.Draw = delegate(Graphics g, float w, float h)
                {
                    Fluent.Box(g, new RectangleF(1, 1, w - 2, h - 2), Fluent.Card, 9, Fluent.Border);
                    Fluent.DiskIcon(g, w / 2 - 21, 31, Fluent.Muted);
                    Fluent.Write(g, "Sin unidades para este perfil", 24, 93, w - 48, 20, Fluent.Text, true);
                    Fluent.Write(g, "Conecta el dispositivo o revisa el filtro de hardware en settings.json.", 24, 132, w - 48, 12, Fluent.Muted);
                    Fluent.Write(g, "Filtro actual: " + p.hardwareFilter, 24, 166, w - 48, 11, Fluent.Muted);
                };
                cards.Controls.Add(empty);
            }
            SizeCards(); cards.ResumeLayout(); cards.AutoScrollPosition = new Point(-scroll.X, -scroll.Y);
            heading.Invalidate(); stats.Invalidate(); section.Invalidate(); footer.Invalidate(); UpdateActions();
        }
        void SizeCards()
        {
            int width = cards.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - Fluent.S(2);
            int columns = Math.Max(1, Math.Min(3, width / Fluent.S(268)));
            int cardWidth = Math.Max(Fluent.S(240), width / columns - Fluent.S(14));
            foreach (Control card in cards.Controls) card.Width = cardWidth;
        }
        async Task Operate(bool online)
        {
            if (busy) return;
            if (demo) { foreach (var d in disks.Where(((EnclosureProfile)profiles.SelectedItem).Matches)) { d.IsOffline = !online; d.OperationalStatus = online ? "Online" : "Offline"; } Log("Demostración: estado simulado actualizado."); Render(); return; }
            var p = (EnclosureProfile)profiles.SelectedItem;
            if (MessageBox.Show(this, (online ? "Montar" : "Vaciar cache y desmontar") + " los discos externos de " + p.name + "?", "Confirmar operacion", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            Busy(true); int failures = 0;
            try
            {
                var targets = (await Task.Run(() => storage.Discover())).Where(p.Matches).ToList();
                if (targets.Count == 0) { Log("No hay discos coincidentes."); return; }
                foreach (var disk in targets)
                    try { await Task.Run(() => storage.Change(disk, online, Log)); }
                    catch (Exception e) { failures++; Log("Disco " + disk.Number + ": ERROR: " + e.Message); }
                Log("Operacion terminada: " + (targets.Count - failures) + " correctos; " + failures + " errores.");
                if (failures > 0) MessageBox.Show(this, "Hay discos sin completar. Consulte el registro y cierre los archivos abiertos antes de reintentar.", "Operacion incompleta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception e) { Log(e.Message); }
            finally { Busy(false); }
            await RefreshDisks();
        }
        async Task InspectLock()
        {
            if (busy) return;

            if (demo) { Log("Demostración: la consulta de archivos está desactivada."); return; }
            using (var picker = new OpenFileDialog { Title = "Seleccione el archivo ocupado (Restart Manager consulta archivos concretos)" })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                Busy(true);
                try
                {
                    var locks = await Task.Run(() => LockResolver.Find(picker.FileName));
                    if (locks.Length == 0) MessageBox.Show(this, "No se detectaron procesos para este archivo. Esto no prueba que el volumen este libre.");
                    foreach (var entry in locks)
                    {
                        Log("Archivo ocupado por " + entry.Name + " (PID " + entry.Process.ProcessId + ")");
                        if (MessageBox.Show(this, entry.Name + " (PID " + entry.Process.ProcessId + ") usa el archivo. Solicitar cierre normal? Guarde primero su trabajo.", "Proceso detectado", MessageBoxButtons.YesNo) != DialogResult.Yes) continue;
                        using (var proc = Process.GetProcessById(entry.Process.ProcessId))
                        {
                            long stamp = ((long)(uint)entry.Process.StartTime.dwHighDateTime << 32) | (uint)entry.Process.StartTime.dwLowDateTime;
                            if (proc.StartTime.ToUniversalTime().ToFileTimeUtc() != stamp) throw new IOException("El PID fue reutilizado; no se cerrara.");
                            Log(proc.CloseMainWindow() ? "Cierre solicitado; espere y reintente el desmontaje." : "Sin ventana principal; cierre la aplicacion manualmente.");
                        }
                    }
                }
                catch (Exception e) { Log("Restart Manager: " + e.Message); }
                finally { Busy(false); }
            }
        }
    }
}
