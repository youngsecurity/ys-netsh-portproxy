namespace PortProxyGooey {
    partial class About {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(About));
            linkLabel1 = new System.Windows.Forms.LinkLabel();
            label1 = new System.Windows.Forms.Label();
            label_version = new System.Windows.Forms.Label();
            label_Star = new System.Windows.Forms.Label();
            SuspendLayout();
            // 
            // linkLabel1
            // 
            linkLabel1.ActiveLinkColor = System.Drawing.Color.FromArgb(191, 97, 106);
            resources.ApplyResources(linkLabel1, "linkLabel1");
            linkLabel1.LinkColor = System.Drawing.Color.FromArgb(94, 129, 172);
            linkLabel1.Name = "linkLabel1";
            linkLabel1.TabStop = true;
            linkLabel1.VisitedLinkColor = System.Drawing.Color.FromArgb(180, 142, 173);
            linkLabel1.Click += linkLabel1_Click;
            // 
            // label1
            // 
            resources.ApplyResources(label1, "label1");
            label1.Name = "label1";
            // 
            // label_version
            // 
            resources.ApplyResources(label_version, "label_version");
            label_version.Name = "label_version";
            // 
            // label_Star
            // 
            resources.ApplyResources(label_Star, "label_Star");
            label_Star.Name = "label_Star";
            // 
            // About
            // 
            resources.ApplyResources(this, "$this");
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(46, 52, 64);
            Controls.Add(label_Star);
            Controls.Add(label_version);
            Controls.Add(label1);
            Controls.Add(linkLabel1);
            ForeColor = System.Drawing.Color.FromArgb(216, 222, 233);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "About";
            TopMost = true;
            FormClosing += About_FormClosing;
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.LinkLabel linkLabel1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label_version;
        private System.Windows.Forms.Label label_Star;
    }
}