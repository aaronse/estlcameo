using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EstlCameo
{
    internal enum FilterMatchMode
    {
        Basic,      // substring
        Wildcard,   // * ?
        Regex
    }

    internal sealed class LogFilterForm : Form
    {
        private RadioButton _rbInclude;
        private RadioButton _rbExclude;
        private RadioButton _rbBasic;
        private RadioButton _rbWildcard;
        private RadioButton _rbRegex;
        private TextBox _txtPattern;
        private Button _btnOk;
        private Button _btnCancel;
        private Label _lblPreview;
        private Button _btnTextColor;
        private Button _btnBackColor;
        private ColorDialog _colorDialog;


        public bool Include { get; set; } = true;
        public FilterMatchMode Mode { get; set; } = FilterMatchMode.Basic;
        public string Pattern { get; set; } = string.Empty;
        public Color ForeColorValue { get; set; } = Color.Empty;
        public Color BackColorValue { get; set; } = Color.Empty;


        public LogFilterForm()
        {
            Text = "Edit Filter";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(420, 250);
            ShowInTaskbar = false;

            BuildUi();
        }

        private void BuildUi()
        {
            var grpType = new GroupBox
            {
                Text = "Filter Type",
                Left = 10,
                Top = 10,
                Width = 180,
                Height = 80
            };

            _rbInclude = new RadioButton { Text = "Include", Left = 10, Top = 20, AutoSize = true, Checked = true };
            _rbExclude = new RadioButton { Text = "Exclude", Left = 10, Top = 45, AutoSize = true };

            grpType.Controls.Add(_rbInclude);
            grpType.Controls.Add(_rbExclude);
            Controls.Add(grpType);

            var grpMode = new GroupBox
            {
                Text = "Match Mode",
                Left = 210,
                Top = 10,
                Width = 190,
                Height = 100
            };

            _rbBasic = new RadioButton { Text = "Basic (contains)", Left = 10, Top = 20, AutoSize = true, Checked = true };
            _rbWildcard = new RadioButton { Text = "Wildcard (* ?)", Left = 10, Top = 45, AutoSize = true };
            _rbRegex = new RadioButton { Text = "Regex", Left = 10, Top = 70, AutoSize = true };

            grpMode.Controls.Add(_rbBasic);
            grpMode.Controls.Add(_rbWildcard);
            grpMode.Controls.Add(_rbRegex);
            Controls.Add(grpMode);

            var lblPattern = new Label
            {
                Text = "Pattern:",
                Left = 10,
                Top = 105,
                AutoSize = true
            };
            Controls.Add(lblPattern);

            _txtPattern = new TextBox
            {
                Left = 10,
                Top = 125,
                Width = 390
            };
            Controls.Add(_txtPattern);

            _lblPreview = new Label
            {
                Left = 10,
                Top = 155,          // a bit higher
                Width = 160,
                Height = 24,
                Text = "Sample text",
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(_lblPreview);

            _btnTextColor = new Button
            {
                Text = "Text Color...",
                Left = 180,
                Top = 155,          // aligned with preview, right side
                Width = 100
            };
            _btnTextColor.Click += (_, __) => PickColor(isForeground: true);
            Controls.Add(_btnTextColor);

            _btnBackColor = new Button
            {
                Text = "Back Color...",
                Left = 290,
                Top = 155,
                Width = 100
            };
            _btnBackColor.Click += (_, __) => PickColor(isForeground: false);
            Controls.Add(_btnBackColor);

            _colorDialog = new ColorDialog();

            _btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Left = 230,
                Top = 200,          // pushed down below preview+color row
                Width = 80
            };
            _btnOk.Click += (_, __) => OnOk();

            _btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Left = 320,
                Top = 200,
                Width = 80
            };

            Controls.Add(_btnOk);
            Controls.Add(_btnCancel);

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            // initial values
            Load += (_, __) =>
            {
                _rbInclude.Checked = Include;
                _rbExclude.Checked = !Include;

                _rbBasic.Checked = Mode == FilterMatchMode.Basic;
                _rbWildcard.Checked = Mode == FilterMatchMode.Wildcard;
                _rbRegex.Checked = Mode == FilterMatchMode.Regex;

                _txtPattern.Text = Pattern ?? string.Empty;
                _txtPattern.SelectionStart = _txtPattern.TextLength;

                if (!ForeColorValue.IsEmpty)
                    _lblPreview.ForeColor = ForeColorValue;
                if (!BackColorValue.IsEmpty)
                    _lblPreview.BackColor = BackColorValue;
            };
        }

        private void PickColor(bool isForeground)
        {
            _colorDialog.Color = isForeground
                ? (_lblPreview.ForeColor.IsEmpty ? SystemColors.ControlText : _lblPreview.ForeColor)
                : (_lblPreview.BackColor.IsEmpty ? SystemColors.Control : _lblPreview.BackColor);

            if (_colorDialog.ShowDialog(this) != DialogResult.OK)
                return;

            if (isForeground)
            {
                _lblPreview.ForeColor = _colorDialog.Color;
            }
            else
            {
                _lblPreview.BackColor = _colorDialog.Color;
            }
        }

        private void OnOk()
        {
            Include = _rbInclude.Checked;
            if (_rbWildcard.Checked) Mode = FilterMatchMode.Wildcard;
            else if (_rbRegex.Checked) Mode = FilterMatchMode.Regex;
            else Mode = FilterMatchMode.Basic;

            Pattern = _txtPattern.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(Pattern))
            {
                MessageBox.Show(
                    this,
                    "Please enter a pattern.",
                    "Filter",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                DialogResult = DialogResult.None;
                return;
            }

            ForeColorValue = _lblPreview.ForeColor;
            BackColorValue = _lblPreview.BackColor;

            Close();
        }
    }
}
