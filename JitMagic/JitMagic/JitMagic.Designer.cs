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
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // labelProcessInformation
            // 
            this.labelProcessInformation.AutoSize = true;
            this.labelProcessInformation.Location = new System.Drawing.Point(12, 9);
            this.labelProcessInformation.Name = "labelProcessInformation";
            this.labelProcessInformation.Size = new System.Drawing.Size(96, 13);
            this.labelProcessInformation.TabIndex = 0;
            this.labelProcessInformation.Text = "No process loaded";
            // 
            // listViewDebuggers
            // 
            this.listViewDebuggers.Activation = System.Windows.Forms.ItemActivation.TwoClick;
            this.listViewDebuggers.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.listViewDebuggers.Location = new System.Drawing.Point(-1, 9);
            this.listViewDebuggers.Name = "listViewDebuggers";
            this.listViewDebuggers.Size = new System.Drawing.Size(354, 54);
            this.listViewDebuggers.TabIndex = 1;
            this.listViewDebuggers.UseCompatibleStateImageBehavior = false;
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.White;
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.listViewDebuggers);
            this.panel1.Location = new System.Drawing.Point(12, 29);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(354, 68);
            this.panel1.TabIndex = 2;
            // 
            // JitMagic
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(379, 121);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.labelProcessInformation);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "JitMagic";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "JitMagic";
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelProcessInformation;
        private System.Windows.Forms.ListView listViewDebuggers;
        private System.Windows.Forms.Panel panel1;
    }
}

