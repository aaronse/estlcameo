using System;
using System.Windows.Forms;

namespace EstlCameo
{
    public partial class ProjectResolutionDialog : Form
    {
        public ProjectResolutionDialog()
        {
            InitializeComponent();

            this.StartPosition = FormStartPosition.CenterScreen; // or CenterParent
            this.Shown += (s, e) =>
            {
                // Make sure it’s focused and on top when it appears
                this.Activate();
                this.BringToFront();
            };
        }

        private void btnSelectFile_Click(object sender, EventArgs e)
        {
            // We don't pick the file here; TrayForm will open the OpenFileDialog.
            this.DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            Close();
        }

        private void linkMoreInfo_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // Simple inline explanation for now. You can replace this
            // later with a URL or a richer help window.
            string message =
                "EstlCameo creates automatic snapshots of your Estlcam projects by " +
                "watching the .E12 project file on disk.\n\n" +
                "Estlcam does not currently expose the 'active project path' via an API " +
                "or per-instance state file, so EstlCameo has to guess based on:\n" +
                "  • Window title (file name)\n" +
                "  • Estlcam's recent file list\n\n" +
                "When that guess fails, EstlCameo needs you to pick the correct .E12 file. " +
                "Once selected, snapshots will be created automatically whenever you press Ctrl+S.";

            MessageBox.Show(this, message, "EstlCameo – More info",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
