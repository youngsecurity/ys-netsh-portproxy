using System.Windows.Forms;

namespace PortProxyGooey {
    partial class SetProxy {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SetProxy));
            label_ListenOn = new Label();
            label_ConnectTo = new Label();
            label_ConnectPort = new Label();
            button_Set = new Button();
            label_ListenPort = new Label();
            label_Comment = new Label();
            textBox_Comment = new TextBox();
            label_Group = new Label();
            comboBox_Group = new ComboBox();
            comboBox_ListenOn = new ComboBox();
            lblDash = new Label();
            chkBox_ListenPortRange = new CheckBox();
            lblRequired = new Label();
            tTipSetProxy = new ToolTip(components);
            lblWSLIP = new Label();
            lblType = new Label();
            lblRangeCount = new Label();
            progBarRange = new ProgressBar();
            chkAutoComment = new CheckBox();
            lblWSLDiscovered = new Label();
            lblDiscoveredIP6 = new Label();
            lblDiscoveredIP4 = new Label();
            comboBox_ListenPortRange = new ComboBox();
            comboBox_ConnectTo = new ComboBox();
            lblDupe = new Label();
            btnCancel = new Button();
            lblClone = new Label();
            listBoxIP4 = new ListBox();
            listBoxIP6 = new ListBox();
            pnlTop = new Panel();
            pnlBottom = new Panel();
            comboBox_ListenPort = new ComboBox();
            comboBox_ConnectPort = new ComboBox();
            tmrGeneral = new Timer(components);
            pnlTop.SuspendLayout();
            SuspendLayout();
            // 
            // label_ListenOn
            // 
            resources.ApplyResources(label_ListenOn, "label_ListenOn");
            label_ListenOn.Cursor = Cursors.Help;
            label_ListenOn.ForeColor = System.Drawing.Color.FromArgb(191, 97, 106);
            label_ListenOn.Name = "label_ListenOn";
            tTipSetProxy.SetToolTip(label_ListenOn, resources.GetString("label_ListenOn.ToolTip"));
            // 
            // label_ConnectTo
            // 
            resources.ApplyResources(label_ConnectTo, "label_ConnectTo");
            label_ConnectTo.Cursor = Cursors.Help;
            label_ConnectTo.ForeColor = System.Drawing.Color.FromArgb(191, 97, 106);
            label_ConnectTo.Name = "label_ConnectTo";
            tTipSetProxy.SetToolTip(label_ConnectTo, resources.GetString("label_ConnectTo.ToolTip"));
            // 
            // label_ConnectPort
            // 
            resources.ApplyResources(label_ConnectPort, "label_ConnectPort");
            label_ConnectPort.Cursor = Cursors.Help;
            label_ConnectPort.ForeColor = System.Drawing.Color.FromArgb(191, 97, 106);
            label_ConnectPort.Name = "label_ConnectPort";
            tTipSetProxy.SetToolTip(label_ConnectPort, resources.GetString("label_ConnectPort.ToolTip"));
            // 
            // button_Set
            // 
            resources.ApplyResources(button_Set, "button_Set");
            button_Set.BackColor = System.Drawing.Color.FromArgb(41, 46, 57);
            button_Set.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(67, 76, 94);
            button_Set.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(59, 66, 82);
            button_Set.ForeColor = System.Drawing.Color.FromArgb(235, 203, 139);
            button_Set.Name = "button_Set";
            button_Set.UseVisualStyleBackColor = false;
            button_Set.Click += button_Set_Click;
            // 
            // label_ListenPort
            // 
            resources.ApplyResources(label_ListenPort, "label_ListenPort");
            label_ListenPort.Cursor = Cursors.Help;
            label_ListenPort.ForeColor = System.Drawing.Color.FromArgb(191, 97, 106);
            label_ListenPort.Name = "label_ListenPort";
            tTipSetProxy.SetToolTip(label_ListenPort, resources.GetString("label_ListenPort.ToolTip"));
            // 
            // label_Comment
            // 
            resources.ApplyResources(label_Comment, "label_Comment");
            label_Comment.Cursor = Cursors.Help;
            label_Comment.ForeColor = System.Drawing.Color.FromArgb(216, 222, 233);
            label_Comment.Name = "label_Comment";
            tTipSetProxy.SetToolTip(label_Comment, resources.GetString("label_Comment.ToolTip"));
            // 
            // textBox_Comment
            // 
            resources.ApplyResources(textBox_Comment, "textBox_Comment");
            textBox_Comment.AutoCompleteCustomSource.AddRange(new string[] { resources.GetString("textBox_Comment.AutoCompleteCustomSource"), resources.GetString("textBox_Comment.AutoCompleteCustomSource1"), resources.GetString("textBox_Comment.AutoCompleteCustomSource2"), resources.GetString("textBox_Comment.AutoCompleteCustomSource3") });
            textBox_Comment.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            textBox_Comment.AutoCompleteSource = AutoCompleteSource.CustomSource;
            textBox_Comment.BackColor = System.Drawing.Color.FromArgb(41, 46, 57);
            textBox_Comment.BorderStyle = BorderStyle.FixedSingle;
            textBox_Comment.ForeColor = System.Drawing.Color.FromArgb(229, 233, 240);
            textBox_Comment.Name = "textBox_Comment";
            // 
            // label_Group
            // 
            resources.ApplyResources(label_Group, "label_Group");
            label_Group.Cursor = Cursors.Help;
            label_Group.ForeColor = System.Drawing.Color.FromArgb(216, 222, 233);
            label_Group.Name = "label_Group";
            tTipSetProxy.SetToolTip(label_Group, resources.GetString("label_Group.ToolTip"));
            // 
            // comboBox_Group
            // 
            resources.ApplyResources(comboBox_Group, "comboBox_Group");
            comboBox_Group.AutoCompleteCustomSource.AddRange(new string[] { resources.GetString("comboBox_Group.AutoCompleteCustomSource"), resources.GetString("comboBox_Group.AutoCompleteCustomSource1"), resources.GetString("comboBox_Group.AutoCompleteCustomSource2"), resources.GetString("comboBox_Group.AutoCompleteCustomSource3"), resources.GetString("comboBox_Group.AutoCompleteCustomSource4"), resources.GetString("comboBox_Group.AutoCompleteCustomSource5"), resources.GetString("comboBox_Group.AutoCompleteCustomSource6") });
            comboBox_Group.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            comboBox_Group.AutoCompleteSource = AutoCompleteSource.CustomSource;
            comboBox_Group.BackColor = System.Drawing.Color.FromArgb(41, 46, 57);
            comboBox_Group.ForeColor = System.Drawing.Color.FromArgb(229, 233, 240);
            comboBox_Group.FormattingEnabled = true;
            comboBox_Group.Items.AddRange(new object[] { resources.GetString("comboBox_Group.Items"), resources.GetString("comboBox_Group.Items1"), resources.GetString("comboBox_Group.Items2") });
            comboBox_Group.Name = "comboBox_Group";
            comboBox_Group.Sorted = true;
            // 
            // comboBox_ListenOn
            // 
            resources.ApplyResources(comboBox_ListenOn, "comboBox_ListenOn");
            comboBox_ListenOn.AutoCompleteCustomSource.AddRange(new string[] { resources.GetString("comboBox_ListenOn.AutoCompleteCustomSource"), resources.GetString("comboBox_ListenOn.AutoCompleteCustomSource1"), resources.GetString("comboBox_ListenOn.AutoCompleteCustomSource2"), resources.GetString("comboBox_ListenOn.AutoCompleteCustomSource3"), resources.GetString("comboBox_ListenOn.AutoCompleteCustomSource4"), resources.GetString("comboBox_ListenOn.AutoCompleteCustomSource5"), resources.GetString("comboBox_ListenOn.AutoCompleteCustomSource6"), resources.GetString("comboBox_ListenOn.AutoCompleteCustomSource7") });
            comboBox_ListenOn.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            comboBox_ListenOn.AutoCompleteSource = AutoCompleteSource.CustomSource;
            comboBox_ListenOn.BackColor = System.Drawing.Color.FromArgb(41, 46, 57);
            comboBox_ListenOn.ForeColor = System.Drawing.Color.FromArgb(229, 233, 240);
            comboBox_ListenOn.FormattingEnabled = true;
            comboBox_ListenOn.Items.AddRange(new object[] { resources.GetString("comboBox_ListenOn.Items"), resources.GetString("comboBox_ListenOn.Items1"), resources.GetString("comboBox_ListenOn.Items2"), resources.GetString("comboBox_ListenOn.Items3"), resources.GetString("comboBox_ListenOn.Items4"), resources.GetString("comboBox_ListenOn.Items5"), resources.GetString("comboBox_ListenOn.Items6"), resources.GetString("comboBox_ListenOn.Items7"), resources.GetString("comboBox_ListenOn.Items8") });
            comboBox_ListenOn.Name = "comboBox_ListenOn";
            comboBox_ListenOn.TextChanged += comboBox_ListenOn_TextChanged;
            comboBox_ListenOn.KeyPress += comboBox_ListenOn_KeyPress;
            // 
            // lblDash
            // 
            resources.ApplyResources(lblDash, "lblDash");
            lblDash.ForeColor = System.Drawing.Color.FromArgb(216, 222, 233);
            lblDash.Name = "lblDash";
            // 
            // chkBox_ListenPortRange
            // 
            resources.ApplyResources(chkBox_ListenPortRange, "chkBox_ListenPortRange");
            chkBox_ListenPortRange.Cursor = Cursors.Help;
            chkBox_ListenPortRange.ForeColor = System.Drawing.Color.FromArgb(216, 222, 233);
            chkBox_ListenPortRange.Name = "chkBox_ListenPortRange";
            tTipSetProxy.SetToolTip(chkBox_ListenPortRange, resources.GetString("chkBox_ListenPortRange.ToolTip"));
            chkBox_ListenPortRange.UseVisualStyleBackColor = true;
            chkBox_ListenPortRange.CheckedChanged += chkBox_ListenPortRange_CheckedChanged;
            // 
            // lblRequired
            // 
            resources.ApplyResources(lblRequired, "lblRequired");
            lblRequired.Cursor = Cursors.Help;
            lblRequired.ForeColor = System.Drawing.Color.FromArgb(191, 97, 106);
            lblRequired.Name = "lblRequired";
            tTipSetProxy.SetToolTip(lblRequired, resources.GetString("lblRequired.ToolTip"));
            // 
            // tTipSetProxy
            // 
            tTipSetProxy.BackColor = System.Drawing.Color.FromArgb(235, 203, 139);
            // 
            // lblWSLIP
            // 
            resources.ApplyResources(lblWSLIP, "lblWSLIP");
            lblWSLIP.BackColor = System.Drawing.Color.FromArgb(41, 46, 57);
            lblWSLIP.Cursor = Cursors.Help;
            lblWSLIP.ForeColor = System.Drawing.Color.FromArgb(129, 161, 193);
            lblWSLIP.Name = "lblWSLIP";
            tTipSetProxy.SetToolTip(lblWSLIP, resources.GetString("lblWSLIP.ToolTip"));
            // 
            // lblType
            // 
            resources.ApplyResources(lblType, "lblType");
            lblType.BackColor = System.Drawing.Color.FromArgb(41, 46, 57);
            lblType.Cursor = Cursors.Help;
            lblType.ForeColor = System.Drawing.Color.FromArgb(143, 188, 187);
            lblType.Name = "lblType";
            tTipSetProxy.SetToolTip(lblType, resources.GetString("lblType.ToolTip"));
            // 
            // lblRangeCount
            // 
            resources.ApplyResources(lblRangeCount, "lblRangeCount");
            lblRangeCount.BackColor = System.Drawing.Color.FromArgb(41, 46, 57);
            lblRangeCount.Cursor = Cursors.Help;
            lblRangeCount.ForeColor = System.Drawing.Color.FromArgb(163, 190, 140);
            lblRangeCount.Name = "lblRangeCount";
            tTipSetProxy.SetToolTip(lblRangeCount, resources.GetString("lblRangeCount.ToolTip"));
            // 
            // progBarRange
            // 
            resources.ApplyResources(progBarRange, "progBarRange");
            progBarRange.BackColor = System.Drawing.Color.FromArgb(41, 46, 57);
            progBarRange.Cursor = Cursors.Help;
            progBarRange.ForeColor = System.Drawing.Color.FromArgb(180, 142, 173);
            progBarRange.Name = "progBarRange";
            progBarRange.Step = 1;
            progBarRange.Style = ProgressBarStyle.Continuous;
            tTipSetProxy.SetToolTip(progBarRange, resources.GetString("progBarRange.ToolTip"));
            // 
            // chkAutoComment
            // 
            resources.ApplyResources(chkAutoComment, "chkAutoComment");
            chkAutoComment.Checked = true;
            chkAutoComment.CheckState = CheckState.Checked;
            chkAutoComment.Cursor = Cursors.Help;
            chkAutoComment.ForeColor = System.Drawing.Color.FromArgb(216, 222, 233);
            chkAutoComment.Name = "chkAutoComment";
            tTipSetProxy.SetToolTip(chkAutoComment, resources.GetString("chkAutoComment.ToolTip"));
            chkAutoComment.UseVisualStyleBackColor = true;
            // 
            // lblWSLDiscovered
            // 
            resources.ApplyResources(lblWSLDiscovered, "lblWSLDiscovered");
            lblWSLDiscovered.Cursor = Cursors.Help;
            lblWSLDiscovered.ForeColor = System.Drawing.Color.FromArgb(143, 188, 187);
            lblWSLDiscovered.Name = "lblWSLDiscovered";
            tTipSetProxy.SetToolTip(lblWSLDiscovered, resources.GetString("lblWSLDiscovered.ToolTip"));
            // 
            // lblDiscoveredIP6
            // 
            resources.ApplyResources(lblDiscoveredIP6, "lblDiscoveredIP6");
            lblDiscoveredIP6.Cursor = Cursors.Help;
            lblDiscoveredIP6.ForeColor = System.Drawing.Color.FromArgb(129, 161, 193);
            lblDiscoveredIP6.Name = "lblDiscoveredIP6";
            tTipSetProxy.SetToolTip(lblDiscoveredIP6, resources.GetString("lblDiscoveredIP6.ToolTip"));
            // 
            // lblDiscoveredIP4
            // 
            resources.ApplyResources(lblDiscoveredIP4, "lblDiscoveredIP4");
            lblDiscoveredIP4.Cursor = Cursors.Help;
            lblDiscoveredIP4.ForeColor = System.Drawing.Color.FromArgb(129, 161, 193);
            lblDiscoveredIP4.Name = "lblDiscoveredIP4";
            tTipSetProxy.SetToolTip(lblDiscoveredIP4, resources.GetString("lblDiscoveredIP4.ToolTip"));
            // 
            // comboBox_ListenPortRange
            // 
            resources.ApplyResources(comboBox_ListenPortRange, "comboBox_ListenPortRange");
            comboBox_ListenPortRange.AutoCompleteCustomSource.AddRange(new string[] { resources.GetString("comboBox_ListenPortRange.AutoCompleteCustomSource"), resources.GetString("comboBox_ListenPortRange.AutoCompleteCustomSource1"), resources.GetString("comboBox_ListenPortRange.AutoCompleteCustomSource2"), resources.GetString("comboBox_ListenPortRange.AutoCompleteCustomSource3"), resources.GetString("comboBox_ListenPortRange.AutoCompleteCustomSource4"), resources.GetString("comboBox_ListenPortRange.AutoCompleteCustomSource5"), resources.GetString("comboBox_ListenPortRange.AutoCompleteCustomSource6"), resources.GetString("comboBox_ListenPortRange.AutoCompleteCustomSource7"), resources.GetString("comboBox_ListenPortRange.AutoCompleteCustomSource8"), resources.GetString("comboBox_ListenPortRange.AutoCompleteCustomSource9"), resources.GetString("comboBox_ListenPortRange.AutoCompleteCustomSource10"), resources.GetString("comboBox_ListenPortRange.AutoCompleteCustomSource11"), resources.GetString("comboBox_ListenPortRange.AutoCompleteCustomSource12"), resources.GetString("comboBox_ListenPortRange.AutoCompleteCustomSource13") });
            comboBox_ListenPortRange.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            comboBox_ListenPortRange.AutoCompleteSource = AutoCompleteSource.CustomSource;
            comboBox_ListenPortRange.BackColor = System.Drawing.Color.FromArgb(41, 46, 57);
            comboBox_ListenPortRange.ForeColor = System.Drawing.Color.FromArgb(229, 233, 240);
            comboBox_ListenPortRange.FormattingEnabled = true;
            comboBox_ListenPortRange.Items.AddRange(new object[] { resources.GetString("comboBox_ListenPortRange.Items"), resources.GetString("comboBox_ListenPortRange.Items1"), resources.GetString("comboBox_ListenPortRange.Items2"), resources.GetString("comboBox_ListenPortRange.Items3"), resources.GetString("comboBox_ListenPortRange.Items4"), resources.GetString("comboBox_ListenPortRange.Items5"), resources.GetString("comboBox_ListenPortRange.Items6"), resources.GetString("comboBox_ListenPortRange.Items7"), resources.GetString("comboBox_ListenPortRange.Items8"), resources.GetString("comboBox_ListenPortRange.Items9"), resources.GetString("comboBox_ListenPortRange.Items10"), resources.GetString("comboBox_ListenPortRange.Items11"), resources.GetString("comboBox_ListenPortRange.Items12"), resources.GetString("comboBox_ListenPortRange.Items13") });
            comboBox_ListenPortRange.Name = "comboBox_ListenPortRange";
            tTipSetProxy.SetToolTip(comboBox_ListenPortRange, resources.GetString("comboBox_ListenPortRange.ToolTip"));
            comboBox_ListenPortRange.TextChanged += comboBox_ListenPortRange_TextChanged;
            comboBox_ListenPortRange.KeyDown += comboBox_ListenPortRange_KeyDown;
            comboBox_ListenPortRange.KeyPress += comboBox_ListenPortRange_KeyPress;
            comboBox_ListenPortRange.MouseWheel += comboBox_ListenPortRange_MouseWheel;
            // 
            // comboBox_ConnectTo
            // 
            resources.ApplyResources(comboBox_ConnectTo, "comboBox_ConnectTo");
            comboBox_ConnectTo.AutoCompleteCustomSource.AddRange(new string[] { resources.GetString("comboBox_ConnectTo.AutoCompleteCustomSource"), resources.GetString("comboBox_ConnectTo.AutoCompleteCustomSource1"), resources.GetString("comboBox_ConnectTo.AutoCompleteCustomSource2"), resources.GetString("comboBox_ConnectTo.AutoCompleteCustomSource3"), resources.GetString("comboBox_ConnectTo.AutoCompleteCustomSource4"), resources.GetString("comboBox_ConnectTo.AutoCompleteCustomSource5"), resources.GetString("comboBox_ConnectTo.AutoCompleteCustomSource6"), resources.GetString("comboBox_ConnectTo.AutoCompleteCustomSource7") });
            comboBox_ConnectTo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            comboBox_ConnectTo.AutoCompleteSource = AutoCompleteSource.CustomSource;
            comboBox_ConnectTo.BackColor = System.Drawing.Color.FromArgb(41, 46, 57);
            comboBox_ConnectTo.ForeColor = System.Drawing.Color.FromArgb(229, 233, 240);
            comboBox_ConnectTo.FormattingEnabled = true;
            comboBox_ConnectTo.Items.AddRange(new object[] { resources.GetString("comboBox_ConnectTo.Items"), resources.GetString("comboBox_ConnectTo.Items1"), resources.GetString("comboBox_ConnectTo.Items2"), resources.GetString("comboBox_ConnectTo.Items3"), resources.GetString("comboBox_ConnectTo.Items4"), resources.GetString("comboBox_ConnectTo.Items5"), resources.GetString("comboBox_ConnectTo.Items6"), resources.GetString("comboBox_ConnectTo.Items7"), resources.GetString("comboBox_ConnectTo.Items8") });
            comboBox_ConnectTo.Name = "comboBox_ConnectTo";
            comboBox_ConnectTo.TextChanged += comboBox_ConnectTo_TextChanged;
            comboBox_ConnectTo.KeyPress += comboBox_ConnectTo_KeyPress;
            // 
            // lblDupe
            // 
            resources.ApplyResources(lblDupe, "lblDupe");
            lblDupe.BackColor = System.Drawing.Color.FromArgb(208, 135, 112);
            lblDupe.ForeColor = System.Drawing.Color.FromArgb(235, 203, 139);
            lblDupe.Name = "lblDupe";
            // 
            // btnCancel
            // 
            resources.ApplyResources(btnCancel, "btnCancel");
            btnCancel.BackColor = System.Drawing.Color.FromArgb(41, 46, 57);
            btnCancel.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(67, 76, 94);
            btnCancel.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(59, 66, 82);
            btnCancel.ForeColor = System.Drawing.Color.FromArgb(191, 97, 106);
            btnCancel.Name = "btnCancel";
            btnCancel.UseVisualStyleBackColor = false;
            btnCancel.Click += btnCancel_Click;
            // 
            // lblClone
            // 
            resources.ApplyResources(lblClone, "lblClone");
            lblClone.BackColor = System.Drawing.Color.FromArgb(103, 81, 99);
            lblClone.ForeColor = System.Drawing.Color.FromArgb(180, 142, 173);
            lblClone.Name = "lblClone";
            // 
            // listBoxIP4
            // 
            resources.ApplyResources(listBoxIP4, "listBoxIP4");
            listBoxIP4.BackColor = System.Drawing.Color.FromArgb(41, 46, 57);
            listBoxIP4.BorderStyle = BorderStyle.FixedSingle;
            listBoxIP4.ForeColor = System.Drawing.Color.FromArgb(229, 233, 240);
            listBoxIP4.FormattingEnabled = true;
            listBoxIP4.Name = "listBoxIP4";
            listBoxIP4.Sorted = true;
            listBoxIP4.DoubleClick += listBoxIP4_DoubleClick;
            // 
            // listBoxIP6
            // 
            resources.ApplyResources(listBoxIP6, "listBoxIP6");
            listBoxIP6.BackColor = System.Drawing.Color.FromArgb(41, 46, 57);
            listBoxIP6.BorderStyle = BorderStyle.FixedSingle;
            listBoxIP6.ForeColor = System.Drawing.Color.FromArgb(229, 233, 240);
            listBoxIP6.FormattingEnabled = true;
            listBoxIP6.Name = "listBoxIP6";
            listBoxIP6.DoubleClick += listBoxIP6_DoubleClick;
            // 
            // pnlTop
            // 
            pnlTop.BackColor = System.Drawing.Color.FromArgb(41, 46, 57);
            pnlTop.Controls.Add(lblClone);
            resources.ApplyResources(pnlTop, "pnlTop");
            pnlTop.Name = "pnlTop";
            // 
            // pnlBottom
            // 
            pnlBottom.BackColor = System.Drawing.Color.FromArgb(41, 46, 57);
            resources.ApplyResources(pnlBottom, "pnlBottom");
            pnlBottom.Name = "pnlBottom";
            // 
            // comboBox_ListenPort
            // 
            resources.ApplyResources(comboBox_ListenPort, "comboBox_ListenPort");
            comboBox_ListenPort.AutoCompleteCustomSource.AddRange(new string[] { resources.GetString("comboBox_ListenPort.AutoCompleteCustomSource"), resources.GetString("comboBox_ListenPort.AutoCompleteCustomSource1"), resources.GetString("comboBox_ListenPort.AutoCompleteCustomSource2"), resources.GetString("comboBox_ListenPort.AutoCompleteCustomSource3"), resources.GetString("comboBox_ListenPort.AutoCompleteCustomSource4"), resources.GetString("comboBox_ListenPort.AutoCompleteCustomSource5"), resources.GetString("comboBox_ListenPort.AutoCompleteCustomSource6"), resources.GetString("comboBox_ListenPort.AutoCompleteCustomSource7"), resources.GetString("comboBox_ListenPort.AutoCompleteCustomSource8"), resources.GetString("comboBox_ListenPort.AutoCompleteCustomSource9"), resources.GetString("comboBox_ListenPort.AutoCompleteCustomSource10"), resources.GetString("comboBox_ListenPort.AutoCompleteCustomSource11"), resources.GetString("comboBox_ListenPort.AutoCompleteCustomSource12"), resources.GetString("comboBox_ListenPort.AutoCompleteCustomSource13") });
            comboBox_ListenPort.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            comboBox_ListenPort.AutoCompleteSource = AutoCompleteSource.CustomSource;
            comboBox_ListenPort.BackColor = System.Drawing.Color.FromArgb(41, 46, 57);
            comboBox_ListenPort.ForeColor = System.Drawing.Color.FromArgb(229, 233, 240);
            comboBox_ListenPort.FormattingEnabled = true;
            comboBox_ListenPort.Items.AddRange(new object[] { resources.GetString("comboBox_ListenPort.Items"), resources.GetString("comboBox_ListenPort.Items1"), resources.GetString("comboBox_ListenPort.Items2"), resources.GetString("comboBox_ListenPort.Items3"), resources.GetString("comboBox_ListenPort.Items4"), resources.GetString("comboBox_ListenPort.Items5"), resources.GetString("comboBox_ListenPort.Items6"), resources.GetString("comboBox_ListenPort.Items7"), resources.GetString("comboBox_ListenPort.Items8"), resources.GetString("comboBox_ListenPort.Items9"), resources.GetString("comboBox_ListenPort.Items10"), resources.GetString("comboBox_ListenPort.Items11"), resources.GetString("comboBox_ListenPort.Items12"), resources.GetString("comboBox_ListenPort.Items13") });
            comboBox_ListenPort.Name = "comboBox_ListenPort";
            comboBox_ListenPort.TextChanged += comboBox_ListenPort_TextChanged;
            comboBox_ListenPort.KeyDown += comboBox_ListenPort_KeyDown;
            comboBox_ListenPort.KeyPress += comboBox_ListenPort_KeyPress;
            comboBox_ListenPort.MouseWheel += comboBox_ListenPort_MouseWheel;
            // 
            // comboBox_ConnectPort
            // 
            resources.ApplyResources(comboBox_ConnectPort, "comboBox_ConnectPort");
            comboBox_ConnectPort.AutoCompleteCustomSource.AddRange(new string[] { resources.GetString("comboBox_ConnectPort.AutoCompleteCustomSource"), resources.GetString("comboBox_ConnectPort.AutoCompleteCustomSource1"), resources.GetString("comboBox_ConnectPort.AutoCompleteCustomSource2"), resources.GetString("comboBox_ConnectPort.AutoCompleteCustomSource3"), resources.GetString("comboBox_ConnectPort.AutoCompleteCustomSource4"), resources.GetString("comboBox_ConnectPort.AutoCompleteCustomSource5"), resources.GetString("comboBox_ConnectPort.AutoCompleteCustomSource6"), resources.GetString("comboBox_ConnectPort.AutoCompleteCustomSource7"), resources.GetString("comboBox_ConnectPort.AutoCompleteCustomSource8"), resources.GetString("comboBox_ConnectPort.AutoCompleteCustomSource9"), resources.GetString("comboBox_ConnectPort.AutoCompleteCustomSource10"), resources.GetString("comboBox_ConnectPort.AutoCompleteCustomSource11"), resources.GetString("comboBox_ConnectPort.AutoCompleteCustomSource12"), resources.GetString("comboBox_ConnectPort.AutoCompleteCustomSource13") });
            comboBox_ConnectPort.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            comboBox_ConnectPort.AutoCompleteSource = AutoCompleteSource.CustomSource;
            comboBox_ConnectPort.BackColor = System.Drawing.Color.FromArgb(41, 46, 57);
            comboBox_ConnectPort.ForeColor = System.Drawing.Color.FromArgb(229, 233, 240);
            comboBox_ConnectPort.FormattingEnabled = true;
            comboBox_ConnectPort.Items.AddRange(new object[] { resources.GetString("comboBox_ConnectPort.Items"), resources.GetString("comboBox_ConnectPort.Items1"), resources.GetString("comboBox_ConnectPort.Items2"), resources.GetString("comboBox_ConnectPort.Items3"), resources.GetString("comboBox_ConnectPort.Items4"), resources.GetString("comboBox_ConnectPort.Items5"), resources.GetString("comboBox_ConnectPort.Items6"), resources.GetString("comboBox_ConnectPort.Items7"), resources.GetString("comboBox_ConnectPort.Items8"), resources.GetString("comboBox_ConnectPort.Items9"), resources.GetString("comboBox_ConnectPort.Items10"), resources.GetString("comboBox_ConnectPort.Items11"), resources.GetString("comboBox_ConnectPort.Items12"), resources.GetString("comboBox_ConnectPort.Items13") });
            comboBox_ConnectPort.Name = "comboBox_ConnectPort";
            comboBox_ConnectPort.TextChanged += comboBox_ConnectPort_TextChanged;
            comboBox_ConnectPort.KeyDown += comboBox_ConnectPort_KeyDown;
            comboBox_ConnectPort.KeyPress += comboBox_ConnectPort_KeyPress;
            comboBox_ConnectPort.MouseWheel += comboBox_ConnectPort_MouseWheel;
            // 
            // tmrGeneral
            // 
            tmrGeneral.Enabled = true;
            tmrGeneral.Interval = 1500;
            tmrGeneral.Tick += tmrGeneral_Tick;
            // 
            // SetProxy
            // 
            AcceptButton = button_Set;
            resources.ApplyResources(this, "$this");
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(46, 52, 64);
            ControlBox = false;
            Controls.Add(comboBox_ConnectPort);
            Controls.Add(comboBox_ListenPortRange);
            Controls.Add(comboBox_ListenPort);
            Controls.Add(pnlTop);
            Controls.Add(lblDiscoveredIP6);
            Controls.Add(lblDiscoveredIP4);
            Controls.Add(lblWSLDiscovered);
            Controls.Add(listBoxIP6);
            Controls.Add(listBoxIP4);
            Controls.Add(chkAutoComment);
            Controls.Add(btnCancel);
            Controls.Add(progBarRange);
            Controls.Add(lblRangeCount);
            Controls.Add(lblType);
            Controls.Add(lblDupe);
            Controls.Add(lblWSLIP);
            Controls.Add(comboBox_ConnectTo);
            Controls.Add(lblRequired);
            Controls.Add(chkBox_ListenPortRange);
            Controls.Add(lblDash);
            Controls.Add(comboBox_ListenOn);
            Controls.Add(comboBox_Group);
            Controls.Add(label_Group);
            Controls.Add(textBox_Comment);
            Controls.Add(label_Comment);
            Controls.Add(label_ListenPort);
            Controls.Add(button_Set);
            Controls.Add(label_ConnectPort);
            Controls.Add(label_ConnectTo);
            Controls.Add(label_ListenOn);
            Controls.Add(pnlBottom);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "SetProxy";
            ShowInTaskbar = false;
            TopMost = true;
            FormClosing += SetProxyForm_FormClosing;
            Load += SetProxyForm_Load;
            pnlTop.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label_ListenOn;
        private Label label_ConnectTo;
        private Label label_ConnectPort;
        private Button button_Set;
        private Label label_ListenPort;
        private Label label_Comment;
        private TextBox textBox_Comment;
        private Label label_Group;
        private ComboBox comboBox_Group;
        private ComboBox comboBox_ListenOn;
        private Label lblDash;
        private CheckBox chkBox_ListenPortRange;
        private Label lblRequired;
        private ToolTip tTipSetProxy;
        private ComboBox comboBox_ConnectTo;
        private Label lblWSLIP;
        private Label lblDupe;
        private Label lblType;
        private Label lblRangeCount;
        private ProgressBar progBarRange;
        private Button btnCancel;
        private Label lblClone;
        private CheckBox chkAutoComment;
        private ListBox listBoxIP4;
        private ListBox listBoxIP6;
        private Label lblWSLDiscovered;
        private Label lblDiscoveredIP6;
        private Label lblDiscoveredIP4;
        private Panel pnlTop;
        private Panel pnlBottom;
        private ComboBox comboBox_ListenPort;
        private ComboBox comboBox_ListenPortRange;
        private ComboBox comboBox_ConnectPort;
        private Timer tmrGeneral;
    }
}