#region + -- IMPORTS -- +

using JSE_Utils;
using NStandard;
using PortProxyGooey.Utils;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Rule = PortProxyGooey.Data.Rule;

#endregion

namespace PortProxyGooey {
    // TODO: since switching tboxes to cboxes some things arent fully working. i.e. port #'s dont inc/dec properly with mousewheel/arrows. Thouroughly examine all stuff.
    public partial class SetProxy : Form {

        #region + -- VAR DECLARATIONS -- +

        public readonly PortProxyGooey ParentWindow;

        private bool _updateMode;
        private ListViewItem _listViewItem;
        private Rule _itemRule;

        // Remembers the last label we added for the user
        private string strLastAutoLabel = string.Empty;

        #endregion

        public SetProxy(PortProxyGooey parent) {

            ParentWindow = parent;

            InitializeComponent();

            // Fetch All Group names from ListViewProxies on main form
            string[] groupNames = (
                from g in parent.listViewProxies.Groups.OfType<ListViewGroup>()
                let header = g.Header
                where !header.IsNullOrWhiteSpace()
                select header
            ).ToArray();

            // Add them to the Groups combobox, but don't add if "WSL" or "Docker" since we've already added them ourself.
            groupNames = Array.FindAll(groupNames, item => item != "Docker" && item != "WSL");
            comboBox_Group.Items.AddRange(groupNames);

            // Set the Default // TODO: "Default" is only getting put in blank/new form; if modding a grp Already in the Defautl grp, it's staying blank.
            comboBox_Group.SelectedIndex = 0;

        }

        private void SetProxyForm_Load(object sender, EventArgs e) {

            this.Cursor = Cursors.WaitCursor;

            Top = ParentWindow.Top + (ParentWindow.Height - Height) / 2;
            Left = ParentWindow.Left + (ParentWindow.Width - Width) / 2;

            // Load up any discovered WSL ports
            WSL.GetListeningPorts_BGW(DiscoveredPorts_Callback);

            this.Cursor = Cursors.Default;
        }

        /// <summary>
        /// Add a New proxy rule, or Clone a proxy rule.
        /// </summary>
        /// <param name="rule">[optional] Rule object. If null, function acts as a brand new empty proxy; if Rule passed, function acts as a clone.</param>
        public void UseNormalMode(Rule rule = null) {

            _updateMode = false;
            _listViewItem = null;
            _itemRule = null;

            // Show "Clone" label?
            lblClone.Visible = rule != null;

            lblType.Text = rule != null ? rule.Type : "v4tov4";
            comboBox_Group.Text = rule != null ? rule.Group : string.Empty;

            comboBox_ListenOn.Text = rule != null ? rule.ListenOn : "*";
            comboBox_ListenPort.Text = rule != null ? rule.ListenPort.ToString() : string.Empty;
            comboBox_ConnectTo.Text = rule != null ? rule.ConnectTo : string.Empty;
            comboBox_ConnectPort.Text = rule != null ? rule.ConnectPort.ToString() : string.Empty;
            textBox_Comment.Text = rule != null ? rule.Comment : string.Empty;

        }

        /// <summary>
        /// Update/Modify an existing proxy
        /// </summary>
        /// <param name="item"></param>
        /// <param name="rule">A populated Rule object</param>
        public void UseUpdateMode(ListViewItem item, Rule rule) {

            _updateMode = true;
            _listViewItem = item;

            _itemRule = rule;

            lblType.Text = rule.Type;
            comboBox_Group.Text = rule.Group;

            comboBox_ListenOn.Text = rule.ListenOn;
            comboBox_ListenPort.Text = rule.ListenPort.ToString();
            comboBox_ConnectTo.Text = rule.ConnectTo;
            comboBox_ConnectPort.Text = rule.ConnectPort.ToString();
            textBox_Comment.Text = rule.Comment;

        }

        /// <summary>
        /// Determine the Type of proxy to pass
        /// </summary>
        /// <param name="listenOn">IP to Listen on</param>
        /// <param name="connectTo">IP to connect to</param>
        /// <returns>Properly formatted proxy Type</returns>
        private static string GetPassType(string listenOn, string connectTo) {

            string from = Network.IsIPv6(listenOn) ? "v6" : "v4";
            string to = Network.IsIPv6(connectTo) ? "v6" : "v4";
            return $"{from}to{to}";

        }

