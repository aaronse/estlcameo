using System;
using System.Windows.Forms;

namespace EstlCameo
{
    public static class Toast
    {
        // Simple message-only toast (no file link)
        public static void Show(string message)
        {
            if (Application.OpenForms.Count > 0)
            {
                var mainForm = Application.OpenForms[0];
                mainForm.BeginInvoke(new Action(() =>
                {
                    // pass empty filePath so clicking does nothing
                    var toast = new ToastForm(message, string.Empty, null);
                    toast.Show();
                }));
            }
        }

        // Snapshot toast with file link + preview image
        public static void ShowSnapshot(string message, string filePath, string previewImagePath = null)
        {
            if (Application.OpenForms.Count > 0)
            {
                var mainForm = Application.OpenForms[0];
                mainForm.BeginInvoke(new Action(() =>
                {
                    var toast = new ToastForm(message, filePath, previewImagePath);
                    toast.Show();
                }));
            }
        }
    }
}
