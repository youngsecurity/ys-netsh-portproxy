#region + -- IMPORTS -- +

using JSE_Utils;
using Microsoft.Win32;
using NStandard;
using PortProxyGooey.Data;
using PortProxyGooey.UI;
using PortProxyGooey.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Resources;
using System.Text;
using System.Windows.Forms;
using static System.Windows.Forms.ListViewItem;
using ListView = System.Windows.Forms.ListView;

#endregion

namespace PortProxyGooey {

    public partial class PortProxyGooey : Form {

        #region + -- VAR DECLARATIONS -- +

        // Easy place to change info app-wide as needed
        internal static readonly string strAppURL = "https://github.com/STaRDoGG/PortProxyGUI";
        internal static readonly string strWFCURL = "https://www.binisoft.org/wfc";

        private readonly ListViewColumnSorter lvwColumnSorter = new();
        public SetProxy SetProxyForm;
        public About AboutForm;
        private AppConfig AppConfig;
        private DateTime ClickStartTime;
        private int ClickCount;

        // Keep track of enable/disabled proxies
        private int intEnabled = 0;
        private int intDisabled = 0;

        #endregion

        public PortProxyGooey() {

            InitializeComponent();

            this.Text = $"Port Proxy Gooey v{Application.ProductVersion}";

            listViewProxies.ListViewItemSorter = lvwColumnSorter;
        }

        private void PortProxyGooey_Load(object sender, EventArgs e) {

            // TEST AREA



            //Audio.PlaySound_BGW("HS");
            //Audio.PlaySound2_BGW("HS");
            //Audio.tmpPlay(Properties.Resources.HS);

            Debug.WriteLine(WSL.GetDistros());
            //Audio.PlaySound_BGW("Flush");
            //WSL.GetListeningPorts_BGW((dicPorts) => Debug.WriteLine($"WSL Listening Ports: {dicPorts}"));

            // END

            // Get the resource manager for your application.
            ResourceManager rm = Properties.Resources.ResourceManager;

            // Retrieve the image resource by name.
            // TODO: https://stackoverflow.com/questions/4416934/c-how-to-make-a-picture-background-transparent
            //Image img = (Image)rm.GetObject("decoration");

            //e.Graphics.DrawImage(img, 50, 50, 100, 100);

            //Debug.WriteLine(PortProxyUtil.CheckPortOpen("::1", 3568)); // bad
            //Debug.WriteLine(PortProxyUtil.CheckPortOpen("::1", 443));  // good
            //Debug.WriteLine(PortProxyUtil.CheckPortOpen("24.14.15.201", 3568)); // bad
            //Debug.WriteLine(PortProxyUtil.CheckPortOpen("24.14.15.201", 443));  // good
            //Debug.WriteLine(PortProxyUtil.CheckPortOpen("0.0.0.0", 443)); // good (returns false though. is it because 0.0.0.0 isnt a real ip?)
            //Debug.WriteLine(PortProxyUtil.CheckPortOpen("127.0.0.1", 443)); // good

            //JSE_Utils.WSL.WSL_GetVersion();

            Debug.WriteLine("Network Available?: " + Network.IsNetworkAvailable());

            //Debug.WriteLine(Services.GetInfo(PortProxyUtil.ServiceName));

            AppConfig = Program.Database.GetAppConfig();

            // Set Main Window Size from saved settings
            Size size = AppConfig.MainWindowSize;
            this.Left -= (size.Width - Width) / 2;
            this.Top -= (size.Height - Height) / 2;
            ResetWindowSize();

            // Set Main Window Location from saved settings
            if (AppConfig.MainWindowLocationX != 0 && AppConfig.MainWindowLocationY != 0) {
                this.StartPosition = FormStartPosition.Manual;
                this.Left = AppConfig.MainWindowLocationX;
                this.Top = AppConfig.MainWindowLocationY;
            }

        }

        private void PortProxyGooey_Shown(object sender, EventArgs e) {

            RefreshProxyList();

            // Sort columns based on saved config
            lvwColumnSorter.SortColumn = AppConfig.SortColumn;

            switch (AppConfig.SortOrder) {

                case 2:
                    lvwColumnSorter.Order = SortOrder.Descending;
                    break;
                case 1:
                    lvwColumnSorter.Order = SortOrder.Ascending;
                    break;
                case 0:
                    lvwColumnSorter.Order = SortOrder.None;
                    break;

            }

            // Perform the sort with these new sort options
            listViewProxies.Sort();

            // Update labels
            UpdateProxyCount();
        }

        private void ResetWindowSize() {

            Size = AppConfig.MainWindowSize;

            if (AppConfig.PortProxyColumnWidths.Length != listViewProxies.Columns.Count) {
                Any.ReDim(ref AppConfig.PortProxyColumnWidths, listViewProxies.Columns.Count);
            }

            foreach ((ColumnHeader column, int configWidth) in Any.Zip(listViewProxies.Columns.OfType<ColumnHeader>(), AppConfig.PortProxyColumnWidths)) {
                column.Width = configWidth;
            }

        }

        private static Rule ParseRule(ListViewItem item) {

            ListViewSubItem[] subItems = item.SubItems.OfType<ListViewSubItem>().ToArray();
            int listenPort, connectPort;

            listenPort = Rule.ParsePort(subItems[3].Text);
            connectPort = Rule.ParsePort(subItems[5].Text);

            Rule rule = new() {

                Type = subItems[1].Text.Trim(),
                ListenOn = subItems[2].Text.Trim(),
                ListenPort = listenPort,
                ConnectTo = subItems[4].Text.Trim(),
                ConnectPort = connectPort,
                Comment = subItems[6].Text.Trim(),
                Group = item.Group?.Header.Trim(),

            };

            return rule;

        }

