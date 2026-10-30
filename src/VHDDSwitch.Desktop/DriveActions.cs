using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using VHDDSwitch.Core;

namespace VHDDSwitch.Desktop
{
    public sealed partial class Dashboard
    {
        readonly HashSet<string> keepActive = new HashSet<string>();
        readonly Timer keepActiveTimer = new Timer { Interval = 30000 };
        bool keepActiveWorking;
        void SetupDriveActions()
        {
            keepActiveTimer.Tick += async delegate
            {
                if (demo || injectedReader || busy || refreshing || keepActiveWorking || keepActive.Count == 0) return;
                keepActiveWorking = true;
                try
                {
                    foreach (var d in disks.Where(d => keepActive.Contains(EnclosureProfile.Identity(d)) && !d.IsOffline && !d.Protected).ToArray())
                    {
                        if (lifetime.IsCancellationRequested) return;
                        try { await Task.Run(() => storage.KeepActive(d)); }
                        catch (Exception e) { keepActive.Remove(EnclosureProfile.Identity(d)); Log("Keep active stopped: " + e.Message); }
                    }
                }
                finally { keepActiveWorking = false; }
            };
            keepActiveTimer.Start();
        }
        void ToggleActive(IEnumerable<StorageDisk> targets)
        {
            var selected = targets.Where(d => !d.Protected && !d.IsOffline).ToArray();
            bool disable = selected.Length > 0 && selected.All(d => keepActive.Contains(EnclosureProfile.Identity(d)));
            foreach (var disk in selected) { if (disable) keepActive.Remove(EnclosureProfile.Identity(disk)); else keepActive.Add(EnclosureProfile.Identity(disk)); }
            Log(disable ? "Keep active disabled." : "Keep active enabled while this app is running. Hardware may still enforce its own sleep policy.");
        }
        void ShowDiskActions(DiskCard card)
        {
            var d = card.Disk; var menu = new ContextMenuStrip();
            bool available = !busy && !refreshing && !keepActiveWorking && d.TelemetryState == TelemetryState.Ready && !d.Protected;
            var change = menu.Items.Add(d.IsOffline ? "Mount drive" : "Dismount drive", null, async delegate { await RunSingle(d); }); change.Enabled = available;
            var awake = menu.Items.Add(keepActive.Contains(EnclosureProfile.Identity(d)) ? "Stop keeping active" : "Keep active while app is open", null, delegate { ToggleActive(new[] { d }); }); awake.Enabled = available && !d.IsOffline;
            foreach (var volume in d.Volumes)
            {
                var v = volume; var item = new ToolStripMenuItem((v.DriveLetter ?? "") + ": " + v.Label) { Enabled = available && !d.IsOffline };
                item.DropDownItems.Add("Rename volume…", null, async delegate { await EditVolume(d, v, false); });
                item.DropDownItems.Add("Assign drive letter…", null, async delegate { await EditVolume(d, v, true); }); menu.Items.Add(item);
            }
            menu.Closed += delegate { menu.Dispose(); }; menu.Show(card, new Point(Fluent.S(20), Fluent.S(225)));
        }
        async Task RunSingle(StorageDisk disk)
        {
            if (busy || refreshing || keepActiveWorking) return;
            if (!demo && injectedReader) return;
            if (MessageBox.Show(this, (disk.IsOffline ? "Mount" : "Dismount") + " disk " + disk.Number + "?", "Confirm drive action", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
            keepActive.Remove(EnclosureProfile.Identity(disk)); Busy(true);
            try { if (demo) disk.IsOffline = !disk.IsOffline; else await Task.Run(() => storage.Change(disk, disk.IsOffline, Log)); }
            catch (Exception e) { Log(e.Message); }
            finally { Busy(false); }
            await RefreshDisks();
        }
        async Task EditVolume(StorageDisk disk, StorageVolume volume, bool letter)
        {
            if (busy || refreshing || keepActiveWorking || (!demo && injectedReader)) return;
            using (var dialog = new Form { Text = letter ? "Assign drive letter" : "Rename volume", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(420, 150), BackColor = Fluent.Sidebar, ForeColor = Fluent.Text, MinimizeBox = false, MaximizeBox = false })
            {
                var hint = new Label { Text = letter ? "Choose D–Z. Windows remembers the assignment where possible." : "New filesystem volume label:", Left = 16, Top = 15, Width = 385, Height = 35 };
                var input = new TextBox { Left = 16, Top = 55, Width = 385, Text = letter ? volume.DriveLetter : volume.Label, MaxLength = letter ? 1 : 32 };
                var save = new Button { Text = "Apply", DialogResult = DialogResult.OK, Left = 305, Top = 100 }; dialog.Controls.AddRange(new Control[] { hint, input, save }); dialog.AcceptButton = save;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string value = letter ? input.Text.Trim().ToUpperInvariant() : input.Text;
                if (letter && (value.Length != 1 || value[0] < 'D' || value[0] > 'Z')) { MessageBox.Show(this, "Choose a letter from D to Z."); return; }
                Busy(true);
                try
                {
                    if (demo) { if (letter) volume.DriveLetter = value; else volume.Label = value; }
                    else await Task.Run(() => storage.EditVolume(disk, volume, letter ? null : value, letter ? value : null));
                    Log("Volume update completed.");
                }
                catch (Exception e) { Log("Volume update failed: " + e.Message); }
                finally { Busy(false); }
                await RefreshDisks();
            }
        }
    }
}
