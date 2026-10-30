using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using VHDDSwitch.Core;
using VHDDSwitch.Desktop;

// This entry point ALWAYS uses simulated data. No discovery timer is started.
internal static class DesktopPreview
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [STAThread] static void Main(string[] args)
    {
        SetProcessDPIAware();
        using (var g = Graphics.FromHwnd(IntPtr.Zero)) Fluent.Scale = g.DpiX / 96f;
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
        var settings = Settings.Load(Path.Combine(root, "settings.json"));
        if (args.Length > 0 && args[0] == "--render")
        {
            using (var form = new Dashboard(settings, true))
            {
                form.Size = new Size(Fluent.S(1280), Fluent.S(1000));
                var handle = form.Handle; form.CreateControl(); form.PerformLayout();
                Render(form, Path.Combine(root, "build", "preview-win11.png"));
                form.Size = new Size(Fluent.S(1000), Fluent.S(740)); form.PerformLayout();
                Render(form, Path.Combine(root, "build", "preview-compact.png"));
                VerifyDemo(form, root);
                Console.WriteLine("Rendered simulated layouts; 8 UI checks passed. No storage discovery or mutations.");
            }
            return;
        }
        Application.Run(new Dashboard(settings, true));
    }
    static void VerifyDemo(Dashboard form, string root)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var type = typeof(Dashboard);
        var timer = (Timer)type.GetField("timer", flags).GetValue(form);
        var cards = (FlowLayoutPanel)type.GetField("cards", flags).GetValue(form);
        var selector = (ComboBox)type.GetField("profiles", flags).GetValue(form);
        Check(!timer.Enabled, "demo must never start polling");
        Check(cards.Controls.OfType<DiskCard>().Count() == 5, "five simulated rack disks");
        Check(cards.Controls.Cast<Control>().All(c => c.Right <= cards.ClientSize.Width), "responsive cards stay inside viewport");
        ((Task)type.GetMethod("Operate", flags).Invoke(form, new object[] { false })).GetAwaiter().GetResult();
        Check(cards.Controls.OfType<DiskCard>().All(c => c.Disk.IsOffline), "eject changes simulated state only");
        ((Task)type.GetMethod("Operate", flags).Invoke(form, new object[] { true })).GetAwaiter().GetResult();
        Check(cards.Controls.OfType<DiskCard>().All(c => !c.Disk.IsOffline), "mount restores simulated state");
        cards.Controls.OfType<DiskCard>().First().SetExpanded(true);
        ((Task)type.GetMethod("RefreshDisks", flags).Invoke(form, null)).GetAwaiter().GetResult();
        Check(cards.Controls.OfType<DiskCard>().First().Expanded, "expanded details survive refresh");
        if (selector.Items.Count > 1) selector.SelectedIndex = 1;
        Check(cards.Controls.OfType<DiskCard>().Count() == 1, "profile navigation isolates backup");
        Render(form, Path.Combine(root, "build", "preview-backup.png"));
        var profile = (EnclosureProfile)selector.SelectedItem; string filter = profile.hardwareFilter;
        profile.hardwareFilter = "NO-MATCH-DEMO"; type.GetMethod("Render", flags).Invoke(form, null);
        Check(cards.Controls.OfType<DiskCard>().Count() == 0 && cards.Controls.Count == 1, "empty state");
        Render(form, Path.Combine(root, "build", "preview-empty.png")); profile.hardwareFilter = filter;
    }
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Render(Form form, string path)
    {
        Layout(form);
        // Render our own controls without showing a window or querying storage.
        // DrawToBitmap skips invisible descendants when a Form is never shown.
        using (var image = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
        using (var graphics = Graphics.FromImage(image))
        { PaintTree(form, graphics); image.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
    }
    static void PaintTree(Control control, Graphics graphics)
    {
        using (var brush = new SolidBrush(control.BackColor)) graphics.FillRectangle(brush, control.ClientRectangle);
        if (control is FluentSurface || control is FluentButton)
        {
            var state = graphics.Save();
            var paint = control.GetType().GetMethod("OnPaint", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            paint.Invoke(control, new object[] { new PaintEventArgs(graphics, control.ClientRectangle) });
            graphics.Restore(state);
        }
        // Controls are painted back-to-front, matching WinForms z-order.
        for (int i = control.Controls.Count - 1; i >= 0; i--)
        {
            var child = control.Controls[i]; if (child.Width < 1 || child.Height < 1) continue;
            var state = graphics.Save(); graphics.TranslateTransform(child.Left, child.Top);
            graphics.SetClip(child.ClientRectangle, System.Drawing.Drawing2D.CombineMode.Intersect);
            PaintTree(child, graphics); graphics.Restore(state);
        }
    }
    static void Layout(Control control)
    { control.CreateControl(); control.PerformLayout(); foreach (Control child in control.Controls) Layout(child); }
}
