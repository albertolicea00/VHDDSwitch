using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using VHDDSwitch.Core;

namespace VHDDSwitch.Desktop
{
    internal sealed class GroupEditor : Form
    {
        internal readonly List<EnclosureProfile> Groups;
        readonly ComboBox groups = new ComboBox();
        readonly TextBox name = new TextBox();
        readonly CheckedListBox drives = new CheckedListBox();
        EnclosureProfile current;
        sealed class Choice
        {
            internal string Key, Label;
            public override string ToString() { return Label; }
        }
        internal GroupEditor(Settings settings, List<StorageDisk> disks)
        {
            Groups = new JavaScriptSerializer().Deserialize<List<EnclosureProfile>>(new JavaScriptSerializer().Serialize(settings.profiles));
            Text = "Manage drive groups"; Size = new Size(Fluent.S(720), Fluent.S(580)); MinimumSize = Size;
            StartPosition = FormStartPosition.CenterParent; BackColor = Fluent.Background; ForeColor = Fluent.Text; Font = new Font("Segoe UI", 10);
            MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 6 };
            foreach (int h in new[] { 85, 32, 45, 45, 0, 48 }) layout.RowStyles.Add(h == 0 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.Absolute, Fluent.S(h)));
            var top = new FlowLayoutPanel { Dock = DockStyle.Fill };
            groups.DropDownStyle = ComboBoxStyle.DropDownList; groups.Width = Fluent.S(350);
            top.Controls.Add(groups);
            var add = new Button { Text = "New group", AutoSize = true }; top.Controls.Add(add);
            var remove = new Button { Text = "Delete", AutoSize = true }; top.Controls.Add(remove);
            var suggest = new Button { Text = "Suggest USB groups", AutoSize = true }; top.Controls.Add(suggest);
            suggest.Click += delegate
            {
                Store(); var suggestions = UsbGrouping.Suggest(disks).Where(g => !Groups.Any(old => old.members != null && new HashSet<string>(old.members).SetEquals(g.members))).ToList();
                if (suggestions.Count == 0) { MessageBox.Show(this, "No new groups share a confirmed USB device interface. Refresh drive details and try again. Devices attached to different USB interfaces are not combined just because they share a hub."); return; }
                foreach (var suggestion in suggestions)
                {
                    string description = String.Join(Environment.NewLine, disks.Where(suggestion.Matches).Select(d => d.FriendlyName + " · " + d.SerialNumber));
                    if (MessageBox.Show(this, "Create a suggested group for these drives?" + Environment.NewLine + description, "Shared USB interface", MessageBoxButtons.YesNo) != DialogResult.Yes) continue;
                    Groups.Add(suggestion); groups.Items.Add(suggestion); groups.SelectedItem = suggestion;
                }
            };
            layout.Controls.Add(top, 0, 0);
            layout.Controls.Add(new Label { Text = "Group name", Dock = DockStyle.Fill }, 0, 1);
            name.Dock = DockStyle.Fill; layout.Controls.Add(name, 0, 2);
            layout.Controls.Add(new Label { Text = "Select drives. Disconnected members are kept until unchecked.", Dock = DockStyle.Fill }, 0, 3);
            drives.Dock = DockStyle.Fill; drives.CheckOnClick = true; drives.BackColor = Fluent.Sidebar; drives.ForeColor = Fluent.Text;
            foreach (var d in disks.Where(d => !String.IsNullOrWhiteSpace(d.UniqueId)))
                drives.Items.Add(new Choice { Key = EnclosureProfile.Identity(d), Label = d.FriendlyName + " · " + d.SerialNumber + " · " + d.BusType + (d.Protected ? " · Protected" : "") });
            foreach (string key in Groups.SelectMany(g => g.members ?? new string[0]).Distinct())
                if (!drives.Items.Cast<Choice>().Any(c => c.Key == key)) drives.Items.Add(new Choice { Key = key, Label = "Disconnected · " + key });
            layout.Controls.Add(drives, 0, 4);
            var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var save = new Button { Text = "Save changes", AutoSize = true }; var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            bottom.Controls.Add(save); bottom.Controls.Add(cancel); layout.Controls.Add(bottom, 0, 5); Controls.Add(layout);
            groups.SelectedIndexChanged += delegate { Store(); current = groups.SelectedItem as EnclosureProfile; LoadGroup(); };
            add.Click += delegate { Store(); var g = new EnclosureProfile { id = Guid.NewGuid().ToString("N"), name = "New group", members = new string[0], expectedBays = 1 }; Groups.Add(g); groups.Items.Add(g); groups.SelectedItem = g; name.Focus(); name.SelectAll(); };
            remove.Click += delegate { if (current == null) return; var old = current; current = null; Groups.Remove(old); groups.Items.Remove(old); if (groups.Items.Count > 0) groups.SelectedIndex = 0; else LoadGroup(); };
            save.Click += delegate { Store(); if (Groups.Any(g => String.IsNullOrWhiteSpace(g.name))) { MessageBox.Show(this, "Each group needs a name."); return; } DialogResult = DialogResult.OK; Close(); };
            foreach (var g in Groups.Where(g => !g.allExternal)) groups.Items.Add(g);
            if (groups.Items.Count > 0) groups.SelectedIndex = 0; else LoadGroup();
            AcceptButton = save; CancelButton = cancel;
        }
        void Store()
        {
            if (current == null) return;
            current.name = name.Text.Trim(); current.members = drives.CheckedItems.Cast<Choice>().Select(c => c.Key).Distinct().ToArray();
            current.expectedBays = Math.Max(1, Math.Min(64, current.members.Length)); current.autoMountOnDetect = false;
        }
        void LoadGroup()
        {
            name.Enabled = drives.Enabled = current != null; name.Text = current == null ? "" : current.name;
            for (int i = 0; i < drives.Items.Count; i++) drives.SetItemChecked(i, current != null && current.members != null && current.members.Contains(((Choice)drives.Items[i]).Key));
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Fluent.WindowStyle(Handle); }
    }
}
