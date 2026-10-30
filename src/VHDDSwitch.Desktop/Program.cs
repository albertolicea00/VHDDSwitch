using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using Timer = System.Windows.Forms.Timer;
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
        readonly IStorageTelemetryReader telemetry;
        readonly bool injectedReader;
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        readonly Timer animation = new Timer();
        readonly ComboBox profiles = new ComboBox(); readonly FlowLayoutPanel cards = new FlowLayoutPanel();
        readonly TextBox events = new TextBox(); readonly NotifyIcon tray = new NotifyIcon(); readonly Timer timer = new Timer();
        readonly List<FluentButton> actions = new List<FluentButton>(); readonly List<FluentButton> navigation = new List<FluentButton>();
        readonly List<string> history = new List<string>();
        readonly FluentSurface heading = new FluentSurface(), stats = new FluentSurface(), section = new FluentSurface(), activity = new FluentSurface(), footer = new FluentSurface();
        List<StorageDisk> disks = new List<StorageDisk>(); HashSet<string> previous;
        List<StorageDisk> visible = new List<StorageDisk>();
        bool busy, exiting, refreshing, discoveryPending, inventoryVerified;
        string inventoryError, lastUpdate = "Waiting";
        FluentButton mount, eject, refresh;
        public Dashboard(Settings config) : this(config, false) { }
        public Dashboard(Settings config, bool demonstration) : this(config, demonstration, null) { }
        public Dashboard(Settings config, bool demonstration, IStorageTelemetryReader reader)
        {
            settings = config; demo = demonstration;
            telemetry = reader ?? new StorageTelemetryReader(); injectedReader = reader != null;
            discoveryPending = !demo; inventoryVerified = demo;
            Text = "VHDDSwitch" + (demo ? " · Demo" : "");
            AutoScaleMode = AutoScaleMode.None; Font = new Font("Segoe UI", 10);
            BackColor = Fluent.Background; ForeColor = Fluent.Text;
            MinimumSize = new Size(Fluent.S(1000), Fluent.S(740));
            Size = new Size(Math.Min(Fluent.S(1280), Screen.PrimaryScreen.WorkingArea.Width), Math.Min(Fluent.S(1000), Screen.PrimaryScreen.WorkingArea.Height));
            StartPosition = FormStartPosition.CenterScreen; Icon = Fluent.AppIcon(); DoubleBuffered = true;
            profiles.Items.AddRange(settings.profiles.Cast<object>().ToArray()); profiles.SelectedIndex = 0;
            BuildSidebar(); BuildContent();
            profiles.SelectedIndexChanged += delegate { Render(); };
            var menu = new ContextMenuStrip { BackColor = Fluent.Sidebar, ForeColor = Fluent.Text };
            menu.Items.Add("Open VHDDSwitch", null, delegate { Restore(); });
            foreach (var p in settings.profiles)
            {
                var profile = p; var item = new ToolStripMenuItem(p.name);
                item.DropDownItems.Add("Mount", null, async delegate { Restore(); profiles.SelectedItem = profile; await Operate(true); });
                item.DropDownItems.Add("Dismount", null, async delegate { Restore(); profiles.SelectedItem = profile; await Operate(false); });
                menu.Items.Add(item);
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { if (!busy) { exiting = true; Close(); } else Restore(); });
            tray.Icon = Icon; tray.Text = demo ? "VHDDSwitch · Demo" : "VHDDSwitch"; tray.ContextMenuStrip = menu; tray.Visible = true;
            tray.DoubleClick += delegate { Restore(); };
            Resize += delegate { if (WindowState == FormWindowState.Minimized && settings.app.minimizeToTray) Hide(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (busy) { e.Cancel = true; return; }
                if (!exiting && !demo && settings.app.minimizeToTray && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            };
            timer.Interval = settings.app.refreshIntervalSeconds * 1000;
            timer.Tick += async delegate { await RefreshDisks(); };
            animation.Interval = 80;
            animation.Tick += delegate { if (!Visible) return; stats.Invalidate(); foreach (Control card in cards.Controls) card.Invalidate(); };
            Shown += async delegate
            {
                if (!demo) { await RefreshDisks(); if (!IsDisposed) timer.Start(); }
            };
            if (demo) { disks = DemoData.Disks(); lastUpdate = "Simulated data"; Log("Demo view. No real disks are queried or modified."); }
            Render();
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Fluent.WindowStyle(Handle); }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { lifetime.Cancel(); animation.Dispose(); timer.Dispose(); tray.Dispose(); profiles.Dispose(); events.Dispose(); if (Icon != null) Icon.Dispose(); }
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
                Fluent.Write(g, "STORAGE", 22, 89, w - 44, 10, Fluent.Muted, true);
                using (var p = new Pen(Fluent.Border)) g.DrawLine(p, w - 1, 0, w - 1, h);
                Fluent.Box(g, new RectangleF(16, h - 109, w - 32, 86), Color.FromArgb(38, 38, 38), 8, Fluent.Border);
                Fluent.Glyph(g, "\uE73E", 29, h - 96, 16, Fluent.Green);
                Fluent.Write(g, "Local control", 55, h - 97, w - 74, 12, Fluent.Text, true);
                Fluent.Write(g, demo ? "Demo mode" : "System drive protection enabled", 29, h - 69, w - 51, 11, Fluent.Muted);
                Fluent.Write(g, demo ? "Simulated data only" : "Windows · .NET Framework", 29, h - 50, w - 51, 10, Fluent.Muted);
            };
            var list = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Fluent.Sidebar };
            foreach (var p in settings.profiles)
            {
                var profile = p;
                string label = p.id == "orico-5bay" ? "Rack " + p.expectedBays + " bays" : p.name;
                if (p.id == "toshiba-backup") label = "Toshiba Backup";
                var b = new FluentButton { Text = label, Symbol = p.expectedBays > 1 ? "\uE8F1" : "\uEDA2", Navigation = true, Width = Fluent.S(198), Height = Fluent.S(44), Margin = new Padding(0, 0, 0, Fluent.S(6)), Tag = p, AccessibleName = p.name };
                b.Click += delegate { if (!busy) profiles.SelectedItem = profile; };
                navigation.Add(b); list.Controls.Add(b);
            }
            var spacer = new Panel { Height = Fluent.S(30), Width = Fluent.S(190) }; list.Controls.Add(spacer);
            var locks = new FluentButton { Text = "File locks", Symbol = "\uE72E", Navigation = true, Width = Fluent.S(198), Height = Fluent.S(42), Margin = new Padding(0, 0, 0, Fluent.S(6)) };
            locks.Click += async delegate { await InspectLock(); }; actions.Add(locks); list.Controls.Add(locks);
            var log = new FluentButton { Text = "Activity log", Symbol = "\uE9D9", Navigation = true, Width = Fluent.S(198), Height = Fluent.S(42) };
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
                Fluent.Write(g, "Devices  /  " + (demo ? "Demo" : "External storage"), 0, 0, w, 11, Fluent.Muted);
                var p = (EnclosureProfile)profiles.SelectedItem;
                Fluent.Write(g, p.name, 0, 24, w, 28, Fluent.Text, true, 42);
                Fluent.Write(g, "Your drives, all in one place.", 0, 64, w, 12, Fluent.Muted);
            };
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0, Fluent.S(8), 0, 0), BackColor = Fluent.Background };
            mount = MakeButton("Mount all", "\uE8B5", 146, async delegate { await Operate(true); }, true);
            eject = MakeButton("Dismount all", "\uE8CD", 169, async delegate { await Operate(false); });
            toolbar.Controls.Add(mount); toolbar.Controls.Add(eject);
            refresh = MakeButton("Refresh", "\uE72C", 127, async delegate { await RefreshDisks(); }); toolbar.Controls.Add(refresh);
            stats.Dock = DockStyle.Fill; stats.Margin = new Padding(0, Fluent.S(5), 0, Fluent.S(7)); stats.Draw = PaintStats;
            section.Dock = DockStyle.Fill; section.Margin = Padding.Empty;
            section.Draw = delegate(Graphics g, float w, float h)
            {
                Fluent.Write(g, "Drives", 0, 15, 160, 16, Fluent.Text, true);
                int online = visible.Count(d => !d.IsOffline);
                string status = discoveryPending ? "Discovering drives…" : inventoryError != null ? "Refresh failed" : visible.Count == 0 ? "No disks detected" : online + " online · " + (visible.Count - online) + " offline";
                Fluent.Write(g, status, Math.Max(170, w - 210), 18, 210, 11, Fluent.Muted);
            };
            cards.Dock = DockStyle.Fill; cards.AutoScroll = true; cards.Margin = Padding.Empty; cards.BackColor = Fluent.Background;
            cards.SizeChanged += delegate { SizeCards(); };
            activity.Dock = DockStyle.Fill; activity.Margin = new Padding(0, Fluent.S(8), 0, 0); activity.ChildBackground = Fluent.Card; activity.Draw = PaintActivity;
            var fullLog = new FluentButton { Text = "View log", Symbol = "\uE8A7", Width = Fluent.S(122), Height = Fluent.S(30) };
            fullLog.Click += delegate { ShowLog(); }; activity.Controls.Add(fullLog);
            activity.Resize += delegate { fullLog.Location = new Point(activity.Width - Fluent.S(137), Fluent.S(13)); };
            footer.Dock = DockStyle.Fill; footer.Margin = Padding.Empty;
            footer.Draw = delegate(Graphics g, float w, float h)
            {
                using (var b = new SolidBrush(demo ? Fluent.Accent : Fluent.Green)) g.FillEllipse(b, 1, 14, 5, 5);
                Fluent.Write(g, busy ? "Operation in progress…" : refreshing ? discoveryPending ? "Discovering drives…" : "Loading drive details…" : demo ? "Demo · no access to real disks" : "Automatic refresh every " + settings.app.refreshIntervalSeconds + " s", 14, 8, w - 170, 10, Fluent.Muted);
                Fluent.Write(g, lastUpdate, Math.Max(0, w - 155), 8, 155, 10, Fluent.Muted);
            };
            layout.Controls.Add(heading, 0, 0); layout.Controls.Add(toolbar, 0, 1); layout.Controls.Add(stats, 0, 2); layout.Controls.Add(section, 0, 3); layout.Controls.Add(cards, 0, 4); layout.Controls.Add(activity, 0, 5); layout.Controls.Add(footer, 0, 6);
            Controls.Add(layout); layout.BringToFront();
        }
        void PaintStats(Graphics g, float w, float h)
        {
            float gap = 12, width = (w - gap * 2) / 3;
            long total = visible.Sum(d => d.Volumes.Sum(v => v.TotalBytes)), free = visible.Sum(d => d.Volumes.Sum(v => v.FreeBytes));
            string[] titles = { "DISKS DETECTED", "TOTAL CAPACITY", "AVAILABLE SPACE" };
            string[] values = { visible.Count + " / " + ((EnclosureProfile)profiles.SelectedItem).expectedBays, Fluent.Capacity(total), Fluent.Capacity(free) };
            string[] symbols = { "\uEDA2", "\uE8F1", "\uE9F9" };
            for (int i = 0; i < 3; i++)
            {
                float x = i * (width + gap);
                Fluent.Box(g, new RectangleF(x + 1, 1, width - 2, h - 2), Fluent.Card, 8, Fluent.Border);
                Fluent.Write(g, titles[i], x + 17, 16, width - 33, 10, Fluent.Muted, true);
                bool pending = discoveryPending || (i > 0 && visible.Any(d => !d.HasTelemetry && d.TelemetryState == TelemetryState.Loading));
                if (pending) Fluent.Skeleton(g, x + 17, 48, Math.Max(30, width - 88), 23);
                else Fluent.Write(g, i > 0 && visible.Any(d => !d.HasTelemetry) ? "—" : inventoryError != null && visible.Count == 0 ? "—" : values[i], x + 16, 38, width - 60, 25, Fluent.Text, true, 38);
                Fluent.Glyph(g, symbols[i], x + width - 42, 46, 20, Fluent.Accent);
            }
        }
        void PaintActivity(Graphics g, float w, float h)
        {
            Fluent.Box(g, new RectangleF(1, 1, w - 2, h - 2), Fluent.Card, 8, Fluent.Border);
            Fluent.Write(g, "Recent activity", 17, 17, w - 170, 13, Fluent.Text, true);
            var recent = history.Skip(Math.Max(0, history.Count - 2)).ToArray();
            if (recent.Length == 0) Fluent.Write(g, "Operations and notices will appear here.", 17, 53, w - 36, 12, Fluent.Muted);
            for (int i = 0; i < recent.Length; i++) Fluent.Write(g, recent[i], 17, 48 + i * 21, w - 36, 11, Fluent.Muted);
        }
        void ShowLog()
        {
            using (var dialog = new Form { Text = "Activity log", StartPosition = FormStartPosition.CenterParent, Size = new Size(Fluent.S(820), Fluent.S(480)), BackColor = Fluent.Background, ForeColor = Fluent.Text, ShowInTaskbar = false, MinimizeBox = false })
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
            bool ready = !busy && !refreshing && inventoryVerified && visible.Count > 0 && visible.All(d => d.TelemetryState == TelemetryState.Ready);
            if (mount != null) mount.Enabled = ready && visible.Any(d => d.IsOffline && !d.Protected);
            if (eject != null) eject.Enabled = ready && visible.Any(d => !d.IsOffline && !d.Protected);
            if (refresh != null) refresh.Enabled = !busy && !refreshing;
            if (mount != null && eject != null) { mount.Primary = mount.Enabled; eject.Primary = !mount.Enabled && eject.Enabled; mount.Invalidate(); eject.Invalidate(); }
        }
        static string DiskKey(StorageDisk d) { return d.Number + "|" + d.UniqueId + "|" + d.SerialNumber; }
        async Task RefreshDisks()
        {
            if (busy || refreshing || lifetime.IsCancellationRequested) return;
            if (demo) { lastUpdate = "Simulated data"; Render(); Log("Demo: refreshed the view without querying hardware."); return; }
            var token = lifetime.Token;
            refreshing = true; inventoryError = null; lastUpdate = "Refreshing…"; UpdateActions(); footer.Invalidate();
            try
            {
                var fresh = await Task.Run(() => telemetry.ReadDisks(), token);
                if (token.IsCancellationRequested || IsDisposed) return;
                foreach (var disk in fresh)
                {
                    var cached = disks.FirstOrDefault(old => !String.IsNullOrWhiteSpace(disk.UniqueId) && DiskKey(old) == DiskKey(disk) && old.IsOffline == disk.IsOffline && old.HasTelemetry);
                    disk.TelemetryState = TelemetryState.Loading; disk.HasTelemetry = cached != null;
                    if (cached != null) disk.Volumes = cached.Volumes;
                }
                disks = fresh; discoveryPending = false; inventoryVerified = true;
                Render(); // Publish disk identities before starting slow filesystem queries.
                var candidates = disks.Where(d => settings.profiles.Any(p => p.Matches(d))).ToList();
                using (var concurrency = new SemaphoreSlim(2))
                    await Task.WhenAll(candidates.Select(d => LoadTelemetry(d, concurrency, token)));
                if (token.IsCancellationRequested || IsDisposed) return;
                var current = new HashSet<string>(disks.Select(d => d.UniqueId));
                if (previous != null && !injectedReader)
                    foreach (var disk in disks.Where(d => d.IsOffline && d.TelemetryState == TelemetryState.Ready && !previous.Contains(d.UniqueId)))
                        if (settings.profiles.Any(p => p.autoMountOnDetect && p.Matches(disk)))
                        {
                            Busy(true);
                            try { await Task.Run(() => storage.Change(disk, true, Log)); }
                            catch (Exception e) { Log("Auto-mount: " + e.Message); }
                            finally { if (!token.IsCancellationRequested) Busy(false); }
                        }
                previous = current; lastUpdate = "Updated " + DateTime.Now.ToString("HH:mm:ss");
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                if (token.IsCancellationRequested || IsDisposed) return;
                discoveryPending = false; inventoryVerified = false; inventoryError = e.Message; lastUpdate = "Refresh failed";
                // A transient provider error must not erase previously displayed data.
                Log("Discovery error: " + e.Message);
            }
            finally
            {
                refreshing = false;
                if (!token.IsCancellationRequested && !IsDisposed) Render();
            }
        }
        async Task LoadTelemetry(StorageDisk disk, SemaphoreSlim concurrency, CancellationToken token)
        {
            await concurrency.WaitAsync(token);
            try
            {
                var details = await Task.Run(() => telemetry.ReadDetails(disk), token);
                if (token.IsCancellationRequested || IsDisposed) return;
                if (details == null || DiskKey(details) != DiskKey(disk)) throw new IOException("The disk identity changed while loading details.");
                disk.Volumes = details.Volumes; disk.Protected |= details.Protected;
                disk.HasTelemetry = true; disk.TelemetryState = TelemetryState.Ready; disk.TelemetryError = null;
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e)
            {
                if (token.IsCancellationRequested || IsDisposed) return;
                disk.TelemetryState = TelemetryState.Error; disk.TelemetryError = e.Message;
                Log("Disk " + disk.Number + " details: " + e.Message);
            }
            finally { concurrency.Release(); }
            if (!token.IsCancellationRequested && !IsDisposed) Render();
        }
        void Render()
        {
            if (profiles.SelectedItem == null || IsDisposed) return;
            var p = (EnclosureProfile)profiles.SelectedItem; visible = disks.Where(p.Matches).OrderBy(d => d.Number).ToList();
            foreach (var b in navigation) { b.Selected = b.Tag == p; b.Invalidate(); }
            var scroll = cards.AutoScrollPosition;
            cards.SuspendLayout();
            // Reuse cards so telemetry updates preserve focus, expansion and scroll.
            var existing = cards.Controls.OfType<DiskCard>().ToDictionary(c => DiskKey(c.Disk));
            var wanted = new HashSet<string>(visible.Select(DiskKey));
            foreach (Control control in cards.Controls.Cast<Control>().ToArray())
            {
                var card = control as DiskCard;
                if (card == null || !wanted.Contains(DiskKey(card.Disk))) { cards.Controls.Remove(control); control.Dispose(); }
            }
            int index = 0;
            foreach (var d in visible)
            {
                DiskCard card;
                if (!existing.TryGetValue(DiskKey(d), out card))
                {
                    card = new DiskCard(d); card.ExpansionChanged += delegate { cards.PerformLayout(); }; cards.Controls.Add(card);
                }
                else card.UpdateDisk(d);
                cards.Controls.SetChildIndex(card, index++);
            }
            if (visible.Count == 0 && discoveryPending)
            {
                for (int i = 0; i < Math.Min(p.expectedBays, 12); i++) cards.Controls.Add(new SkeletonCard());
            }
            else if (visible.Count == 0)
            {
                var empty = new FluentSurface { Height = Fluent.S(230), Margin = Padding.Empty, AccessibleName = inventoryError != null ? "Discovery failed" : "No matching disks" };
                empty.Draw = delegate(Graphics g, float w, float h)
                {
                    Fluent.Box(g, new RectangleF(1, 1, w - 2, h - 2), Fluent.Card, 9, Fluent.Border);
                    Fluent.DiskIcon(g, w / 2 - 21, 31, Fluent.Muted);
                    Fluent.Write(g, inventoryError != null ? "Unable to load your drives" : "No drives match this profile", 24, 93, w - 48, 20, Fluent.Text, true);
                    Fluent.Write(g, inventoryError != null ? "Select Refresh to retry. See the activity log for details." : "Connect a device or check the hardware filter in settings.json.", 24, 132, w - 48, 12, Fluent.Muted);
                    Fluent.Write(g, "Current filter: " + p.hardwareFilter, 24, 166, w - 48, 11, Fluent.Muted);
                };
                cards.Controls.Add(empty);
            }
            SizeCards(); cards.ResumeLayout(); cards.AutoScrollPosition = new Point(-scroll.X, -scroll.Y);
            bool loading = discoveryPending || visible.Any(d => !d.HasTelemetry && d.TelemetryState == TelemetryState.Loading);
            if (loading) animation.Start(); else animation.Stop();
            heading.Invalidate(); stats.Invalidate(); section.Invalidate(); footer.Invalidate(); UpdateActions();
        }
        void SizeCards()
        {
            int width = cards.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - Fluent.S(2);
            int columns = Math.Max(1, Math.Min(3, width / Fluent.S(268)));
            int cardWidth = Math.Max(Fluent.S(240), width / columns - Fluent.S(14));
            foreach (Control card in cards.Controls) card.Width = card is DiskCard || card is SkeletonCard ? cardWidth : Math.Max(100, width);
        }
        async Task Operate(bool online)
        {
            if (busy) return;
            if (!demo && (injectedReader || refreshing || !inventoryVerified || visible.Any(d => d.TelemetryState != TelemetryState.Ready))) { Log("Wait for verified disk details before running an operation."); return; }
            if (demo) { foreach (var d in disks.Where(((EnclosureProfile)profiles.SelectedItem).Matches)) { d.IsOffline = !online; d.OperationalStatus = online ? "Online" : "Offline"; } Log("Demo: simulated state updated."); Render(); return; }
            var p = (EnclosureProfile)profiles.SelectedItem;
            if (MessageBox.Show(this, (online ? "Mount" : "Flush caches and dismount") + " the external disks in " + p.name + "?", "Confirm operation", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            Busy(true); int failures = 0;
            try
            {
                var targets = (await Task.Run(() => storage.Discover())).Where(p.Matches).ToList();
                if (targets.Count == 0) { Log("No matching disks found."); return; }
                foreach (var disk in targets)
                    try { await Task.Run(() => storage.Change(disk, online, Log)); }
                    catch (Exception e) { failures++; Log("Disk " + disk.Number + ": ERROR: " + e.Message); }
                Log("Operation finished: " + (targets.Count - failures) + " succeeded; " + failures + " errors.");
                if (failures > 0) MessageBox.Show(this, "Some disks failed. Check the log and close open files before retrying.", "Incomplete operation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception e) { Log(e.Message); }
            finally { Busy(false); }
            await RefreshDisks();
        }
        async Task InspectLock()
        {
            if (busy) return;

            if (demo) { Log("Demo: file lock queries are disabled."); return; }
            using (var picker = new OpenFileDialog { Title = "Select the busy file to inspect with Restart Manager" })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                Busy(true);
                try
                {
                    var locks = await Task.Run(() => LockResolver.Find(picker.FileName));
                    if (locks.Length == 0) MessageBox.Show(this, "No processes found for this file. This does not prove the volume is free.");
                    foreach (var entry in locks)
                    {
                        Log("File held by " + entry.Name + " (PID " + entry.Process.ProcessId + ")");
                        if (MessageBox.Show(this, entry.Name + " (PID " + entry.Process.ProcessId + ") is using the file. Request normal close? Save your work first.", "Process detected", MessageBoxButtons.YesNo) != DialogResult.Yes) continue;
                        using (var proc = Process.GetProcessById(entry.Process.ProcessId))
                        {
                            long stamp = ((long)(uint)entry.Process.StartTime.dwHighDateTime << 32) | (uint)entry.Process.StartTime.dwLowDateTime;
                            if (proc.StartTime.ToUniversalTime().ToFileTimeUtc() != stamp) throw new IOException("Process ID reused; closing was rejected.");
                            Log(proc.CloseMainWindow() ? "Close requested; wait and retry dismounting." : "No main window; close the application manually.");
                        }
                    }
                }
                catch (Exception e) { Log("Restart Manager: " + e.Message); }
                finally { Busy(false); }
            }
        }
    }
}
