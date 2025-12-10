using System.Windows.Forms;
using System.Drawing;

namespace EstlCameo
{
    partial class ProjectResolutionDialog
    {
        private Label lblTitle;
        private Label lblBody;
        private LinkLabel linkMoreInfo;
        private Button btnSelectFile;
        private Button btnCancel;

        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblBody = new System.Windows.Forms.Label();
            this.linkMoreInfo = new System.Windows.Forms.LinkLabel();
            this.btnSelectFile = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font(
                "Segoe UI",
                10F,
                System.Drawing.FontStyle.Bold,
                System.Drawing.GraphicsUnit.Point);
            this.lblTitle.Location = new System.Drawing.Point(12, 9);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(191, 19);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "EstlCameo needs your help";
            // 
            // lblBody
            // 
            this.lblBody.AutoSize = false;
            this.lblBody.Location = new System.Drawing.Point(14, 35);
            this.lblBody.Name = "lblBody";
            this.lblBody.Size = new System.Drawing.Size(360, 80);
            this.lblBody.TabIndex = 1;
            this.lblBody.Text =
                "I detected a Save in Estlcam, but I’m not sure which project file is open.\n\n" +
                "To create automatic snapshots, EstlCameo needs to know the correct .E12 " +
                "project file path.\n\n" +
                "Please select the project file you are currently working on in Estlcam.";
            // 
            // linkMoreInfo
            // 
            this.linkMoreInfo.AutoSize = true;
            this.linkMoreInfo.Location = new System.Drawing.Point(14, 125);
            this.linkMoreInfo.Name = "linkMoreInfo";
            this.linkMoreInfo.Size = new System.Drawing.Size(71, 15);
            this.linkMoreInfo.TabIndex = 2;
            this.linkMoreInfo.TabStop = true;
            this.linkMoreInfo.Text = "What’s this?";
            this.linkMoreInfo.LinkClicked +=
                new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkMoreInfo_LinkClicked);
            // 
            // btnSelectFile
            // 
            this.btnSelectFile.Anchor = ((System.Windows.Forms.AnchorStyles)(
                System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right));
            this.btnSelectFile.Location = new System.Drawing.Point(182, 155);
            this.btnSelectFile.Name = "btnSelectFile";
            this.btnSelectFile.Size = new System.Drawing.Size(110, 27);
            this.btnSelectFile.TabIndex = 3;
            this.btnSelectFile.Text = "Select project file…";
            this.btnSelectFile.UseVisualStyleBackColor = true;
            this.btnSelectFile.Click += new System.EventHandler(this.btnSelectFile_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)(
                System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(298, 155);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(76, 27);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // ProjectResolutionDialog
            // 
            this.AcceptButton = this.btnSelectFile;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(386, 194);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSelectFile);
            this.Controls.Add(this.linkMoreInfo);
            this.Controls.Add(this.lblBody);
            this.Controls.Add(this.lblTitle);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ProjectResolutionDialog";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "EstlCameo – Project selection needed";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion
    }
}
