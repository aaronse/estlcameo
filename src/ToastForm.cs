using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;   // avoid ambiguity with System.Threading.Timer

namespace EstlCameo
{
    public class ToastForm : Form
    {
        private readonly Label messageLabel;
        private readonly LinkLabel fileLinkLabel;
        private readonly PictureBox previewBox;
        private readonly Timer lifetimeTimer;
        private readonly Timer fadeTimer;

        private readonly string filePath;
        private readonly bool hasFileLink;

        private const int ToastWidth = 380;
        private const int LifetimeMs = 2500;
        private const int FadeIntervalMs = 50;
        private const double FadeStep = 0.07;

        public ToastForm(string message, string filePath, string previewImagePath = null)
        {
            this.filePath = filePath ?? string.Empty;
            hasFileLink = !string.IsNullOrEmpty(this.filePath);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            Opacity = 0.97;
            BackColor = Color.FromArgb(40, 40, 40);
            Padding = new Padding(8);

            // --- Controls ---

            // Thumbnail on top
            previewBox = new PictureBox
            {
                Dock = DockStyle.Top,
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.Transparent,
            };

            // Main message
            messageLabel = new Label
            {
                Dock = DockStyle.Top,
                Text = message,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.TopLeft,
                Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point),
                AutoSize = true,  // <=== allow it to grow for multiple lines
                MaximumSize = new Size(ToastWidth - Padding.Horizontal - 16, 0),
                Padding = new Padding(0, 4, 0, 0),
                UseMnemonic = false
            };

            // File link
            fileLinkLabel = new LinkLabel
            {
                Dock = DockStyle.Top,
                Text = Path.GetFileName(filePath),
                LinkColor = Color.DeepSkyBlue,
                VisitedLinkColor = Color.SkyBlue,
                ActiveLinkColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 10F, FontStyle.Underline, GraphicsUnit.Point),
                AutoSize = false,
                Height = 24,
                Padding = new Padding(0, 2, 0, 4)
            };
            fileLinkLabel.LinkClicked += (_, __) => OpenFileInExplorer();

            // Order of adding: bottom-most first when using DockStyle.Top
            if (hasFileLink)
            {
                Controls.Add(fileLinkLabel);   // bottom (if present)
            }
            Controls.Add(messageLabel);
            Controls.Add(previewBox);

            // Load / scale preview if available
            int previewHeight = LoadAndScalePreview(previewImagePath);

            var preferredMessageSize =
                messageLabel.GetPreferredSize(new Size(ToastWidth - Padding.Horizontal - 16, 0));
            messageLabel.Height = preferredMessageSize.Height;

            int linkHeight = hasFileLink ? fileLinkLabel.Height : 0;
            int textBlockHeight = messageLabel.Height + linkHeight + 16;
            int desiredClientHeight = previewHeight + textBlockHeight + Padding.Vertical;

            ClientSize = new Size(ToastWidth, desiredClientHeight);

            // Position bottom-right of primary screen using final height
            var workingArea = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(
                workingArea.Right - Width - 20,
                workingArea.Bottom - Height - 40
            );

            // --- Timers ---

            lifetimeTimer = new Timer { Interval = LifetimeMs };
            lifetimeTimer.Tick += (_, __) =>
            {
                lifetimeTimer.Stop();
                fadeTimer.Start();
            };

            fadeTimer = new Timer { Interval = FadeIntervalMs };
            fadeTimer.Tick += (_, __) =>
            {
                Opacity -= FadeStep;
                if (Opacity <= 0.05)
                {
                    fadeTimer.Stop();
                    Close();
                }
            };

            // Click anywhere to open in Explorer
            Click += (_, __) => OpenFileInExplorer();
            messageLabel.Click += (_, __) => OpenFileInExplorer();
            previewBox.Click += (_, __) => OpenFileInExplorer();
        }

        /// <summary>
        /// Load preview image, scale to 1/8th of original size,
        /// then clamp width if needed to fit the toast width.
        /// Returns the final preview height.
        /// </summary>
        private int LoadAndScalePreview(string previewImagePath)
        {
            if (string.IsNullOrEmpty(previewImagePath) || !File.Exists(previewImagePath))
            {
                // IMPORTANT: collapse the preview area when no image
                previewBox.Image = null;
                previewBox.Height = 0;
                return 0;
            }

            try
            {
                using var original = Image.FromFile(previewImagePath);

                // 1/8th scale as requested
                double baseScale = 1.0 / 8.0;
                int scaledW = Math.Max(1, (int)(original.Width * baseScale));
                int scaledH = Math.Max(1, (int)(original.Height * baseScale));

                // Clamp width so it fits inside toast (minus padding)
                int maxWidth = ToastWidth - Padding.Horizontal - 16;
                if (scaledW > maxWidth)
                {
                    double clampScale = (double)maxWidth / scaledW;
                    scaledW = (int)(scaledW * clampScale);
                    scaledH = (int)(scaledH * clampScale);
                }

                var thumb = new Bitmap(scaledW, scaledH);
                using (var g = Graphics.FromImage(thumb))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.Clear(Color.Transparent);
                    g.DrawImage(original, 0, 0, scaledW, scaledH);
                }

                previewBox.Image = thumb;
                previewBox.Height = scaledH + 4; // small bottom gap

                return previewBox.Height;
            }
            catch
            {
                // If anything goes wrong loading/decoding the PNG, we just skip the preview.
                previewBox.Image = null;
                previewBox.Height = 0;
                return 0;
            }
        }

        // Prevent the form from stealing focus
        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                const int WS_EX_NOACTIVATE = 0x08000000;
                const int WS_EX_TOOLWINDOW = 0x00000080;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            lifetimeTimer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (previewBox.Image != null)
            {
                previewBox.Image.Dispose();
                previewBox.Image = null;
            }
        }

        private void OpenFileInExplorer()
        {
            try
            {
                if (!string.IsNullOrEmpty(filePath))
                {
                    Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
            }
            catch
            {
                // Don't crash the toast on explorer errors
            }

            Close();
        }
    }
}