        /// <summary>
        /// Enable/Disable selected Proxies
        /// </summary>
        /// <param name="bEnable">[optional: default True] True: Enable selected; False: Disable selected.</param>
        /// <param name="bAll">[optional: default False] True: Enable All; False: Disable All.</param>
        private void ToggleSelectedProxies(bool bEnable = true, bool bAll = false) {
            // TODO: working on the label tip counter(s)
            // TODO: The "All" code so far "seems" to be working, but the tooltip counter gets fucked up after using it. Seems to trigger after doing a couple manually, then the All.

            IEnumerable<ListViewItem> items;

            // Determine whether we're en/disabling All items in the list, or just the selected ones.
            if (bAll) {
                items = listViewProxies.Items.OfType<ListViewItem>();
            } else {
                items = listViewProxies.SelectedItems.OfType<ListViewItem>();
            }

            // Set the imageindex of each item + keep track of how many items are en/disabled
            foreach (ListViewItem item in items) {

                if (bEnable) {

                    item.ImageIndex = 1;

                    intEnabled++;
                    intDisabled--; // might be wrong

                } else {

                    item.ImageIndex = 0;

                    intEnabled--;
                    intDisabled++;

                }

                // Actually make the changes now
                try {

                    Rule rule = ParseRule(item);

                    if (bEnable) {
                        PortProxyUtil.AddOrUpdateProxy(rule);
                    } else {
                        PortProxyUtil.DeleteProxy(rule);
                    }

                } catch (NotSupportedException ex) {

                    MessageBox.Show(ex.Message, "Scramble the jets!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;

                }
            }

            // Update the label + tooltip
            UpdateProxyCount();

            // Notify the Windows Service (IpHlpSvc) of the changes
            Services.ParamChangeWinAPI(PortProxyUtil.ServiceName);

        }

        /// <summary>
        /// Delete Prox(ies)
        /// </summary>
        /// <param name="intShowConfirmation">[optional] display a confirmation first: 1 = Yes, 0 = No (default)</param>
        private void DeleteSelectedProxies(int intShowConfirmation = 0) {

            IEnumerable<ListViewItem> items = listViewProxies.SelectedItems.OfType<ListViewItem>();

            if (intShowConfirmation == 1) {

                int intCount = listViewProxies.SelectedItems.Count;

                // Pluralize if necessary ;)
                string strMsg = $"Delete {intCount} {(intCount == 1 ? "proxy" : "proxies")}?";

                if (MessageBox.Show(strMsg, "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No)
                    return;

            }

            ToggleSelectedProxies(false);
            Program.Database.RemoveRange(items.Select(x => new Rule { Id = x.Tag.ToString() }));
            foreach (ListViewItem item in items)
                listViewProxies.Items.Remove(item);

            RefreshProxyList();
            UpdateProxyCount();
        }

        private void SetProxyForUpdateOrClone(SetProxy form, bool bclone = false) {

            ListViewItem item = listViewProxies.SelectedItems.OfType<ListViewItem>().FirstOrDefault();

            try {

                Rule rule = ParseRule(item);

                if (bclone) {

                    // Clone rule
                    form.UseNormalMode(rule);

                } else {

                    // Modify rule
                    form.UseUpdateMode(item, rule);

                }

            } catch (NotSupportedException ex) {
                MessageBox.Show(ex.Message, "Ayy Now!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }

        }

        /// <summary>
        /// Add Groups to ListView
        /// </summary>
        /// <param name="rules"></param>
        private void InitProxyGroups(Rule[] rules) {

            listViewProxies.Groups.Clear();

            ListViewGroup[] groups = (
                from g in rules.GroupBy(x => x.Group)
                let name = g.Key
                where !name.IsNullOrWhiteSpace()
                orderby name
                select new ListViewGroup(name)
            ).ToArray();

            listViewProxies.Groups.AddRange(groups);

            // Add group names to menu item
            CreateGroupsMenuItem(groups);
        }

        private void InitProxyItems(Rule[] rules, Rule[] proxies) {

            // Reset when updating the list
            intEnabled = 0;
            intDisabled = 0;

            listViewProxies.Items.Clear();

            foreach (Rule rule in rules) {

                int imageIndex = proxies.Any(p => p.EqualsWithKeys(rule)) ? 1 : 0;

                ListViewGroup group = listViewProxies.Groups.OfType<ListViewGroup>().FirstOrDefault(x => x.Header == rule.Group);

                ListViewItem item = new();
                UpdateListViewItem(item, rule, imageIndex);
                listViewProxies.Items.Add(item);

                //int itemCount = Listview.CountItemsInGroup(listViewProxies, group);
                //Debug.WriteLine($"Number of items in group ({group}): {itemCount}");


            }

            // Left off: getting proxy count for group headers; need to figure out how to fetch by group name? Or just use it's index?. Is this the best place for it?


        }

        public void UpdateListViewItem(ListViewItem item, Rule rule, int imageIndex) {

            // WIP: in process of handling text color changes for ConnectTo column.
            // Idea is to check if the WSL IP already in that columnn is outdated compared to the current WSL IP; if yes, then color it red to alert user they may want to update it so it still works.
            item.UseItemStyleForSubItems = false;
            Debug.WriteLine("IP:" + lblWSLIP.Text.Substring(lblWSLIP.Text.IndexOf("WSL: ") + 5)); // NOTE: timer doesn't fetch the IP in time to use this here, during initial load. Prob add a func to the timer itself?





            item.ImageIndex = imageIndex;
            item.Tag = rule.Id;
            item.SubItems.Clear();
            item.ToolTipText = $"Added:"; // TODO
            item.SubItems.AddRange(new[]
            {
                new ListViewSubItem(item, rule.Type),
                new ListViewSubItem(item, rule.ListenOn),
                new ListViewSubItem(item, rule.ListenPort.ToString()) { Tag = "Number" },
                new ListViewSubItem(item, rule.ConnectTo) { ForeColor = Color.FromArgb(191, 97, 106), BackColor = Color.FromArgb(67, 76, 94), Font = item.Font },
                new ListViewSubItem(item, rule.ConnectPort.ToString ()) { Tag = "Number" },
                new ListViewSubItem(item, rule.Comment ?? string.Empty),
            });

            if (rule.Group.IsNullOrWhiteSpace())
                item.Group = null;
            else {

                ListViewGroup group = listViewProxies.Groups.OfType<ListViewGroup>().FirstOrDefault(x => x.Header == rule.Group);

                if (group == null) {

                    group = new ListViewGroup(rule.Group);
                    listViewProxies.Groups.Add(group);

                }

                item.Group = group;
            }

            // Keep track of how many enabled/disabled
            if (imageIndex == 1) {
                intEnabled++;
            } else {
                intDisabled++;
            }

        }

        /// <summary>
        /// Adds all group labels to the Move menu
        /// </summary>
        /// <param name="lvGroups">The list of group names</param>
        private void CreateGroupsMenuItem(ListViewGroup[] lvGroups) {

            // First, clear out any previous ones in case we're renaming a group, refreshing this list, or something, so we don't get dupes.
            foreach (ToolStripMenuItem item in toolStripMenuItem_MoveTo.DropDownItems.OfType<ToolStripMenuItem>().ToList()) {
                toolStripMenuItem_MoveTo.DropDownItems.Remove(item);
            }

            // Add a "New" item
            // Default group doesn't get added to the ListViewGroup[]; manually add it.
            ToolStripMenuItem toolStripMenuItem_Move = new ToolStripMenuItem("New ...");
            toolStripMenuItem_Move.Click += ToolStripMenuItem_Move_Click;
            toolStripMenuItem_MoveTo.DropDownItems.Add(toolStripMenuItem_Move);
            toolStripMenuItem_Move.Image = Properties.Resources._new;
            toolStripMenuItem_Move.ToolTipText = "Move proxy(s) to a new group";

            // Add a seperator
            ToolStripSeparator toolStripSeparatorNewGroup = new ToolStripSeparator();
            toolStripMenuItem_MoveTo.DropDownItems.Add(toolStripSeparatorNewGroup);

            // Default group doesn't get added to the ListViewGroup[]; manually add it.
            toolStripMenuItem_Move = new ToolStripMenuItem("Default");
            toolStripMenuItem_Move.Click += ToolStripMenuItem_Move_Click;
            toolStripMenuItem_MoveTo.DropDownItems.Add(toolStripMenuItem_Move);
            toolStripMenuItem_Move.Image = Properties.Resources.add;
            toolStripMenuItem_Move.ToolTipText = "Move selected proxy(s) to the Default group";

            // Add all the other groups
            foreach (ListViewGroup header in lvGroups) {

                toolStripMenuItem_Move = new ToolStripMenuItem(header.Header);
                toolStripMenuItem_Move.Click += ToolStripMenuItem_Move_Click;
                toolStripMenuItem_MoveTo.DropDownItems.Add(toolStripMenuItem_Move);

                // Give it an icon for some of the well known things
                if (header.ToString().ToLower().Contains("docker")) {
                    toolStripMenuItem_Move.Image = Properties.Resources.docker_1;
                } else if (header.ToString().ToLower().Contains("wsl")) {
                    toolStripMenuItem_Move.Image = Properties.Resources.wsl;
                }

                // Set any other properties of the menu item
                //toolStripMenuItem_Move.ToolTipText = string.Format("Move proxy(s) to the {0} group", header.Header);

            }
        }

        /// <summary>
        /// Updates the proxy count label and tooltip
        /// </summary>
        public void UpdateProxyCount() {

            string strTotal = listViewProxies.Items.Count.ToString();

            lblProxyCount.Text = strTotal;
            tTipPPG.SetToolTip(lblProxyCount, string.Format("Total Proxies: {0}{3}Enabled: {1}{3}Disabled: {2}", strTotal, intEnabled.ToString(), intDisabled.ToString(), Environment.NewLine));

        }

        public void RefreshProxyList() {

            listViewProxies.Cursor = Cursors.WaitCursor;

            // Fetch all proxies stored in the registry
            Rule[] proxies = PortProxyUtil.GetProxies();

            // Fetch all proxies from db, store in an array.
            Rule[] rules = Program.Database.Rules.ToArray();

            // Compare list of registry proxies with list of proxies stored in our db (?)
            foreach (Rule proxy in proxies) {

                Rule matchedRule = rules.FirstOrDefault(r => r.EqualsWithKeys(proxy));
                proxy.Id = matchedRule?.Id;

            }

            //
            IEnumerable<Rule> pendingAdds = proxies.Where(x => x.Valid && x.Id == null);
            IEnumerable<Rule> pendingUpdates =
                from proxy in proxies
                let exist = rules.FirstOrDefault(r => r.Id == proxy.Id)
                where exist is not null
                where proxy.Valid && proxy.Id is not null
                select proxy;

            Program.Database.AddRange(pendingAdds);
            Program.Database.UpdateRange(pendingUpdates);

            // Fetch all proxies from db (again)
            rules = Program.Database.Rules.ToArray();

            InitProxyGroups(rules);
            InitProxyItems(rules, proxies);

            // LEFT OFF: Now that I can get the count, need to add it to the actual header as well as handle other areas to remove the count as necessary
            foreach (ToolStripMenuItem item in toolStripMenuItem_MoveTo.DropDownItems.OfType<ToolStripMenuItem>().ToList()) {
                int itemCount = Listview.CountItemsInGroup(listViewProxies, item.Text);
                Debug.WriteLine($"Number of items in group ({item.Text}): {itemCount}");
            }

            listViewProxies.Cursor = Cursors.Default;

        }

        /// <summary>
        /// Context-Menu Actions
        /// </summary>
        private void contextMenuStrip_RightClick_MouseClick(object sender, MouseEventArgs e) {

            if (sender is ContextMenuStrip strip) {

                ToolStripMenuItem selected = strip.Items.OfType<ToolStripMenuItem>().FirstOrDefault(x => x.Selected);

                if (selected is null || !selected.Enabled)
                    return;

                contextMenuStrip_RightClick.Visible = false;

                switch (selected) {

                    case ToolStripMenuItem item when item == toolStripMenuItem_Enable:

                        ToggleSelectedProxies(true);
                        break;

                    case ToolStripMenuItem item when item == toolStripMenuItem_Disable:

                        ToggleSelectedProxies(false);
                        break;

                    // New Item
                    case ToolStripMenuItem item when item == toolStripMenuItem_New:

                        NewItem();
                        break;

                    // Clone Item
                    case ToolStripMenuItem item when item == toolStripMenuItem_Clone:

                        ModOrClone(true);
                        break;

                    // Modify/Edit Item
                    case ToolStripMenuItem item when item == toolStripMenuItem_Modify:

                        ModOrClone();
                        break;

                    // Refresh List
                    case ToolStripMenuItem item when item == toolStripMenuItem_Refresh:

                        RefreshProxyList();
                        break;

                    // Clear All Proxies
                    case ToolStripMenuItem item when item == clearToolStripMenuItem:

                        ClearProxies();
                        break;

                    // Delete Item(s)
                    case ToolStripMenuItem item when item == toolStripMenuItem_Delete:

                        DeleteSelectedProxies(1);
                        break;

                        // About
                        //case ToolStripMenuItem item when item == toolStripMenuItem_About:

                        //    AboutForm ??= new About(this);

                        //    AboutForm.Show();
                        //    break;
                }
            }
        }

        /// <summary>
        /// Are we modifying a proxy or cloning it?
        /// </summary>
        /// <param name="bClone">[Optional] True = Clone; else = Modify</param>
        private void ModOrClone(bool bClone = false) {

            SetProxyForm ??= new SetProxy(this);
            SetProxyForUpdateOrClone(SetProxyForm, bClone);
            SetProxyForm.ShowDialog();

        }

        /// <summary>
        /// Deletes ALL proxies in the list
        /// </summary>
        private void ClearProxies() {

            if (listViewProxies.Items.Count > 0) {

                if (MessageBox.Show(
                            string.Format("This will literally delete:{0}{0}- EVERY PROXY{0}- All Groups{0}- All Comments{0}{0}that you have in this list and on your machine and start from scratch.{0}{0}Proceed?", Environment.NewLine),
                            "* FOCUS! *",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question,
                            MessageBoxDefaultButton.Button2
                            ) == DialogResult.Yes) {

                    // Select all in the list and delete
                    listViewProxies.Items.Cast<ListViewItem>().ToList().ForEach(item => item.Selected = true);
                    DeleteSelectedProxies();
                }

            } else {
                MessageBox.Show("Nothing to clear, dumdum.", "But hey ... you tried. *shrug*", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

        }

        /// <summary>
        /// Add a new item to the proxy list
        /// </summary>
        private void NewItem() {

            SetProxyForm ??= new SetProxy(this);
            SetProxyForm.UseNormalMode();
            SetProxyForm.ShowDialog();

        }

        private void listViewProxies_MouseUp(object sender, MouseEventArgs e) {

            if (sender is ListView listView) {

                GroupRename();

                ListViewHitTestInfo hit = listViewProxies.HitTest(e.Location);

                // If a ListView item was clicked ...
                if (hit.Item != null) {

                    // Fetch which column the user clicked in
                    int columnindex = hit.Item.SubItems.IndexOf(hit.SubItem);

                    // If it's the first column (checkbox icon column) then toggle it's state
                    if (columnindex == 0 && e.Button == MouseButtons.Left) {
                        ToggleSelectedProxies(hit.Item.ImageIndex != 1);
                    }

                } else {

                    if (e.Button == MouseButtons.Left) {

                        ClickCount++;

                        if (ClickCount == 1) {

                            ClickStartTime = DateTime.Now;

                        } else if (ClickCount == 2) {

                            ClickCount = 0;

                            DateTime endTime = DateTime.Now;
                            TimeSpan elapsed = endTime - ClickStartTime;
                            double elapsedMilliseconds = elapsed.TotalMilliseconds;

                            if (elapsedMilliseconds < SystemInformation.DoubleClickTime) {

                                // Double-clicking an empty space opens the New Item dialog
                                NewItem();

                            }
                        }
                    }
                }
                ToggleMenuItems(e, listView);
            }
        }

        /// <summary>
        /// Handles the state of ListviewProxies menu items
        /// </summary>
        private void ToggleMenuItems(MouseEventArgs e, ListView listView) {

            int intCount = listViewProxies.SelectedItems.Count;

            // Enable/Disable (state): Set enabled state of toolstrip items based on the selected item's image index
            toolStripMenuItem_Enable.Enabled = e.Button == MouseButtons.Right && listView.SelectedItems.OfType<ListViewItem>().Any(x => x.ImageIndex == 0);
            toolStripMenuItem_Disable.Enabled = e.Button == MouseButtons.Right && listView.SelectedItems.OfType<ListViewItem>().Any(x => x.ImageIndex == 1);

            // Enable/Disable (count): Add count to label if more than 1
            if (toolStripMenuItem_Enable.Enabled && intCount > 1) { toolStripMenuItem_Enable.Text = $"Enable ({intCount})"; }
            if (toolStripMenuItem_Disable.Enabled && intCount > 1) { toolStripMenuItem_Disable.Text = $"Disable ({intCount})"; }

            // Only show the Enable/Disable All item if there are more than 1 items (proxies) in the list
            toolStripMenuItem_EnableDisableAll.Visible = listViewProxies.Items.Count > 1;

            // Add count to menu items let users know how many they're about to nuke (or move, etc.)
            if (intCount > 1) {

                toolStripMenuItem_Delete.Text = $"Delete ({intCount})";
                toolStripMenuItem_MoveTo.Text = $"Group: Move ({intCount}) to ...";

                // Firewall
                toolStripMenuItem_FirewallAdd.Text = $"Add ({intCount})";
                toolStripMenuItem_FirewallRemove.Text = $"Remove ({intCount})";

            }

            toolStripMenuItem_Delete.Enabled = e.Button == MouseButtons.Right && listView.SelectedItems.OfType<ListViewItem>().Any();
            toolStripMenuItem_Modify.Enabled = e.Button == MouseButtons.Right && listView.SelectedItems.OfType<ListViewItem>().Count() == 1;
            toolStripMenuItem_Clone.Enabled = e.Button == MouseButtons.Right && listView.SelectedItems.OfType<ListViewItem>().Count() == 1;

            // Firewall: Only vis if 1 or more are selected.
            toolStripMenuItem_Firewall.Visible = e.Button == MouseButtons.Right && listView.SelectedItems.OfType<ListViewItem>().Any();
            toolStripSeparator_Firewall.Visible = e.Button == MouseButtons.Right && listView.SelectedItems.OfType<ListViewItem>().Any();

            // Clear List: Only vis if Items exist in the list
            clearToolStripMenuItem.Visible = e.Button == MouseButtons.Right && listView.SelectedItems.OfType<ListViewItem>().Count() == 1;

            // Move: Only visible if Items exist in the list (along with the seperator so we don't get ugly double-seperators)
            toolStripMenuItem_MoveTo.Visible = e.Button == MouseButtons.Right && listView.SelectedItems.OfType<ListViewItem>().Count() == 1;
            toolStripSeparator_MoveTo.Visible = e.Button == MouseButtons.Right && listView.SelectedItems.OfType<ListViewItem>().Count() == 1;

            // NETSH Menu
            NetSHToolStripMenuItem.Visible = e.Button == MouseButtons.Right && listView.SelectedItems.OfType<ListViewItem>().Count() == 1;

            // Registry Key
            registryKeyToolStripMenuItem.Visible = e.Button == MouseButtons.Right && listView.SelectedItems.OfType<ListViewItem>().Count() == 1;
            toolStripSeparator_NetshRegistry.Visible = e.Button == MouseButtons.Right && listView.SelectedItems.OfType<ListViewItem>().Count() == 1;

            // Open in Browser / Copy to Clipboard
            toolStripMenuItem_OpenInBrowser.Visible = e.Button == MouseButtons.Right && listView.SelectedItems.OfType<ListViewItem>().Count() == 1;
            toolStripMenuItem_Clipboard.Visible = e.Button == MouseButtons.Right && listView.SelectedItems.OfType<ListViewItem>().Count() == 1;
            toolStripSeparator_BrowserClipboard.Visible = e.Button == MouseButtons.Right && listView.SelectedItems.OfType<ListViewItem>().Count() == 1;

        }

        /// <summary>
        /// Renames a group by editing all items in that group in the db to match the newly entered name.
        /// </summary>
        /// <param name="bRename"></param>
        private void GroupRename(bool bRename = false) {
            // TODO: need to finish when items are in the Default group....
            ListView.SelectedListViewItemCollection selectedItems = listViewProxies.SelectedItems;

            if (selectedItems.Count > 0) {

                ListViewItem selectedItem = selectedItems[0];
                ListViewGroup group = selectedItem.Group;

                if (group != null) {

                    string groupHeader = group.Header;

                    toolStripMenuItem_RenameGroup.Visible = true;

                    if (bRename) {

                        string input = groupHeader;

                        // Can't be the same as current, or empty.
                        if (Dialogs.InputDialog(ref input, "New group name:") == DialogResult.OK && input != groupHeader && !string.IsNullOrEmpty(input)) {

                            // Rename it in the db
                            Rule rule = ParseRule(selectedItem);
                            Program.Database.RenameGroup(rule, input.Trim());

                            RefreshProxyList();

                        }

                    }

                } else {

                    toolStripMenuItem_RenameGroup.Visible = false;
                    Debug.WriteLine("Item in Default Group");

                }

            }
        }

        /// <summary>
        /// Double-clicking an item in the list of proxies opens up it's Edit/Modify form
        /// </summary>
        private void listViewProxies_DoubleClick(object sender, EventArgs e) {

            if (sender is ListView listView) {

                bool selectAny = listView.SelectedItems.OfType<ListViewItem>().Any();

                if (selectAny) {

                    SetProxyForm ??= new SetProxy(this);
                    SetProxyForUpdateOrClone(SetProxyForm);
                    SetProxyForm.ShowDialog();

                }

            }
        }

        private void listViewProxies_ColumnClick(object sender, ColumnClickEventArgs e) {

            // Determine if clicked column is already the column that is being sorted.
            if (e.Column == lvwColumnSorter.SortColumn) {

                // Reverse the current sort direction for this column.
                lvwColumnSorter.Order = lvwColumnSorter.Order == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;

            } else {

                // Set the column number that is to be sorted; default to ascending.
                lvwColumnSorter.SortColumn = e.Column;
                lvwColumnSorter.Order = SortOrder.Ascending;

            }

            // Store them
            if (AppConfig is not null && sender is ListView) {

                AppConfig.SortColumn = lvwColumnSorter.SortColumn;
                AppConfig.SortOrder = (int)lvwColumnSorter.Order;

            }

            // Perform the sort with these new sort options.
            listViewProxies.Sort();
        }

        /// <summary>
        /// HotKeys / Shortcuts
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="keyData"></param>
        /// <returns></returns>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {

            switch (keyData) {

                // Enable Item
                case Keys.Control | Keys.E:

                    if (listViewProxies.SelectedItems.Count > 0) { ToggleSelectedProxies(true); }
                    return true;

                // Disable Item
                case Keys.Control | Keys.D:

                    if (listViewProxies.SelectedItems.Count > 0) { ToggleSelectedProxies(false); }
                    return true;

                // Delete Item(s)
                case Keys.Delete:

                    if (listViewProxies.SelectedItems.Count > 0) { DeleteSelectedProxies(1); }
                    return true;

                // Add New Item
                case Keys.Insert:

                    NewItem();
                    return true;

                // FlushDNS
                case Keys.Control | Keys.F:

                    Network.DNS.FlushCache();
                    return true;

                // Clear All Proxies
                case Keys.Control | Keys.C:

                    ClearProxies();
                    return true;

                // Edit Proxy
                case Keys.F2:

                    if (listViewProxies.SelectedItems.Count > 0) { ModOrClone(); }
                    return true;

                // Clone Proxy
                case Keys.F3:

                    if (listViewProxies.SelectedItems.Count > 0) { ModOrClone(true); }
                    return true;

                // Refresh List
                case Keys.F5:

                    RefreshProxyList();
                    return true;

                default:
                    return base.ProcessCmdKey(ref msg, keyData);

            }

        }

        private void listViewProxies_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e) {

            if (AppConfig is not null && sender is ListView listView) {
                AppConfig.PortProxyColumnWidths[e.ColumnIndex] = listView.Columns[e.ColumnIndex].Width;
            }

        }

        /// <summary>
        /// Saves all current configs
        /// </summary>
        private void PortProxyGooey_FormClosing(object sender, FormClosingEventArgs e) {

            // Save Main Window Location
            AppConfig.MainWindowLocationX = this.Location.X;
            AppConfig.MainWindowLocationY = this.Location.Y;

            // Save everything else
            Program.Database.SaveAppConfig(AppConfig);
        }

        private void PortProxyGooey_Resize(object sender, EventArgs e) {

            if (AppConfig is not null && sender is Form form)
                AppConfig.MainWindowSize = form.Size;

            //Debug.WriteLine($"Resized to: [w: {Width} h: {Height}]");
        }

        #region + -- IMPORT / EXPORT -- +

        /// <summary>
        /// Exports current list of proxies to .db
        /// </summary>
        private void toolStripMenuItem_Export_Click(object sender, EventArgs e) {

            SaveFileDialog saveFileDialog = new();

            // Autogenerate a name they can use if they want
            string t2 = DateTime.Now.ToString("MM-dd-yyyy-hh-mm-ss");
            string strGen = "PortProxyGooey-" + t2;

            saveFileDialog.Title = "Export Current Proxy List ...";
            saveFileDialog.Filter = "db files (*.db)|*.db";
            saveFileDialog.FileName = strGen;

            bool intErrCheck = false;

            if (saveFileDialog.ShowDialog() == DialogResult.OK) {

                try {

                    File.Copy(ApplicationDbScope.AppDB, saveFileDialog.FileName, true);

                } catch (Exception ex) {

                    intErrCheck = true;
                    Debug.WriteLine($"Save File issue: {ex.Message}");

                }

                // Give some user feedback
                if (intErrCheck == true) {
                    MessageBox.Show("Couldn't Export Proxy List", "Oops", MessageBoxButtons.OK, MessageBoxIcon.Error);
                } else {
                    MessageBox.Show("Proxy List Exported", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        /// <summary>
        /// Imports saved proxy list from .db
        /// </summary>
        private void toolStripMenuItem_Import_Click(object sender, EventArgs e) {

            using OpenFileDialog openFileDialog = new();
            openFileDialog.Title = "Import Proxy List ...";
            openFileDialog.Filter = "db files (*.db)|*.db";
            int intAdded = 0;

            if (openFileDialog.ShowDialog() == DialogResult.OK) {

                if (MessageBox.Show($"Overwrite current list with the selected list? {openFileDialog.FileName}", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes) {

                    using (ApplicationDbScope scope = ApplicationDbScope.FromFile(openFileDialog.FileName)) {

                        foreach (Rule rule in scope.Rules) {

                            Rule exist = Program.Database.GetRule(rule.Type, rule.ListenOn, rule.ListenPort);

                            if (exist is null) {

                                rule.Id = Guid.NewGuid().ToString();
                                Program.Database.Add(rule);
                                intAdded++;

                            }
                        }
                    }

                    RefreshProxyList();
                    MessageBox.Show($"{intAdded} rules imported.", "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        #endregion

        private void toolStripMenuItem_ResetWindowSize_Click(object sender, EventArgs e) {

            if (MessageBox.Show("Are sure you want to reset the window?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes) {

                AppConfig = new AppConfig();
                ResetWindowSize();

            }

        }

        #region + -- EXTERNAL APPS -- +

        /// <summary>
        /// External App: Network Adapters
        /// </summary>
        private void adaptersToolStripMenuItem_Click(object sender, EventArgs e) {
            Misc.RunCommand("explorer", "/e,::{26EE0668-A00A-44D7-9371-BEB064C98683}\\0\\::{7007ACC7-3202-11D1-AAD2-00805FC1270E}");

        }

        /// <summary>
        /// External App: Windows Firewall Control (Rules Panel)
        /// </summary>
        private void rulesPanelToolStripMenuItem_Click(object sender, EventArgs e) {
            WFC("-rp");
        }

        /// <summary>
        /// External App: Windows Firewall Control (Connections Log)
        /// </summary>
        private void connectionsPanelToolStripMenuItem_Click(object sender, EventArgs e) {
            WFC("-cl");
        }

        /// <summary>
        /// External App: Windows Firewall Control
        /// </summary>
        /// <param name="strPanel">The Panel to open</param>
        private static void WFC(string strPanel) {

            try {

                RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Windows Firewall Control");

                if (key != null) {

                    Misc.RunCommand(key.GetValue("InstallationPath").ToString(), strPanel);
                    key.Close();

                } else {

                    // If path to WFC isnt found in the registry, as a courtesy, launch the website for them to dl it, if they want.
                    Misc.RunCommand(strWFCURL);

                }

            } catch (Exception ex) {
                Debug.WriteLine($"WFC(): {ex.Message}");
            }

        }

        /// <summary>
        /// External App: Windows Firewall (Basic)
        /// </summary>
        private void basicToolStripMenuItem_Click(object sender, EventArgs e) {

            try {
                Misc.RunCommand("control", "firewall.cpl");
            } catch (Exception ex) {
                Debug.WriteLine($"Error Launching firewall.cpl: {ex.Message}");
            }

        }

        /// <summary>
        /// External App: Windows Firewall (Advanced)
        /// </summary>
        private void advancedToolStripMenuItem_Click(object sender, EventArgs e) {
            Misc.RunCommand("cmd", "/C wf.msc", Environment.GetEnvironmentVariable("WINDIR") + @"\System32");
        }

        #endregion

        /// <summary>
        /// FlushDNS
        /// </summary>
        private void toolStripMenuItem_FlushDnsCache_Click(object sender, EventArgs e) {

            if (Network.DNS.FlushCache(true, false)) {

                // Give em' a Swirly!
                Audio.PlaySound_BGW("Flush");

                // Note: I could use the built-in confirmation dialog in FlushCache(), but it does a "ding" system sound,
                //       which conflicts with the .wav, so I'm playing the .wav first, THEN let it ding. Slightly nicer.
                MessageBox.Show("DNS Flushed!", "Whoosh", MessageBoxButtons.OK, MessageBoxIcon.Information);

            }

        }

        /// <summary>
        /// Resets the counts on the right-click context menu when the menu closes
        /// </summary>
        private void contextMenuStrip_RightClick_Closed(object sender, ToolStripDropDownClosedEventArgs e) {

            toolStripMenuItem_Enable.Text = "Enable";
            toolStripMenuItem_Disable.Text = "Disable";
            toolStripMenuItem_Delete.Text = "Delete";
            toolStripMenuItem_MoveTo.Text = "Group: Move to ...";

            // Firewall
            toolStripMenuItem_FirewallAdd.Text = "Add";
            toolStripMenuItem_FirewallRemove.Text = "Remove";

        }

        #region + -- CLIPBOARD / VIEW CLINE -- +

        /// <summary>
        /// Copies netsh add command to clipboard
        /// </summary>
        private void NetSHAddCopyToClipboard_Click(object sender, EventArgs e) {
            AdvNetsh(1, 1);
        }

        /// <summary>
        /// Shows netsh add command in a messagebox
        /// </summary>
        private void NetSHAddViewCline_Click(object sender, EventArgs e) {
            AdvNetsh(2, 1);
        }

        /// <summary>
        /// Copy delete cmd to clipboard
        /// </summary>
        private void NetSHDelCopyToClipboard_Click(object sender, EventArgs e) {
            AdvNetsh(1, 2);
        }

        /// <summary>
        /// View delete command in msgbox
        /// </summary>
        private void NetSHDelViewCline_Click(object sender, EventArgs e) {
            AdvNetsh(2, 2);
        }

        /// <summary>
        /// Either shows or copies to clipboard the actual netsh cline for the selected item
        /// </summary>
        /// <param name="intType">1 = Clipboard; else = View</param>
        /// <param name="intCmd">2 = delete; else = add</param>
        private void AdvNetsh(int intType, int intCmd) {

            try {

                string strCmd = intCmd == 2 ? "delete" : "add";
                string strMessage = string.Empty;

                // Message to copy/show
                if (intCmd == 2) {

                    // Delete
                    strMessage = string.Format("netsh interface portproxy {3} {0} listenaddress={1} listenport={2}",
                                                listViewProxies.FocusedItem.SubItems[1].Text,
                                                listViewProxies.FocusedItem.SubItems[2].Text,
                                                listViewProxies.FocusedItem.SubItems[3].Text,
                                                strCmd);
                } else {

                    // Add
                    strMessage = string.Format("netsh interface portproxy {5} {0} listenaddress={1} listenport={2} connectaddress={3} connectport={4}",
                                                listViewProxies.FocusedItem.SubItems[1].Text,
                                                listViewProxies.FocusedItem.SubItems[2].Text,
                                                listViewProxies.FocusedItem.SubItems[3].Text,
                                                listViewProxies.FocusedItem.SubItems[4].Text,
                                                listViewProxies.FocusedItem.SubItems[5].Text,
                                                strCmd);
                }

                // Action to do (copy to clipboard or messagebox)
                if (intType == 1) {
                    Clipboard.SetText(strMessage);
                } else {
                    MessageBox.Show(strMessage, $"netsh {strCmd}", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

            } catch (Exception ex) {
                Debug.WriteLine($"netsh {intCmd}: {(intType == 1 ? "Copy" : "View")} error: {ex.Message}");
                throw;
            }
        }

        #endregion

        /// <summary>
        /// Open Regedit to the specified proxy 'Type'
        /// </summary>
        private void registryKeyToolStripMenuItem_Click(object sender, EventArgs e) {

            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit", true);

            if (key != null) {
                key.SetValue("LastKey", @$"HKEY_LOCAL_MACHINE\{PortProxyUtil.GetKeyName(listViewProxies.SelectedItems[0].SubItems[1].Text)}", RegistryValueKind.String);
                key.Close();
            }

            Misc.RunCommand("regedit.exe");
        }

        private void portForwardingTesterToolStripMenuItem_Click(object sender, EventArgs e) {
            Network.Link_OpenPortTester();
        }

        #region + -- DOUBLE-CLICKING ALL OF THESE OPENS NEW ITEM DIALOG  -- +

        private void lblGooey_DoubleClick(object sender, EventArgs e) {
            NewItem();
        }

        private void lblPP_DoubleClick(object sender, EventArgs e) {
            NewItem();
        }

        private void lblJSE_DoubleClick(object sender, EventArgs e) {
            NewItem();
        }

        private void lblProxyCount_DoubleClick(object sender, EventArgs e) {
            NewItem();
        }

        private void PortProxyGooey_DoubleClick(object sender, EventArgs e) {
            NewItem();
        }

        private void pnlBar_DoubleClick(object sender, EventArgs e) {
            NewItem();
        }

        #endregion

        private void winsockResetToolStripMenuItem_Click(object sender, EventArgs e) {
            Winsock.Reset();
        }

        #region + -- WSL -- +

        private void WSLRunningToolStripMenuItem_Click(object sender, EventArgs e) {
            WSL.IsRunning(true);
        }

        private void WSLShutDownToolStripMenuItem_Click(object sender, EventArgs e) {
            WSL.ShutDown();
        }

        /// <summary>
        /// WSL Mini Menu: Shutdown
        /// </summary>
        private void toolStripMenuItemWSLShutDown_Click(object sender, EventArgs e) {
            WSL.ShutDown();
        }

        private void WSLStartToolStripMenuItem_Click(object sender, EventArgs e) {
            WSL.Start();
        }

        private void WSLRestartToolStripMenuItem_Click(object sender, EventArgs e) {
            WSL.Restart();
        }

        /// <summary>
        /// WSL Mini Menu: Restart
        /// </summary>
        private void toolStripMenuItemWSLRestart_Click(object sender, EventArgs e) =>

            //WSL.Restart();

            WSL.Restart_BGW((result) => { }, true, true);

        #endregion

        #region + -- DOCKER -- +

        private void picDocker_Click(object sender, EventArgs e) {
            contextMenuStrip_Docker.Show(Cursor.Position);
        }

        #endregion

        private void tmrCheck_Tick(object sender, EventArgs e) {

            // Keep Current Local Machine & WSL IPs shown to help alert of any potential changes

            // Fetch all Local IPs
            List<string> lstIPs = Network.GetLocalIPAddress();

            // Get another list of those IPs, excluding the 1st one
            List<string> lstAltIPs = lstIPs.GetRange(1, lstIPs.Count - 1);

            // Join the 'extra' local IPs
            string strAltIPs = string.Join(Environment.NewLine, lstAltIPs);

            lblCurrentLocalIP.Text = $"LOCAL: {(lstIPs.Count > 0 ? lstIPs[0] : "N/A")}";

            // If any "alternate/other" IP's let's add them to the tooltip
            if (lstAltIPs.Count > 0) {
                tTipPPG.SetToolTip(lblCurrentLocalIP, $"Other Reported IPs:{Environment.NewLine}{strAltIPs}");
            }

            // + ----- SERVICE

            // Keep IpHlpSvc status updated
            Services.IsRunning_BGW((result) => {

                if (result) {

                    picIpHlpSvcStatus.Image = Properties.Resources.green;
                    tTipPPG.SetToolTip(picIpHlpSvcStatus, $"{PortProxyUtil.ServiceFriendlyName.ToUpper()} SERVICE: RUNNING");

                } else {

                    picIpHlpSvcStatus.Image = Properties.Resources.red;
                    tTipPPG.SetToolTip(picIpHlpSvcStatus, $"{PortProxyUtil.ServiceFriendlyName.ToUpper()} SERVICE: N/A{Environment.NewLine}Click icon to Start it");

                }

            }, PortProxyUtil.ServiceName, false);

            // + ----- WSL

            WSL.GetIP_BGW((ip) => lblWSLIP.Text = $"WSL: {ip}");
            tTipPPG.SetToolTip(lblWSLIP, "Double-click copies IP to clipboard");

            // TODO: Scan ConnectTo column for stale WSL IPs (related: see line 364)

            // -----

            // Keep WSL status updated
            WSL.IsRunning_BGW((result) => {

                if (result) {

                    picWSLStatus.Image = Properties.Resources.green;
                    picWSLStatus.Tag = "1";
                    picWSL.Visible = true;

                    // LEFT OFF: Need to add WSL version, then below it, all the other WSL info I want.
                    StringBuilder sb = new();

                    WSL.GetVersion_BGW((strVer) => { sb.AppendLine($"WSL{(!string.IsNullOrEmpty(strVer) ? $" (v{(strVer)})" : string.Empty)}: RUNNING"); });

                    // Grab the other WSL Info
                    WSL.GetInfo_BGW((dicWSLInfo) => {

                        // Add the Uptime to the sb
                        if (!string.IsNullOrEmpty(dicWSLInfo["Up Since: Pretty"].ToString())) { sb.AppendLine($"Uptime: {dicWSLInfo["Up Since: Pretty"].ToString()}"); }

                        // NOTE: For reasons unbeknownst to me as of yet; sb loses it's context if put outside one of these BGWs; i.e. placing the following line outside this BGW,
                        //       even though it retains it's contents outside of the above different BGW call ...
                        // Set the final tooltip
                        tTipPPG.SetToolTip(picWSLStatus, sb.ToString());

                    });

                } else {

                    picWSLStatus.Image = Properties.Resources.red;
                    picWSLStatus.Tag = "0";
                    tTipPPG.SetToolTip(picWSLStatus, "WSL: N/A");
                    picWSL.Visible = false;

                }

            }, false);


            // + ----- DOCKER

            // Keep Docker status updated
            if (Docker.IsRunning()) {

                picDockerStatus.Image = Properties.Resources.green;
                Docker.GetInfo_BGW((DockerVersion) => tTipPPG.SetToolTip(picDockerStatus, $"DOCKER{(!string.IsNullOrEmpty(DockerVersion) ? " (v" + (DockerVersion) + ")" : string.Empty)}: RUNNING"), true);
                picDocker.Visible = true;

            } else {

                picDockerStatus.Image = Properties.Resources.red;
                tTipPPG.SetToolTip(picDockerStatus, "DOCKER: N/A");
                picDocker.Visible = false;

            }

            // We initially have these set to disabled so user can't fuck w/the context menus until the first check is done. After that they can go HAM on them all they want.
            if (!picWSLStatus.Enabled) { picWSLStatus.Enabled = true; }
            if (!picDockerStatus.Enabled) { picDockerStatus.Enabled = true; }

        }

        private void ToolStripMenuItem_Move_Click(object sender, EventArgs e) {
            // TODO: LEFT OFF: Need to add the actual MOVE code now ...
            // Pluralize if necessary ;)
            //string strMsg = string.Format("Delete {0} {1}?", intCount, intCount == 1 ? "proxy" : "proxies");

            // If a New group is chosen to be created
            if (sender.ToString() == "New ...") {
                GroupRename(true);
            }

            Debug.WriteLine("Move To: " + sender.ToString());

            IEnumerable<ListViewItem> items = listViewProxies.SelectedItems.OfType<ListViewItem>();

            foreach (ListViewItem item in items) {

                Debug.WriteLine("Current: " + item.Group);

                try {

                    Rule rule = ParseRule(item);



                } catch (NotSupportedException ex) {

                    MessageBox.Show(ex.Message, "Scramble the jets!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;

                }
            }
            Services.ParamChange(PortProxyUtil.ServiceName);
        }

        private void toolStripMenuItem_RenameGroup_Click(object sender, EventArgs e) {
            GroupRename(true);
        }

        /// <summary>
        /// "Restart" IpHlpSvc. Technically not a real service restart, but more of a param reload.
        /// </summary>
        private void toolStripMenuItem_ReloadIpHlpSvc_Click(object sender, EventArgs e) {

            // Source: https://github.com/swagfin/PortProxyGUI/commit/af6cfdcafe66883def4a9305149c6a48afbfb8f9

            try {
                Services.ParamChangeWinAPI(PortProxyUtil.ServiceName);
            } catch (Exception ex) {
                MessageBox.Show(ex.Message, $"{PortProxyUtil.ServiceFriendlyName} Restart Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show($"{PortProxyUtil.ServiceFriendlyName} Restarted", $"{PortProxyUtil.ServiceFriendlyName} Restart", MessageBoxButtons.OK, MessageBoxIcon.Information);

        }

        private void picIpHlpSvcStatus_Click(object sender, EventArgs e) {

            if (((MouseEventArgs)e).Button == MouseButtons.Left) {

                // First, check if the service is already running; we only want to do something if it's not.
                Services.IsRunning_BGW((result) => {

                    if (!result) {

                        // Start the service
                        //Debug.WriteLine($"{PortProxyUtil.ServiceName}: Starting");  

                        Services.Start_BGW((result) => {
                            //Debug.WriteLine(result);
                        }, PortProxyUtil.ServiceName);

                    }

                }, PortProxyUtil.ServiceName, false);

            }

        }

        /// <summary>
        /// Copies the Local Machine's IP(s) to the clipboard (ignoring if no IP available)
        /// </summary>
        private void lblCurrentLocalIP_DoubleClick(object sender, EventArgs e) {

            // Copy the IP (only) to the clipboard
            if (!lblCurrentLocalIP.Text.Contains("N/A")) {
                Clipboard.SetText(lblCurrentLocalIP.Text.Replace("LOCAL IP: ", ""));
            }

        }

        /// <summary>
        /// Copies the WSL IP to the clipboard (ignoring if no IP available)
        /// </summary>
        private void lblWSLIP_DoubleClick(object sender, EventArgs e) {

            // Copy the IP (only) to the clipboard
            if (!lblWSLIP.Text.Contains("N/A")) {
                Clipboard.SetText(lblWSLIP.Text.Replace("WSL: ", ""));
            }

        }

        private void picWSLStatus_DoubleClick(object sender, EventArgs e) {

            // As a convenience, show the full WSL Info window on double-click
            ShowWSLInfoWindow();
        }

        private void picWSLStatus_Click(object sender, EventArgs e) {

            if (((MouseEventArgs)e).Button == MouseButtons.Left) {

                if (picWSLStatus.Tag.ToString() == "1") {

                    // WSL already running; hide the "start" item, show Restart & Shutdown.
                    toolStripMenuItemWSLStart.Visible = false;
                    toolStripMenuItemWSLRestart.Visible = true;
                    toolStripMenuItemWSLShutDown.Visible = true;

                } else {

                    // WSL not running; show the "start" item, hide Restart & Shutdown.
                    toolStripMenuItemWSLStart.Visible = true;
                    toolStripMenuItemWSLRestart.Visible = false;
                    toolStripMenuItemWSLShutDown.Visible = false;
                }

                contextMenuStrip_WSL.Show(Cursor.Position.X - 8, Cursor.Position.Y + 12);
            }

        }

        private void toolStripMenuItem_EnableAll_Click(object sender, EventArgs e) {
            ToggleSelectedProxies(true, true);
        }

        private void toolStripMenuItem_DisableAll_Click(object sender, EventArgs e) {
            ToggleSelectedProxies(false, true);
        }

        private void picDockerStatus_DoubleClick(object sender, EventArgs e) {

            // As a convenience, show the full Docker Info window on double-click
            ShowDockerInfoWindow();

        }

        private void picDockerStatus_Click(object sender, EventArgs e) {

            if (((MouseEventArgs)e).Button == MouseButtons.Left) {
                contextMenuStrip_Docker.Show(Cursor.Position.X - 8, Cursor.Position.Y + 12);
            }

        }

        private void ToolStripMenuItem_DockerInfo_Click(object sender, EventArgs e) {
            ShowDockerInfoWindow();
        }

        /// <summary>
        /// Shows the Docker Info in a Dialog Window
        /// </summary>
        private static void ShowDockerInfoWindow() {

            Docker.GetInfo_BGW((DockerInfo) => {
                Dialogs.CustomDialog(DockerInfo.ReplaceLineEndings(), "Docker Info", false, new Size(537, 688));
            });

        }

        private void ToolStripMenuItem_WSLInfo_Click(object sender, EventArgs e) {
            ShowWSLInfoWindow();
        }

        private static void ShowWSLInfoWindow() {

            if (WSL.IsRunning()) {

                StringBuilder sb = new();

                // Get WSL Vesion
                WSL.GetVersion_BGW((strVer) => { if (!string.IsNullOrEmpty(strVer)) { sb.AppendLine($"Version:\t\t{strVer}"); } });

                // TODO: Left off needing to add any other WSL stuff I want, to the dialog.
                // Get other WSL Info
                WSL.GetInfo_BGW((WSLInfo) => {

                    foreach (DictionaryEntry de in WSLInfo) {

                        switch (de.Key.ToString()) {

                            case string _ when de.Key.ToString().Equals("Up Since:"):

                                sb.AppendLine($"{de.Key.ToString()}\t\t{de.Value.ToString()}");
                                break;

                            case string _ when de.Key.ToString().Equals("Up Since: Pretty"):

                                sb.AppendLine($"\t\t{de.Value.ToString()}");
                                break;

                            case string _ when de.Key.ToString().Equals("Users:"):

                                sb.AppendLine($"{de.Key.ToString()}\t\t{de.Value.ToString()}");
                                break;

                            case string _ when de.Key.ToString().Equals("Load Average: Pretty"):

                                sb.AppendLine($"Load Averages:\t{de.Value.ToString()}");
                                break;

                        }

                    }

                    // NOTE: Might wanna validate sb here before opening the window, just to make sure all is ok.
                    // Show the final result in a Dialog Window
                    Dialogs.CustomDialog(sb.ToString(), "WSL Info", false);

                });

            }

        }

        private void toolStripMenuItem_About_Click(object sender, EventArgs e) {

            AboutForm ??= new About(this);
            AboutForm.Show();

        }

        #region + -- CLIPBOARD COPY -- +

        /// <summary>Copies the subitem's text to the clipboard</summary>
        /// <param name="intSubitemNumber">The subitem number from the Listview that you want copied (when not copying as URL)</param>
        /// <param name="intAsURL">1 or 3 = copy item as http URL; 2 or 4 = copy item as https URL. (See Remark for more)</param>
        /// <returns>Either the seleted item as a http/https URL, or the column/cell contents. All are validated as non-empty first;</returns>
        /// <remarks>
        ///   <para>If <font color="#00b050">intAsURL</font> contains 1 or 2, then any value in intSubitemNumber is <em>ignored</em>; pass anything to it. i.e. 0.</para>
        ///   <para>
        ///     <strong>Values for intAsURL</strong>:</para>
        ///   <para>
        ///     <strong>Pass 1 or 2</strong>: to copy the 'Listen On' item as http or https respectively</para>
        ///   <para>
        ///     <strong>Pass 3 or 4</strong>: to copy the 'Listen On' item as http or https respectively</para>
        /// </remarks>
        private void ClipItem(int intSubitemNumber, int intAsURL = 0) {

            try {

                ListViewItem selectedItem = listViewProxies.SelectedItems[0];
                string strFinal = string.Empty;

                if (intAsURL > 0) {

                    // Make sure we have everything we need first
                    if ((intAsURL == 1 || intAsURL == 2) && (!string.IsNullOrEmpty(selectedItem.SubItems[2].Text) && !string.IsNullOrEmpty(selectedItem.SubItems[3].Text)) ||
                        (intAsURL == 3 || intAsURL == 4) && (!string.IsNullOrEmpty(selectedItem.SubItems[4].Text) && !string.IsNullOrEmpty(selectedItem.SubItems[5].Text))) {

                        // We do, so now just stitch it all together for later
                        if (intAsURL == 1 || intAsURL == 2) {

                            // 1 or 2 = Listening IP:Port
                            strFinal = $"{selectedItem.SubItems[2].Text.Replace("0.0.0.0", "localhost").Replace("*", "localhost")}:{selectedItem.SubItems[3].Text}";

                        } else if (intAsURL == 3 || intAsURL == 4) {

                            // 3 or 4 = ConnectTo IP:Port
                            strFinal = $"{selectedItem.SubItems[4].Text.Replace("0.0.0.0", "localhost").Replace("*", "localhost")}:{selectedItem.SubItems[5].Text}";

                        }

                        // http or https?
                        if (intAsURL == 1 || intAsURL == 3) {

                            strFinal = $"http://{strFinal}";

                        } else if (intAsURL == 2 || intAsURL == 4) {

                            strFinal = $"https://{strFinal}";

                        }

                    }

                    // Finally, if everything was GO, we ship it off to the clipboard, and leave this all behind us.
                    if (!string.IsNullOrEmpty(strFinal)) {
                        Clipboard.SetText(strFinal);
                        return;
                    }

                }

                // However, if we're still here at this point, caller must be wanting just the column's contents instead, so viola ...
                if (!string.IsNullOrEmpty(selectedItem.SubItems[intSubitemNumber].Text))
                    Clipboard.SetText(selectedItem.SubItems[intSubitemNumber].Text);

            } catch (Exception ex) {
                Debug.WriteLine($"ClipItem(): {ex.Message}");
            }

        }

        /// <summary>Copies the subitem's text to the clipboard</summary>
        /// <param name="intSubitemNumber">The subitem number from the Listview that you want copied (when not copying as URL)</param>
        /// <param name="intAsURL">1 or 3 = copy item as http URL; 2 or 4 = copy item as https URL. (See Remark for more)</param>
        /// <returns>Either the seleted item as a http/https URL, or the column/cell contents. All are validated as non-empty first;</returns>
        /// <remarks>
        ///   <para>If <font color="#00b050">intAsURL</font> contains 1 or 2, then any value in intSubitemNumber is <em>ignored</em>; pass anything to it. i.e. 0.</para>
        ///   <para>
        ///     <strong>Values for intAsURL</strong>:</para>
        ///   <para>
        ///     <strong>Pass 1 or 2</strong>: to copy the 'Listen On' item as http or https respectively</para>
        ///   <para>
        ///     <strong>Pass 3 or 4</strong>: to copy the 'Listen On' item as http or https respectively</para>
        ///   <para>
        ///     <em>NOTE:</em> This is the ChatGPT version of my own code above, when asked if mine could be optimized. They both do exactly the same thing. Use your choice.</para>
        /// </remarks>
        private void ClipItemAI(int intSubitemNumber, int intAsURL = 0) {

            try {

                ListViewItem selectedItem = listViewProxies.SelectedItems[0];
                string strFinal = string.Empty;

                if (intAsURL > 0) {

                    string ip = string.Empty;
                    string port = string.Empty;

                    // Determine the IP and Port based on intAsURL
                    if ((intAsURL == 1 || intAsURL == 2) && !string.IsNullOrEmpty(selectedItem.SubItems[2].Text) && !string.IsNullOrEmpty(selectedItem.SubItems[3].Text)) {

                        ip = selectedItem.SubItems[2].Text;
                        port = selectedItem.SubItems[3].Text;

                    } else if ((intAsURL == 3 || intAsURL == 4) && !string.IsNullOrEmpty(selectedItem.SubItems[4].Text) && !string.IsNullOrEmpty(selectedItem.SubItems[5].Text)) {

                        ip = selectedItem.SubItems[4].Text;
                        port = selectedItem.SubItems[5].Text;

                    }

                    // Determine the URL prefix
                    string urlPrefix = intAsURL switch {
                        1 => "http://",
                        2 => "https://",
                        3 => "http://",
                        4 => "https://",
                        _ => string.Empty
                    };

                    // Construct the final URL if IP and Port are valid
                    if (!string.IsNullOrEmpty(ip) && !string.IsNullOrEmpty(port)) {
                        strFinal = $"{urlPrefix}{ip}:{port}";
                    }

                    // If everything was good, copy to clipboard and return
                    if (!string.IsNullOrEmpty(strFinal)) {

                        Clipboard.SetText(strFinal);
                        return;

                    }

                }

                // If we're here, just copy the selected column's content to clipboard
                if (!string.IsNullOrEmpty(selectedItem.SubItems[intSubitemNumber].Text)) {
                    Clipboard.SetText(selectedItem.SubItems[intSubitemNumber].Text);
                }

            } catch (Exception ex) {
                Debug.WriteLine($"ClipItemAI(): {ex.Message}");
            }

        }

        private void toolStripMenuItem_ListeningIP_Click(object sender, EventArgs e) {
            ClipItem(2);
        }

        private void toolStripMenuItem_ListeningPort_Click(object sender, EventArgs e) {
            ClipItem(3);
        }

        private void toolStripMenuItem_ConnectToIP_Click(object sender, EventArgs e) {
            ClipItem(4);
        }

        private void toolStripMenuItem_ConnectToPort_Click(object sender, EventArgs e) {
            ClipItem(5);
        }

        private void toolStripMenuItem_Comment_Click(object sender, EventArgs e) {
            ClipItem(6);
        }

        private void toolStripMenuItem_CopyListeningAsURLhttp_Click(object sender, EventArgs e) {
            ClipItem(0, 1);
        }

        private void toolStripMenuItem_CopyListeningAsURLhttps_Click(object sender, EventArgs e) {
            ClipItem(0, 2);
        }

        private void toolStripMenuItem_CopyConnectToAsURLhttp_Click(object sender, EventArgs e) {
            // Using the AI version here just for the helluvit
            ClipItemAI(0, 3);
        }

        private void toolStripMenuItem_CopyConnectToAsURLhttps_Click(object sender, EventArgs e) {
            // Using the AI version here just for the helluvit
            ClipItemAI(0, 4);
        }

        #endregion

        #region + -- OPEN IN BROWSER -- +

        private void toolStripMenuItem_OpenInBrowserHttp_Click(object sender, EventArgs e) {
            OpenInBrowser();
        }

        private void toolStripMenuItem_OpenInBrowserHttps_Click(object sender, EventArgs e) {
            OpenInBrowser(true);
        }

        /// <summary>
        /// Opens the selected item in the browser.
        /// </summary>
        /// <param name="bHTTPS">[optional: default False] If set to <c>true</c> use https</param>
        private void OpenInBrowser(bool bHTTPS = false) {

            try {

                ListViewItem selectedItem = listViewProxies.SelectedItems[0];
                Misc.RunCommand("explorer.exe", $"http{(bHTTPS ? "s" : string.Empty)}://{selectedItem.SubItems[2].Text.Replace("0.0.0.0", "localhost").Replace("*", "localhost")}:{selectedItem.SubItems[3].Text}");

            } catch (Exception ex) {
                Debug.WriteLine($"OpenInBrowserHttp{(bHTTPS ? "s" : string.Empty)}_Click(): {ex.Message}");
            }

        }

        #endregion


        private void toolStripMenuItem_FirewallAdd_Click(object sender, EventArgs e) {

            // TODO: Need to basically add all the stuff to this that I added to the Remove method below

            foreach (ListViewItem listViewItem in listViewProxies.SelectedItems) {

                int intResult = 0;

                // Generate an MD5 hash for this rule to add to it's Rule Name (we'll use it when removing the rule from the firewall, if needed.)
                string strMD5 = Hash.Generate_MD5($"PPGooey{listViewItem.SubItems[3].Text}{listViewItem.SubItems[5].Text}");

                //Debug.WriteLine(Hash.Generate_MD5($"PPGooey{selectedItem.SubItems[3].Text}{selectedItem.SubItems[5].Text}"));

                // Format: [3] = Local Port(s), [5] = Remote Port(s), Rule Name, [6] = Desc., [2] = , [4] = 
                intResult += Firewall.WinFirewall_Rule_Add(listViewItem.SubItems[3].Text, listViewItem.SubItems[5].Text, $"PPGooey (Out) [{strMD5}]", listViewItem.SubItems[6].Text, listViewItem.SubItems[2].Text, listViewItem.SubItems[4].Text, true, true, true, true);
                intResult += Firewall.WinFirewall_Rule_Add(listViewItem.SubItems[3].Text, listViewItem.SubItems[5].Text, $"PPGooey (In) [{strMD5}]", listViewItem.SubItems[6].Text, listViewItem.SubItems[2].Text, listViewItem.SubItems[4].Text, true, false, true, true);

                // Successful Add will result in intResult = 0 (anything over 0 means something failed)
                Debug.WriteLine($"FWA {intResult}");

            }

        }

        private void toolStripMenuItem_FirewallRemove_Click(object sender, EventArgs e) {
            // TODO: Running great so far; just need to decide how to handle mboxes when doing multiples. Also, what if use manually removed 1 of the items from the fw list? intResult would say failed.
            foreach (ListViewItem listViewItem in listViewProxies.SelectedItems) {

                int intResult = 0;

                // Generate an MD5 hash for this rule to match with it's Rule Name
                string strMD5 = Hash.Generate_MD5($"PPGooey{listViewItem.SubItems[3].Text}{listViewItem.SubItems[5].Text}");

                // Keep track of which rule we're deleting
                string strRule = String.Format("{3}{3}Type: {0}{3}Local Port: {1}{3}Remote Port: {2}", listViewItem.SubItems[1].Text, listViewItem.SubItems[3].Text, listViewItem.SubItems[5].Text, Environment.NewLine);

                // Try to delete them
                intResult += Firewall.WinFirewall_Rule_Remove($"PPGooey (Out) [{strMD5}]");
                intResult += Firewall.WinFirewall_Rule_Remove($"PPGooey (In) [{strMD5}]");

                // Successful Removal will result in intResult = 0 (anything over 0 means something failed)
                MessageBox.Show(intResult == 0 ? $"Firewall Rule successfully removed: {strRule}" : $"Firewall Rule failed to be removed: {strRule}", "Delete Firewall Rule", MessageBoxButtons.OK, MessageBoxIcon.Information);

            }

        }

    }

}
