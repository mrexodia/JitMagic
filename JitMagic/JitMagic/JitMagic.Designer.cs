namespace JitMagic
{
    partial class JitMagic
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.labelProcessInformation = new System.Windows.Forms.Label();
            this.listViewDebuggers = new System.Windows.Forms.ListView();
            this.panel1 = new System.Windows.Forms.Panel();
			this.label1 = new System.Windows.Forms.Label();
			this.btnIgnoreAll = new System.Windows.Forms.Button();
			this.txtIgnore = new System.Windows.Forms.NumericUpDown();
			this.label2 = new System.Windows.Forms.Label();
			this.btnAttach = new System.Windows.Forms.Button();
			this.btnRemoveUs = new System.Windows.Forms.Button();
			this.btnBlacklistPath = new System.Windows.Forms.Button();
            this.panel1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.txtIgnore)).BeginInit();
            this.SuspendLayout();
            // 
            // labelProcessInformation
            // 
            this.labelProcessInformation.AutoSize = true;
			this.labelProcessInformation.Location = new System.Drawing.Point(24, 17);
			this.labelProcessInformation.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.labelProcessInformation.Name = "labelProcessInformation";
			this.labelProcessInformation.Size = new System.Drawing.Size(192, 25);
            this.labelProcessInformation.TabIndex = 0;
            this.labelProcessInformation.Text = "No process loaded";
            // 
            // listViewDebuggers
            // 
			this.listViewDebuggers.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listViewDebuggers.BorderStyle = System.Windows.Forms.BorderStyle.None;
			this.listViewDebuggers.HideSelection = false;
			this.listViewDebuggers.Location = new System.Drawing.Point(4, 12);
			this.listViewDebuggers.Margin = new System.Windows.Forms.Padding(12);
            this.listViewDebuggers.Name = "listViewDebuggers";
			this.listViewDebuggers.Size = new System.Drawing.Size(1301, 124);
            this.listViewDebuggers.TabIndex = 1;
			this.listViewDebuggers.TileSize = new System.Drawing.Size(200, 100);
            this.listViewDebuggers.UseCompatibleStateImageBehavior = false;
            // 
            // panel1
            // 
			this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.BackColor = System.Drawing.Color.White;
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.listViewDebuggers);
			this.panel1.Location = new System.Drawing.Point(24, 48);
			this.panel1.Margin = new System.Windows.Forms.Padding(6);
            this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(1306, 137);
            this.panel1.TabIndex = 2;
            // 
			// label1
			// 
			this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(158, 198);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(43, 25);
			this.label1.TabIndex = 3;
			this.label1.Text = "for ";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			// 
			// btnIgnoreAll
			// 
			this.btnIgnoreAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnIgnoreAll.Location = new System.Drawing.Point(24, 193);
			this.btnIgnoreAll.Name = "btnIgnoreAll";
			this.btnIgnoreAll.Size = new System.Drawing.Size(124, 33);
			this.btnIgnoreAll.TabIndex = 4;
			this.btnIgnoreAll.Text = "Ignore All";
			this.btnIgnoreAll.UseVisualStyleBackColor = true;
			this.btnIgnoreAll.Click += new System.EventHandler(this.btnIgnoreAll_Click);
			// 
			// txtIgnore
			// 
			this.txtIgnore.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.txtIgnore.Location = new System.Drawing.Point(203, 195);
			this.txtIgnore.Maximum = new decimal(new int[] {
            999999,
            0,
            0,
            0});
			this.txtIgnore.Name = "txtIgnore";
			this.txtIgnore.Size = new System.Drawing.Size(70, 31);
			this.txtIgnore.TabIndex = 5;
			this.txtIgnore.Value = new decimal(new int[] {
            3,
            0,
            0,
            0});
			// 
			// label2
			// 
			this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(279, 197);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(87, 25);
			this.label2.TabIndex = 6;
			this.label2.Text = "minutes";
			// 
			// btnAttach
			// 
			this.btnAttach.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
			this.btnAttach.Location = new System.Drawing.Point(609, 193);
			this.btnAttach.Name = "btnAttach";
			this.btnAttach.Size = new System.Drawing.Size(124, 33);
			this.btnAttach.TabIndex = 7;
			this.btnAttach.Text = "Attach";
			this.btnAttach.UseVisualStyleBackColor = true;
			this.btnAttach.Click += new System.EventHandler(this.btnAttach_Click);
			// 
			// btnRemoveUs
			// 
			this.btnRemoveUs.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnRemoveUs.Location = new System.Drawing.Point(1157, 193);
			this.btnRemoveUs.Name = "btnRemoveUs";
			this.btnRemoveUs.Size = new System.Drawing.Size(173, 33);
			this.btnRemoveUs.TabIndex = 8;
			this.btnRemoveUs.Text = "Remove As JIT";
			this.btnRemoveUs.UseVisualStyleBackColor = true;
			this.btnRemoveUs.Click += new System.EventHandler(this.btnRemoveUs_Click);
			// 
			// btnBlacklistPath
			// 
			this.btnBlacklistPath.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnBlacklistPath.Location = new System.Drawing.Point(943, 193);
			this.btnBlacklistPath.Name = "btnBlacklistPath";
			this.btnBlacklistPath.Size = new System.Drawing.Size(196, 33);
			this.btnBlacklistPath.TabIndex = 9;
			this.btnBlacklistPath.Text = "Blacklist This App";
			this.btnBlacklistPath.UseVisualStyleBackColor = true;
			this.btnBlacklistPath.Click += new System.EventHandler(this.btnBlacklistPath_Click);
			// 
            // JitMagic
            // 
			this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(1360, 251);
			this.Controls.Add(this.btnBlacklistPath);
			this.Controls.Add(this.btnRemoveUs);
			this.Controls.Add(this.btnAttach);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.txtIgnore);
			this.Controls.Add(this.btnIgnoreAll);
			this.Controls.Add(this.label1);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.labelProcessInformation);
			this.Margin = new System.Windows.Forms.Padding(12);
            this.MaximizeBox = false;
            this.Name = "JitMagic";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "JitMagic";
			this.TopMost = true;
            this.panel1.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.txtIgnore)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelProcessInformation;
        private System.Windows.Forms.ListView listViewDebuggers;
        private System.Windows.Forms.Panel panel1;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Button btnIgnoreAll;
		private System.Windows.Forms.NumericUpDown txtIgnore;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Button btnAttach;
		private System.Windows.Forms.Button btnRemoveUs;
		private System.Windows.Forms.Button btnBlacklistPath;
    }
}