        private void button_Set_Click(object sender, EventArgs e) {

            int listenPort, listenPortRange, connectPort;

            // Don't let em funk with anything while we're doin work
            this.Enabled = false;

            // Validate the Ports
            try {

                listenPort = Rule.ParsePort(comboBox_ListenPort.Text);
                connectPort = Rule.ParsePort(comboBox_ConnectPort.Text);
                listenPortRange = Rule.ParsePort(comboBox_ListenPortRange.Text);

            } catch (NotSupportedException ex) {

                Debug.WriteLine(ex.Message);
                MessageBox.Show("You're trying to set either a bad port, or no port. Do better.", "Uh, no ...", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                progBarRange.Visible = false;
                this.Enabled = true;
                return;

            }

            // If adding a range ...
            if (chkBox_ListenPortRange.Checked && listenPortRange < listenPort) {

                MessageBox.Show("Ending Port is LOWER than the Starting Port", "You need to fix this ...", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

                // Set focus of the 'offending' error target to better help the user understand the source of the issue.
                comboBox_ListenPortRange.Select();
                this.Enabled = true;
                return;

            }

            progBarRange.Visible = true;

            // Do a single trim here rather than multiple trims (save a few cpu cycles)
            string strListen = comboBox_ListenOn.Text.Trim();
            string strConnect = comboBox_ConnectTo.Text.Trim();
            string strGroup = comboBox_Group.Text.Trim();

            // Validate IPv4 (TODO: works great for IP4, but what if we add IP6 in one or both fields?) IP6 regex updated and now working. Look into if it's validation is needed anywwhere else.
            if (!ValidateIPv4(strListen, 1))
                return;
            if (!ValidateIPv4(strConnect, 2))
                return;

            // Add to Rule struct
            Rule rule = new() {

                Type = lblType.Text,
                ListenOn = strListen,
                ListenPort = listenPort,
                ConnectTo = strConnect,
                ConnectPort = connectPort,
                Comment = textBox_Comment.Text.Trim(),
                Group = (strGroup != "Default" ? strGroup : string.Empty),
            };

            // Validate the Proxy Type ...
            //if (rule.Type == AutoTypeString) rule.Type = GetPassType(rule.ListenOn, rule.ConnectTo);

            // Add the rule to the list & db
            if (_updateMode) {

                // Update/Edit/Modify an Existing Rule

                Rule oldRule = Program.Database.GetRule(_itemRule.Type, _itemRule.ListenOn, _itemRule.ListenPort);
                PortProxyUtil.DeleteProxy(oldRule);
                Program.Database.Remove(oldRule);

                PortProxyUtil.AddOrUpdateProxy(rule);
                Program.Database.Add(rule);

                ParentWindow.UpdateListViewItem(_listViewItem, rule, 1);

            } else {

                // Add a New Rule

                int intRange = listenPortRange - listenPort + 1;
                int intDupes = 0;

                progBarRange.Maximum = intRange;

                for (int i = 0; i < intRange; i++) {

                    rule.ListenPort = listenPort;
                    progBarRange.Value += 1;

                    if (DupeCheck(rule)) {

                        // If its a dupe, skip it.
                        intDupes += 1;

                    } else {

                        PortProxyUtil.AddOrUpdateProxy(rule);
                        Program.Database.Add(rule);

                    }

                    listenPort++;
                }

                // Alert the user if any were skipped due to duping
                if (intDupes > 0)
                    MessageBox.Show($"{intDupes} duplicates skipped.", "Didn't add some ...", MessageBoxButtons.OK, MessageBoxIcon.Information);

            }

            ParentWindow.RefreshProxyList();
            ParentWindow.UpdateProxyCount();

            // Set .EnsureVisible and .Selected to the newest item added, as a convenience.
            // (Searches listViewProxies .tags for the rule.id)
            ListViewItem item = ParentWindow.listViewProxies.Items
                        .Cast<ListViewItem>()
                        .FirstOrDefault(i => i.Tag != null && i.Tag.ToString().Contains(rule.Id));

            if (item != null) {

                ParentWindow.listViewProxies.EnsureVisible(item.Index);
                ParentWindow.listViewProxies.Items[item.Index].Selected = true;

            }

            Services.ParamChange(PortProxyUtil.ServiceName);

            progBarRange.Visible = false;
            this.Enabled = true;
            Close();
        }

        private void SetProxyForm_FormClosing(object sender, FormClosingEventArgs e) {
            ParentWindow.SetProxyForm = null;
        }

        private void chkBox_ListenPortRange_CheckedChanged(object sender, EventArgs e) {

            if (chkBox_ListenPortRange.Checked) {

                lblDash.Visible = true;
                comboBox_ListenPortRange.Visible = true;
                lblRangeCount.Visible = true;
                lblRangeCount.Text = string.IsNullOrEmpty(comboBox_ListenPortRange.Text) ? "Adding: 0" : $"Adding: {CalcRange()}";

            } else {

                lblDash.Visible = false;
                comboBox_ListenPortRange.Visible = false;
                lblRangeCount.Visible = false;

            }

        }

        private void comboBox_ListenPort_TextChanged(object sender, EventArgs e) {

            // Add the same port to the range box as a starting point
            comboBox_ListenPortRange.Text = comboBox_ListenPort.Text;

            // Add the same port to the connect port box as a starting point
            comboBox_ConnectPort.Text = comboBox_ListenPort.Text;

            // If it's a dupe port show the label; else hide it.
            lblDupe.Visible = DupeCheck();

            // Auto-comment common ports
            AutoComment(comboBox_ListenPort);

        }

        private void comboBox_ListenPortRange_TextChanged(object sender, EventArgs e) {

            // TODO: I think I can create a single Call for this sub and line 260 called something like "UpdateRangeLabel"
            int intRangeCount = CalcRange();
            string strBase = "Adding:";

            lblRangeCount.Text = intRangeCount < 0 ? strBase + " 0" : $"{strBase} {intRangeCount}";

            // Auto-comment common ports
            AutoComment(comboBox_ListenPortRange);

        }

        private void comboBox_ConnectPort_TextChanged(object sender, EventArgs e) {

            // Auto-comment common ports
            AutoComment(comboBox_ConnectPort);

        }

        /// <summary>
        /// Calculates how many ports will be added based on the values in the port fields
        /// </summary>
        /// <returns>Number of ports</returns>
        private int CalcRange() {

            // Make sure we have something to calc first, or else error.
            if (!string.IsNullOrWhiteSpace(comboBox_ListenPortRange.Text) && !string.IsNullOrWhiteSpace(comboBox_ListenPort.Text)) {

                int intLPR = Convert.ToInt32(comboBox_ListenPortRange.Text.Trim());
                int intLP = Convert.ToInt32(comboBox_ListenPort.Text.Trim());
                return ((intLPR - intLP) + 1);

            }
            return 0;
        }

        /// <summary>
        /// Validates the IPv4 fields (allows exception for an asterisk); sets focus to invalid field(s)
        /// </summary>
        /// <param name="strIP">IPv4 string to check</param>
        /// <param name="intField">Field to focus back on in case of a failure. 2: ConnectTo, any other int:ListenOn.</param>
        /// <returns>True if valid; False if invalid</returns>
        private bool ValidateIPv4(string strIP, int intField) {

            bool bResult = true;

            if (!Network.IsIPv4(strIP) && strIP != "*") {

                MessageBox.Show($"{strIP} is not a valid IP", "What are you up to here?", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

                if (intField == 2) {

                    // The "ConnectTo" field
                    bResult = false;
                    comboBox_ConnectTo.Select();

                } else {

                    // The "ListenOn" field
                    bResult = false;
                    comboBox_ListenOn.Select();

                }

                progBarRange.Visible = bResult;
                this.Enabled = true;

            }
            return bResult;
        }

        /// <summary>
        /// Automatically enters info in the Comment field for recognized ports.
        /// </summary>
        private void AutoComment(System.Windows.Forms.ComboBox comboBox) {

            // Ref: https://en.wikipedia.org/wiki/List_of_TCP_and_UDP_port_numbers

            Dictionary<string, string> port = new() {
                {"DNS", "53"},
                {"Docker (SSL)", "2376"},
                {"Docker", "2375"},
                {"FTP", "21"},
                {"Glances", "61208"},
                {"HTTP", "80"},
                {"HTTPS", "443"},
                {"IMAP", "143"},
                {"IRC", "194"},
                {"Jellyfin", "8920"},
                {"Jenkens", "33848"},
                {"Minecraft", "25565"},
                {"MSSQL", "1433"},
                {"MySQL/MariaDB", "3306"},
                {"NetBIOS", "139"},
                {"PCAnywhere", "5632"},
                {"PGAdmin", "5432"},
                {"Plex", "32400"},
                {"POP3", "110"},
                {"Postgres", "5050"},
                {"Prometheus", "9090"},
                {"RDP", "3389"},
                {"RPC", "135"},
                {"sFTP", "115"},
                {"SMB", "445"},
                {"SMTP", "25"},
                {"SSH", "22"},
                {"Syncthing", "8384,22000"},
                {"TeamSpeak", "10011,10022,30033"},
                {"Telnet", "23"},
                {"TetriNET", "31457"},
                {"Transmission", "9091"},
                {"VNC", "5900"},
            };

            // TODO: Add more ports, i.e. the *arr and other docker things.
            // TODO: Add saving of below option to db
            if (chkAutoComment.Checked) {

                // Auto-Labeling of Common Ports
                string searchValue = comboBox.Text.Trim();
                string matchingKey = port.FirstOrDefault(x => x.Value.Split(',').Contains(searchValue)).Key;

                if (matchingKey != null) {

                    // If user enters a common port, give it an auto-label as a convenience to them. Non-destructive: this will not delete anything they have manually typed in the comment field.
                    //Debug.WriteLine($"The key for value '{searchValue}' is '{matchingKey}'.");
                    textBox_Comment.Text = $"[{matchingKey}] {(string.IsNullOrEmpty(strLastAutoLabel) ? textBox_Comment.Text.Trim() : textBox_Comment.Text.Replace(strLastAutoLabel, string.Empty).Trim())}";
                    strLastAutoLabel = $"[{matchingKey}]";

                } else {

                    // If no matching port found, just leave whatever text they may have entered, removing any previous auto-label if exists.
                    //Debug.WriteLine($"No key found for value '{searchValue}'.");
                    textBox_Comment.Text = string.IsNullOrEmpty(strLastAutoLabel) ? textBox_Comment.Text.Trim() : textBox_Comment.Text.Replace(strLastAutoLabel, string.Empty).Trim();

                }
            }
        }

        /// <summary>
        /// Compares the respective items in the listview w/the entered data, looking for it already existing in the list.
        /// </summary>
        /// <param name="rule">[optional] rule list. If not passed, will read currently entered form fields.</param>
        /// <returns>True if Dupe found; False if no Dupe.</returns>
        private bool DupeCheck([Optional] Rule rule) {

            // + ------------------------------------------------------------------------------------------------------------------- +
            // | NOTES:                                                                                                              |
            // |   - I haven't thought through all possible dupe scenarios, so they may or may not still need some tweaking.         |
            // |   - Only checks Type & Listen Fields, because once you're already listening on an IP + Port Number + Specific Type  |
            // |     it doesn't matter if you change the ConnectTo IP of the same Type, because you can only listen on 1 at a time;  |
            // |     i.e. 0.0.0.0 Port 53 4to4 can only be changed to something like 0.0.0.0 53 ::1 4to6                             |
            // + ------------------------------------------------------------------------------------------------------------------- +

            string strType = rule != null ? rule.Type : lblType.Text;
            string strListen = rule != null ? rule.ListenOn : comboBox_ListenOn.Text.Trim();
            string strListenPort = rule != null ? rule.ListenPort.ToString() : comboBox_ListenPort.Text.Trim();

            bool bResult = false;

            foreach (ListViewItem item in ParentWindow.listViewProxies.Items) {

                if (item.SubItems[1].Text.Equals(strType) &&
                    item.SubItems[2].Text.Equals(strListen) &&
                    item.SubItems[3].Text.Equals(strListenPort)) {
                        // If dupe port found, flag it.
                        bResult = true;
                        break;
                }
            }
            return bResult;
        }

        /// <summary>
        /// Auto-selects the correct Type based on what the user types into the ListenOn/ConnectTo fields
        /// </summary>
        private void TypeCheck() {
            lblType.Text = GetPassType(comboBox_ListenOn.Text.Trim(), comboBox_ConnectTo.Text.Trim());
        }

        private void comboBox_ConnectTo_TextChanged(object sender, EventArgs e) {
            TypeCheck();
        }

        private void comboBox_ListenOn_TextChanged(object sender, EventArgs e) {

            lblDupe.Visible = DupeCheck();
            TypeCheck();

        }

        /// <summary>
        /// Discoverd Ports BGW callback. Adds the ports to the respective controls.
        /// </summary>
        /// <param name="portsDic">Dictionary containing any results</param>
        private void DiscoveredPorts_Callback(Dictionary<string, string> portsDic) {

            if (portsDic != null) {

                // We keep these boxes and labels hidden unless we have something to put in them
                lblWSLDiscovered.Visible = true;
                listBoxIP4.Visible = true;
                listBoxIP6.Visible = true;
                lblDiscoveredIP4.Visible = true;
                lblDiscoveredIP6.Visible = true;

                foreach (var kvp in portsDic) {

                    // Add ports to their respective ListBoxes
                    if (kvp.Key.Contains("IPv4")) {

                        listBoxIP4.Items.Add(kvp.Value);

                    } else if (kvp.Key.Contains("IPv6")) {

                        listBoxIP6.Items.Add(kvp.Value);
                    }

                    // Also add to autocomplete stuff to the ComboBoxes as well
                    if (!comboBox_ListenPort.AutoCompleteCustomSource.Contains(kvp.Value)) {

                        // Autocomplete
                        comboBox_ListenPort.AutoCompleteCustomSource.Add(kvp.Value);
                        comboBox_ListenPortRange.AutoCompleteCustomSource.Add(kvp.Value);
                        comboBox_ConnectPort.AutoCompleteCustomSource.Add(kvp.Value);

                        // Items list
                        comboBox_ListenPort.Items.Add(kvp.Value);
                        comboBox_ListenPortRange.Items.Add(kvp.Value);
                        comboBox_ConnectPort.Items.Add(kvp.Value);

                    }

                }

                // Sort Numerically
                ComboBoxes.SortItemsNumerically(comboBox_ListenPort);
                ComboBoxes.SortItemsNumerically(comboBox_ListenPortRange);
                ComboBoxes.SortItemsNumerically(comboBox_ConnectPort);
                ListBoxes.SortItemsNumerically(listBoxIP4);
                ListBoxes.SortItemsNumerically(listBoxIP6);

                lblDiscoveredIP4.Text = $"IP4 ({listBoxIP4.Items.Count})";
                lblDiscoveredIP6.Text = $"IP6 ({listBoxIP6.Items.Count})";
            }

        }

        private void btnCancel_Click(object sender, EventArgs e) {
            this.Close();
        }

        #region ComboBox Increase / Decrease

        private void comboBox_ListenPortRange_MouseWheel(object sender, MouseEventArgs e) {
            bool bUp = (e.Delta > 0);
            IncreaseDecrease(bUp, comboBox_ListenPortRange);
        }

        private void comboBox_ListenPort_MouseWheel(object sender, MouseEventArgs e) {
            bool bUp = (e.Delta > 0);
            IncreaseDecrease(bUp, comboBox_ListenPort);
        }

        private void comboBox_ConnectPort_MouseWheel(object sender, MouseEventArgs e) {
            bool bUp = (e.Delta > 0);
            IncreaseDecrease(bUp, comboBox_ConnectPort);
        }

        private void comboBox_ConnectPort_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode.Equals(Keys.Up)) {
                IncreaseDecrease(true, comboBox_ConnectPort);
            } else if (e.KeyCode.Equals(Keys.Down)) {
                IncreaseDecrease(false, comboBox_ConnectPort);
            }
        }

        private void comboBox_ListenPort_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode.Equals(Keys.Up)) {
                IncreaseDecrease(true, comboBox_ListenPort);
            } else if (e.KeyCode.Equals(Keys.Down)) {
                IncreaseDecrease(false, comboBox_ListenPort);
            }
        }

