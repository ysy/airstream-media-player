namespace AirStreamPlayer
{
    partial class About
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
            this.title = new System.Windows.Forms.Label();
            this.byline = new System.Windows.Forms.Label();
            this.okButton = new System.Windows.Forms.Button();
            this.bylink = new System.Windows.Forms.LinkLabel();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // title
            // 
            this.title.AutoSize = true;
            this.title.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.title.Location = new System.Drawing.Point(53, 9);
            this.title.Name = "title";
            this.title.Size = new System.Drawing.Size(365, 29);
            this.title.TabIndex = 0;
            this.title.Text = "Air Stream player for Windows";
            // 
            // byline
            // 
            this.byline.AutoSize = true;
            this.byline.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.byline.Location = new System.Drawing.Point(171, 51);
            this.byline.Name = "byline";
            this.byline.Size = new System.Drawing.Size(111, 18);
            this.byline.TabIndex = 1;
            this.byline.Text = "By Tom Thorpe";
            // 
            // okButton
            // 
            this.okButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.okButton.Location = new System.Drawing.Point(174, 176);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(117, 38);
            this.okButton.TabIndex = 2;
            this.okButton.Text = "Ok";
            this.okButton.UseVisualStyleBackColor = true;
            // 
            // bylink
            // 
            this.bylink.AutoSize = true;
            this.bylink.Location = new System.Drawing.Point(162, 84);
            this.bylink.Name = "bylink";
            this.bylink.Size = new System.Drawing.Size(135, 13);
            this.bylink.TabIndex = 3;
            this.bylink.TabStop = true;
            this.bylink.Text = "http://www.tomthorpe.com";
            this.bylink.VisitedLinkColor = System.Drawing.Color.Blue;
            this.bylink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.bylink_LinkClicked);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(108, 112);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(257, 13);
            this.label1.TabIndex = 4;
            this.label1.Text = "This program is distributed under the GPL v2 Licence";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(70, 134);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(348, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "AirPlay® client for Windows. AirPlay® is a registered trademark by Apple.";
            // 
            // About
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(458, 226);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.bylink);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.byline);
            this.Controls.Add(this.title);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.Name = "About";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "About Air Stream player for Windows";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label title;
        private System.Windows.Forms.Label byline;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.LinkLabel bylink;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
    }
}