        private void comboBox_ListenPortRange_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode.Equals(Keys.Up)) {
                IncreaseDecrease(true, comboBox_ListenPortRange);
            } else if (e.KeyCode.Equals(Keys.Down)) {
                IncreaseDecrease(false, comboBox_ListenPortRange);
            }
        }

        /// <summary>
        /// Increase / Decrease the number in a Textbox
        /// </summary>
        /// <param name="bUp">[boolean] true = Up; false = Down</param>
        /// <param name="textBox">A Textbox control</param>
        private static void IncreaseDecrease(bool bUp, System.Windows.Forms.ComboBox comboBox) {

            if (string.IsNullOrEmpty(comboBox.Text))
                comboBox.Text = "0";

            int intCurrentVal = Convert.ToInt32(comboBox.Text);

            if (bUp) {
                intCurrentVal++;
            } else {
                // Don't allow negative numbers
                if (intCurrentVal != 0)
                    intCurrentVal--;
            }

            comboBox.Text = intCurrentVal.ToString();

            // Move the cursor to the end, or else it just looks weird to me.
            //comboBox.SelectionStart = comboBox.TextLength;
            //comboBox.SelectionLength = 0;
        }

        #endregion

        #region ComboBox KeyPress

        private void comboBox_ListenPort_KeyPress(object sender, KeyPressEventArgs e) {
            DigitsOnly(e);
        }

        private void comboBox_ConnectPort_KeyPress(object sender, KeyPressEventArgs e) {
            DigitsOnly(e);
        }

        private void comboBox_ListenPortRange_KeyPress(object sender, KeyPressEventArgs e) {
            DigitsOnly(e);
        }

        /// <summary>
        /// Cancels the event if anything other than a number is entered
        /// </summary>
        private static void DigitsOnly(KeyPressEventArgs e) {

            if (!char.IsDigit(e.KeyChar) && e.KeyChar != '\b') {
                e.Handled = true;
            }

        }

        #endregion

        #region ComboBox KeyPress

        private void comboBox_ListenOn_KeyPress(object sender, KeyPressEventArgs e) {

            // Check if the Ctrl + V key combination is pressed
            if ((Control.ModifierKeys & Keys.Control) == Keys.Control && e.KeyChar == 22) {

                // Allow pasting
                comboBox_ListenOn.Text = Clipboard.GetText();
                e.Handled = true;

            } else {
                OnlyCertainAllowed(e);
            }

        }

        private void comboBox_ConnectTo_KeyPress(object sender, KeyPressEventArgs e) {

            // Check if the Ctrl + V key combination is pressed
            if ((Control.ModifierKeys & Keys.Control) == Keys.Control && e.KeyChar == 22) {

                // Allow pasting
                comboBox_ConnectTo.Text = Clipboard.GetText();
                e.Handled = true;

            } else {
                OnlyCertainAllowed(e);
            }

        }

        /// <summary>
        /// Only numbers, periods, colons, backspace, and asterisk allowed.
        /// </summary>
        private static void OnlyCertainAllowed(KeyPressEventArgs e) {

            if (!char.IsDigit(e.KeyChar) && e.KeyChar != '.' && e.KeyChar != '*' && e.KeyChar != ':' && e.KeyChar != '\b') {
                e.Handled = true;
            }

        }

        #endregion

        /// <summary>
        /// Put ListBox port into the ListenPort ComboBox Text IP4 field on DoubleClick.
        /// </summary>
        private void listBoxIP4_DoubleClick(object sender, EventArgs e) {
            comboBox_ListenPort.Text = listBoxIP4.Text;
        }

        /// <summary>
        /// Put ListBox port into the ListenPort ComboBox Text IP6 field on DoubleClick.
        /// </summary>
        private void listBoxIP6_DoubleClick(object sender, EventArgs e) {
            comboBox_ListenPort.Text = listBoxIP6.Text;
        }

        private void tmrGeneral_Tick(object sender, EventArgs e) {

            // Current WSL IP
            lblWSLIP.Visible = true;

            if (!ParentWindow.lblWSLIP.Text.Contains("N/A")) {

                string strIPClean = ParentWindow.lblWSLIP.Text.Replace("WSL: ", "");

                // Only change/add these things if not already there, to avoid dupes/flicker, etc.
                if (lblWSLIP.Text != ParentWindow.lblWSLIP.Text) lblWSLIP.Text = ParentWindow.lblWSLIP.Text;                                         // Label
                if (!comboBox_ConnectTo.Items.Contains(strIPClean)) comboBox_ConnectTo.Items.Add(strIPClean);                                        // Add WSL IP to Items List
                if (!comboBox_ConnectTo.AutoCompleteCustomSource.Contains(strIPClean)) comboBox_ConnectTo.AutoCompleteCustomSource.Add(strIPClean);  // '           ' Autocomplete
                if (!comboBox_ListenOn.AutoCompleteCustomSource.Contains(strIPClean)) comboBox_ListenOn.AutoCompleteCustomSource.Add(strIPClean);    // '           ' Autocomplete

            } else {
                lblWSLIP.Text = "WSL: Dunno";
            }

        }
    }
}
