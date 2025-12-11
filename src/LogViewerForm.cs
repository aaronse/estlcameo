// Mostly vibed https://chatgpt.com/c/693b3216-9a50-832b-b45d-b8c9e7b84e53
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EstlCameo
{
    internal sealed class LogViewerForm : Form
    {
        private readonly string _logPath;
        private FileStream _stream;
        private StreamReader _reader;
        private System.Windows.Forms.Timer _timer;

        private readonly List<LogLine> _allLines = new();
        private readonly List<LogLine> _visibleLines = new();
        private readonly BindingList<LogFilter> _filters = new();

        private readonly string _filterConfigPath;
        private ListBox _lineList;
        private DataGridView _filterGrid;
        private ToolStripStatusLabel _statusSel;
        private ToolStripStatusLabel _statusFil;
        private ToolStripStatusLabel _statusTotal;
        private ToolStripStatusLabel _statusFunnel;
        private CheckBox _chkFollowTail;
        private ToolStripButton _btnShowOnlyFiltered;   

        private bool _followTail = true;
        private bool _showOnlyFilteredLines = false;    

        private Panel _scrollMapPanel;                  
        private Splitter _scrollMapSplitter;            
        private bool _scrollMapDragging;                

        // Simple marker support for 1-8
        private const int MarkerCount = 8;              

        private static readonly Color[] FilterColorPalette = new[]
        {
            Color.Black,
            Color.Blue,
            Color.Red,
            Color.DarkGreen,
            Color.DarkCyan,
            Color.DarkMagenta,
            Color.OrangeRed,
            Color.DarkGoldenrod
        };

        private enum LogMessageType
        {
            Debug,
            Info,
            Warn,
            Error,
            Fatal,
            Other
        }

        private enum FilterMatchMode
        {
            Basic,      // substring
            Wildcard,   // * ?
            Regex
        }

        private sealed class LogLine
        {
            public int Index { get; set; }
            public string Text { get; set; }
            public LogMessageType Type { get; set; }
            public bool[] Markers { get; } = new bool[MarkerCount];
        }

        private sealed class LogFilter
        {
            public char Key { get; set; }
            public bool Enabled { get; set; } = true;
            public bool Include { get; set; } = true;
            public FilterMatchMode MatchMode { get; set; } = FilterMatchMode.Basic;
            public string Pattern { get; set; } = string.Empty;

            [Browsable(false)]
            public Regex CompiledRegex { get; set; }

            public override string ToString()
            {
                string kind = Include ? "Include" : "Exclude";
                return $"{Key}: {kind} / {MatchMode} / \"{Pattern}\"";
            }

            public bool IsMatch(string line)
            {
                if (string.IsNullOrEmpty(Pattern))
                    return false;

                switch (MatchMode)
                {
                    case FilterMatchMode.Basic:
                        return line.IndexOf(Pattern, StringComparison.OrdinalIgnoreCase) >= 0;

                    case FilterMatchMode.Wildcard:
                        try
                        {
                            var regex = CompiledRegex ??= BuildWildcardRegex(Pattern);
                            return regex.IsMatch(line);
                        }
                        catch
                        {
                            return false;
                        }

                    case FilterMatchMode.Regex:
                        try
                        {
                            var regex = CompiledRegex ??= new Regex(
                                Pattern,
                                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
                            return regex.IsMatch(line);
                        }
                        catch
                        {
                            return false;
                        }

                    default:
                        return false;
                }
            }

            private static Regex BuildWildcardRegex(string pattern)
            {
                // Escape regex meta, then expand * and ?
                string escaped = Regex.Escape(pattern)
                    .Replace(@"\*", ".*")
                    .Replace(@"\?", ".");
                return new Regex(escaped, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
            }
        }

        private sealed class FilterConfig
        {
            public bool ShowOnlyFilteredLines { get; set; }
            public List<FilterConfigItem> Filters { get; set; }
        }

        private sealed class FilterConfigItem
        {
            public bool Include { get; set; }
            public FilterMatchMode MatchMode { get; set; }
            public string Pattern { get; set; }
            public bool Enabled { get; set; }
        }


        public LogViewerForm(string logPath)
        {
            _logPath = logPath;
            _filterConfigPath = _logPath + ".filters.json";


            Text = "EstlCameo Log Viewer";
            Font = new Font("Consolas", 9.0f, FontStyle.Regular);
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1200, 700);
            KeyPreview = true;

            BuildUi();
            HookEvents();
            LoadFilterConfig();
        }

        private void BuildUi()
        {
            // ToolStrip
            var toolStrip = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                Dock = DockStyle.Top
            };

            var btnClear = new ToolStripButton("Clear")
            {
                ToolTipText = "Clear loaded log lines (does not truncate file)"
            };
            btnClear.Click += (_, __) => ClearLines();

            var btnReload = new ToolStripButton("Reload")
            {
                ToolTipText = "Reload log file from scratch"
            };
            btnReload.Click += (_, __) => ReloadFile();

            var btnCopyAll = new ToolStripButton("Copy All")
            {
                ToolTipText = "Copy all visible log lines to clipboard"
            };
            btnCopyAll.Click += (_, __) => CopyAllVisibleToClipboard();

            var btnCopySel = new ToolStripButton("Copy Selected")
            {
                ToolTipText = "Copy selected lines to clipboard"
            };
            btnCopySel.Click += (_, __) => CopySelectedToClipboard();

            _chkFollowTail = new CheckBox
            {
                Text = "Follow Tail",
                Checked = true,
                AutoSize = true
            };
            _chkFollowTail.CheckedChanged += (_, __) =>      
            {
                _followTail = _chkFollowTail.Checked;
            };
            var hostFollow = new ToolStripControlHost(_chkFollowTail)
            {
                Margin = new Padding(8, 1, 0, 2)
            };

            toolStrip.Items.Add(btnClear);
            toolStrip.Items.Add(btnReload);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(btnCopyAll);
            toolStrip.Items.Add(btnCopySel);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(hostFollow);

            // Show Only Filtered toggle
            _btnShowOnlyFiltered = new ToolStripButton("Show Only Filtered")
            {
                CheckOnClick = true,
                ToolTipText = "Hide lines that do not match Include filters."
            };
            _btnShowOnlyFiltered.CheckedChanged += (_, __) =>
            {
                _showOnlyFilteredLines = _btnShowOnlyFiltered.Checked;
                SaveFilterConfig();
                RebuildVisibleLines();
            };
            toolStrip.Items.Add(_btnShowOnlyFiltered);

            Controls.Add(toolStrip);

            // SplitContainer
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 450
            };

            // --- TOP PANEL: lines + scroll map ---
            var topPanel = new Panel
            {
                Dock = DockStyle.Fill
            };

            _scrollMapPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 40,
                BackColor = Color.White,
                Cursor = Cursors.Hand
            };
            _scrollMapPanel.Paint += OnScrollMapPanelPaint;                  // NEW
            _scrollMapPanel.MouseDown += OnScrollMapPanelMouseDown;          // NEW
            _scrollMapPanel.MouseMove += OnScrollMapPanelMouseMove;          // NEW
            _scrollMapPanel.MouseUp += OnScrollMapPanelMouseUp;              // NEW

            _scrollMapSplitter = new Splitter
            {
                Dock = DockStyle.Right,
                Width = 4,
                BackColor = SystemColors.ControlDark
            };

            _lineList = new ListBox
            {
                Dock = DockStyle.Fill,
                DrawMode = DrawMode.OwnerDrawFixed,
                IntegralHeight = false,
                HorizontalScrollbar = true,
                SelectionMode = SelectionMode.MultiExtended   // NEW: proper multi-select
            };
            _lineList.DrawItem += OnLineListDrawItem;
            _lineList.SelectedIndexChanged += (_, __) => UpdateStatusBar();
            _lineList.KeyDown += OnLineListKeyDown;

            topPanel.Controls.Add(_lineList);
            topPanel.Controls.Add(_scrollMapSplitter);
            topPanel.Controls.Add(_scrollMapPanel);

            split.Panel1.Controls.Add(topPanel);

            // Filter grid
            _filterGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ReadOnly = true,
                BackgroundColor = SystemColors.Window
            };

            var colKey = new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(LogFilter.Key),
                HeaderText = "Key",
                Width = 40
            };

            var colEnabled = new DataGridViewCheckBoxColumn
            {
                DataPropertyName = nameof(LogFilter.Enabled),
                HeaderText = "On",
                Width = 40
            };

            var colType = new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(LogFilter.Include),
                HeaderText = "Type",
                Width = 80
            };

            colType.CellTemplate = new DataGridViewTextBoxCell();

            var colMode = new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(LogFilter.MatchMode),
                HeaderText = "Mode",
                Width = 80
            };

            var colPattern = new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(LogFilter.Pattern),
                HeaderText = "Pattern",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };

            _filterGrid.Columns.AddRange(colKey, colEnabled, colType, colMode, colPattern);
            _filterGrid.DataSource = _filters;
            _filterGrid.CellFormatting += FilterGrid_CellFormatting;
            _filterGrid.KeyDown += OnFilterGridKeyDown;
            _filterGrid.CellDoubleClick += (_, __) => EditSelectedFilter();

            split.Panel2.Controls.Add(_filterGrid);

            Controls.Add(split);

            // Status strip
            var status = new StatusStrip();

            _statusFunnel = new ToolStripStatusLabel(" ");
            _statusSel = new ToolStripStatusLabel("Sel: 0");
            _statusFil = new ToolStripStatusLabel("Fil: 0");
            _statusTotal = new ToolStripStatusLabel("Total: 0");

            status.Items.Add(_statusFunnel);
            status.Items.Add(new ToolStripSeparator());
            status.Items.Add(_statusSel);
            status.Items.Add(new ToolStripSeparator());
            status.Items.Add(_statusFil);
            status.Items.Add(new ToolStripSeparator());
            status.Items.Add(_statusTotal);

            Controls.Add(status);
        }

        private void HookEvents()
        {
            Load += (_, __) => StartTailing();
            FormClosing += (_, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    Hide();
                }
            };

            KeyDown += OnFormKeyDown;    // NEW – for Ctrl+N global
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)    // NEW
        {
            if (e.Control && e.KeyCode == Keys.N)
            {
                AddFilter();
                e.Handled = true;
            }
        }


        private void StartTailing()
        {
            if (string.IsNullOrEmpty(_logPath) || !File.Exists(_logPath))
            {
                MessageBox.Show(
                    this,
                    "No log file found to view.\n\nPath:\n" + _logPath,
                    "Log Viewer",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                _stream = new FileStream(
                    _logPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);

                _reader = new StreamReader(_stream);

                // Load existing content once
                while (!_reader.EndOfStream)
                {
                    var line = _reader.ReadLine();
                    if (line != null)
                        AddLine(line);
                }

                RebuildVisibleLines();

                _timer = new System.Windows.Forms.Timer { Interval = 250 }; // 4x/sec
                _timer.Tick += (_, __) => PollNewLines();
                _timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Unable to open log file:\n" + ex.Message,
                    "Log Viewer",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void LoadFilterConfig()                       // NEW
        {
            if (string.IsNullOrEmpty(_filterConfigPath) || !File.Exists(_filterConfigPath))
                return;

            try
            {
                string json = File.ReadAllText(_filterConfigPath);
                var cfg = JsonSerializer.Deserialize<FilterConfig>(json);
                if (cfg == null)
                    return;

                _filters.Clear();
                if (cfg.Filters != null)
                {
                    foreach (var f in cfg.Filters)
                    {
                        _filters.Add(new LogFilter
                        {
                            Include = f.Include,
                            MatchMode = f.MatchMode,
                            Pattern = f.Pattern,
                            Enabled = f.Enabled
                        });
                    }
                }

                ReindexFilterKeys();
                _showOnlyFilteredLines = cfg.ShowOnlyFilteredLines;
                if (_btnShowOnlyFiltered != null)
                    _btnShowOnlyFiltered.Checked = _showOnlyFilteredLines;

                _filterGrid?.Refresh();
            }
            catch
            {
                // Ignore corrupt config; user can recreate filters
            }
        }

        private void SaveFilterConfig()                       // NEW
        {
            if (string.IsNullOrEmpty(_filterConfigPath))
                return;

            try
            {
                var cfg = new FilterConfig
                {
                    ShowOnlyFilteredLines = _showOnlyFilteredLines,
                    Filters = _filters.Select(f => new FilterConfigItem
                    {
                        Include = f.Include,
                        MatchMode = f.MatchMode,
                        Pattern = f.Pattern,
                        Enabled = f.Enabled
                    }).ToList()
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(cfg, options);
                File.WriteAllText(_filterConfigPath, json);
            }
            catch
            {
                // Best-effort; don't crash logging UI
            }
        }


        private void PollNewLines()
        {
            if (_reader == null)
                return;

            bool anyNew = false;

            try
            {
                while (!_reader.EndOfStream)
                {
                    string line = _reader.ReadLine();
                    if (line == null)
                        break;

                    AddLine(line);
                    anyNew = true;
                }
            }
            catch
            {
                // Probably file truncated/locked. We could add smarter handling later.
                return;
            }

            if (anyNew)
            {
                RebuildVisibleLines();
            }
        }

        private void AddLine(string text)
        {
            var line = new LogLine
            {
                Index = _allLines.Count,
                Text = text,
                Type = ParseType(text)
            };
            _allLines.Add(line);
        }

        private LogMessageType ParseType(string text)
        {
            // Matches Log.Write prefixes like "[E] 12:34:56.78 ..." :contentReference[oaicite:1]{index=1}
            if (string.IsNullOrEmpty(text) || text.Length < 3 || text[0] != '[')
                return LogMessageType.Other;

            return text[1] switch
            {
                'D' => LogMessageType.Debug,
                'I' => LogMessageType.Info,
                'W' => LogMessageType.Warn,
                'E' => LogMessageType.Error,
                'F' => LogMessageType.Fatal,
                _ => LogMessageType.Other
            };
        }

        private void RebuildVisibleLines()
        {
            _visibleLines.Clear();

            bool hasInclude = false;
            bool hasExclude = false;

            foreach (var f in _filters)
            {
                if (!f.Enabled) continue;
                if (f.Include) hasInclude = true;
                else hasExclude = true;
            }

            foreach (var line in _allLines)
            {
                bool includeOk = !_showOnlyFilteredLines || !hasInclude;
                bool excludeFail = false;

                foreach (var f in _filters)
                {
                    if (!f.Enabled) continue;
                    bool match = f.IsMatch(line.Text);

                    if (f.Include && _showOnlyFilteredLines)
                    {
                        if (match)
                            includeOk = true;
                    }
                    else if (!f.Include)
                    {
                        if (match)
                        {
                            excludeFail = true;
                            break;
                        }
                    }
                }

                if (!includeOk || excludeFail)
                    continue;

                _visibleLines.Add(line);
            }

            _lineList.BeginUpdate();
            _lineList.Items.Clear();
            foreach (var line in _visibleLines)
            {
                _lineList.Items.Add(line);
            }
            _lineList.EndUpdate();

            if (_followTail && _visibleLines.Count > 0)
            {
                int lastIndex = _visibleLines.Count - 1;
                _lineList.SelectedIndex = lastIndex;
                _lineList.TopIndex = Math.Max(lastIndex - 3, 0);
            }

            UpdateStatusBar();
            _scrollMapPanel?.Invalidate();    // NEW
        }


        private void ClearLines()
        {
            _allLines.Clear();
            _visibleLines.Clear();

            _lineList.Items.Clear();
            UpdateStatusBar();
        }

        private void ReloadFile()
        {
            _timer?.Stop();
            _reader?.Dispose();
            _stream?.Dispose();

            _allLines.Clear();
            _visibleLines.Clear();
            _lineList.Items.Clear();

            StartTailing();
        }

        private void CopyAllVisibleToClipboard()
        {
            if (_visibleLines.Count == 0)
                return;

            var sb = new System.Text.StringBuilder();
            foreach (var line in _visibleLines)
            {
                sb.AppendLine(line.Text);
            }

            try
            {
                Clipboard.SetText(sb.ToString());
            }
            catch
            {
                // ignore
            }
        }

        private void CopySelectedToClipboard()
        {
            if (_lineList.SelectedItems.Count == 0)
                return;

            var sb = new System.Text.StringBuilder();
            foreach (var obj in _lineList.SelectedItems)
            {
                if (obj is LogLine line)
                    sb.AppendLine(line.Text);
            }

            try
            {
                Clipboard.SetText(sb.ToString());
            }
            catch
            {
                // ignore
            }
        }

        // --- Drawing & status ---

        private void OnLineListDrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();

            if (e.Index < 0 || e.Index >= _lineList.Items.Count)
                return;

            if (_lineList.Items[e.Index] is not LogLine line)
                return;

            Color backColor = e.BackColor;
            Color foreColor = e.ForeColor;

            // Base on log type
            switch (line.Type)
            {
                case LogMessageType.Warn:
                    backColor = Color.FromArgb(255, 255, 220);   // pale yellow
                    foreColor = Color.Black;
                    break;
                case LogMessageType.Error:
                case LogMessageType.Fatal:
                    backColor = Color.FromArgb(255, 220, 220);   // pale red
                    foreColor = Color.Black;
                    break;
                case LogMessageType.Debug:
                    backColor = Color.White;
                    foreColor = Color.DimGray;
                    break;
                case LogMessageType.Info:
                    backColor = Color.White;
                    foreColor = Color.Black;
                    break;
                case LogMessageType.Other:
                    backColor = Color.White;
                    foreColor = Color.DarkSlateGray;
                    break;
            }

            if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
            {
                backColor = SystemColors.Highlight;
                foreColor = SystemColors.HighlightText;
            }

            using (var backBrush = new SolidBrush(backColor))
            using (var foreBrush = new SolidBrush(foreColor))
            {
                e.Graphics.FillRectangle(backBrush, e.Bounds);

                // NEW: marker bar on the left if any marker is set
                bool hasMarker = line.Markers.Any(m => m);
                int textOffsetX = e.Bounds.X + 2;

                if (hasMarker)
                {
                    var markerRect = new Rectangle(e.Bounds.X, e.Bounds.Y, 4, e.Bounds.Height);
                    using (var markerBrush = new SolidBrush(Color.Orange))
                    {
                        e.Graphics.FillRectangle(markerBrush, markerRect);
                    }
                    textOffsetX += 6;
                }

                e.Graphics.DrawString(
                    line.Text,
                    e.Font,
                    foreBrush,
                    new PointF(textOffsetX, e.Bounds.Y));
            }

            e.DrawFocusRectangle();
        }

        private void UpdateStatusBar()
        {
            int selected = _lineList.SelectedItems.Count;
            int filtered = _visibleLines.Count;
            int total = _allLines.Count;

            _statusSel.Text = $"Sel: {selected}";
            _statusFil.Text = $"Fil: {filtered}";
            _statusTotal.Text = $"Total: {total}";

            _statusFunnel.Text = _showOnlyFilteredLines ? "🔽 Filtered" : " ";
        }

        private void FilterGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _filters.Count)
                return;

            var filter = _filters[e.RowIndex];

            if (_filterGrid.Columns[e.ColumnIndex].HeaderText == "Type")
            {
                e.Value = filter.Include ? "Include" : "Exclude";
                e.FormattingApplied = true;
            }
        }

        // --- Filter management ---

        private void AddFilter()
        {
            using (var dlg = new FilterEditDialog())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                var filter = new LogFilter
                {
                    Include = dlg.Include,
                    MatchMode = dlg.Mode,
                    Pattern = dlg.Pattern,
                    Enabled = true
                };

                _filters.Add(filter);
                ReindexFilterKeys();
                RebuildVisibleLines();
                SaveFilterConfig();
            }
        }

        private void EditSelectedFilter()
        {
            if (_filterGrid.CurrentRow == null)
                return;

            var filter = _filterGrid.CurrentRow.DataBoundItem as LogFilter;
            if (filter == null)
                return;

            using (var dlg = new FilterEditDialog())
            {
                dlg.Include = filter.Include;
                dlg.Mode = filter.MatchMode;
                dlg.Pattern = filter.Pattern;

                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                filter.Include = dlg.Include;
                filter.MatchMode = dlg.Mode;
                filter.Pattern = dlg.Pattern;
                filter.CompiledRegex = null; // force recompile

                _filterGrid.Refresh();
                RebuildVisibleLines();
                SaveFilterConfig();
            }
        }

        private void RemoveSelectedFilter()
        {
            if (_filterGrid.CurrentRow == null)
                return;

            var filter = _filterGrid.CurrentRow.DataBoundItem as LogFilter;
            if (filter == null)
                return;

            _filters.Remove(filter);
            ReindexFilterKeys();
            RebuildVisibleLines();
            SaveFilterConfig();
        }

        private void ToggleSelectedFilterOnOff()
        {
            if (_filterGrid.CurrentRow == null)
                return;

            var filter = _filterGrid.CurrentRow.DataBoundItem as LogFilter;
            if (filter == null)
                return;

            filter.Enabled = !filter.Enabled;
            _filterGrid.Refresh();
            RebuildVisibleLines();
            SaveFilterConfig();
        }

        private void MoveSelectedFilter(int delta)
        {
            if (_filterGrid.CurrentRow == null)
                return;

            int index = _filterGrid.CurrentRow.Index;
            int newIndex = index + delta;
            if (newIndex < 0 || newIndex >= _filters.Count)
                return;

            var item = _filters[index];
            _filters.RemoveAt(index);
            _filters.Insert(newIndex, item);

            _filterGrid.ClearSelection();
            _filterGrid.Rows[newIndex].Selected = true;
            _filterGrid.CurrentCell = _filterGrid.Rows[newIndex].Cells[0];

            ReindexFilterKeys();
            RebuildVisibleLines();
            SaveFilterConfig();
        }

        private void ReindexFilterKeys()
        {
            for (int i = 0; i < _filters.Count; i++)
            {
                _filters[i].Key = (char)('A' + i);
            }
        }

        // --- Keyboard handling ---

        private void OnFilterGridKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Space:
                    ToggleSelectedFilterOnOff();
                    e.Handled = true;
                    break;

                case Keys.Delete:
                    RemoveSelectedFilter();
                    e.Handled = true;
                    break;

                case Keys.N when e.Control:
                    AddFilter();
                    e.Handled = true;
                    break;

                case Keys.Up when e.Alt:
                    MoveSelectedFilter(-1);
                    e.Handled = true;
                    break;

                case Keys.Down when e.Alt:
                    MoveSelectedFilter(1);
                    e.Handled = true;
                    break;
            }
        }

        private void OnLineListKeyDown(object sender, KeyEventArgs e)
        {
            // Space / Shift+Space – any filter match
            if (e.KeyCode == Keys.Space && !e.Control && !e.Alt)
            {
                if (e.Shift)
                    JumpToPreviousAnyFilterMatch();
                else
                    JumpToNextAnyFilterMatch();

                e.Handled = true;
                return;
            }

            // A..Z – filter navigation
            if (e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z && !e.Control && !e.Alt)
            {
                char key = (char)e.KeyCode;
                var filter = FindFilterByKey(key);
                if (filter != null && filter.Enabled)
                {
                    if (e.Shift)
                        JumpToPreviousFilterMatch(filter);
                    else
                        JumpToNextFilterMatch(filter);

                    e.Handled = true;
                }
                return;
            }

            // 1..8, Ctrl+1..8 – markers
            if (IsMarkerKey(e.KeyCode))
            {
                int markerIndex = MarkerIndexFromKey(e.KeyCode);
                if (markerIndex >= 0 && markerIndex < MarkerCount)
                {
                    if (e.Control)
                    {
                        // Toggle marker for selected line(s)
                        ToggleMarkerForSelection(markerIndex);
                    }
                    else
                    {
                        if (e.Shift)
                            JumpToPreviousMarkerMatch(markerIndex);
                        else
                            JumpToNextMarkerMatch(markerIndex);
                    }

                    e.Handled = true;
                }
            }
        }

        private static bool IsMarkerKey(Keys key)          // NEW
        {
            return (key >= Keys.D1 && key <= Keys.D8) ||
                   (key >= Keys.NumPad1 && key <= Keys.NumPad8);
        }

        private static int MarkerIndexFromKey(Keys key)    // NEW
        {
            if (key >= Keys.D1 && key <= Keys.D8)
                return key - Keys.D1;
            if (key >= Keys.NumPad1 && key <= Keys.NumPad8)
                return key - Keys.NumPad1;
            return -1;
        }

        private void ToggleMarkerForSelection(int marker)  // NEW
        {
            if (_lineList.SelectedIndices.Count == 0)
                return;

            foreach (int idx in _lineList.SelectedIndices)
            {
                if (idx < 0 || idx >= _visibleLines.Count) continue;
                var line = _visibleLines[idx];
                line.Markers[marker] = !line.Markers[marker];
            }

            _lineList.Invalidate();
            _scrollMapPanel?.Invalidate();
        }

        private void JumpToNextMarkerMatch(int marker)     // NEW
        {
            if (_visibleLines.Count == 0)
                return;

            int start = _lineList.SelectedIndex;
            int idx = start < 0 ? 0 : start + 1;

            for (int i = 0; i < _visibleLines.Count; i++)
            {
                int j = (idx + i) % _visibleLines.Count;
                if (_visibleLines[j].Markers[marker])
                {
                    _lineList.SelectedIndex = j;
                    _lineList.TopIndex = Math.Max(j - 3, 0);
                    break;
                }
            }
        }

        private void JumpToPreviousMarkerMatch(int marker) // NEW
        {
            if (_visibleLines.Count == 0)
                return;

            int start = _lineList.SelectedIndex;
            int idx = start < 0 ? _visibleLines.Count - 1 : start - 1;

            for (int i = 0; i < _visibleLines.Count; i++)
            {
                int j = idx - i;
                if (j < 0) j += _visibleLines.Count;

                if (_visibleLines[j].Markers[marker])
                {
                    _lineList.SelectedIndex = j;
                    _lineList.TopIndex = Math.Max(j - 3, 0);
                    break;
                }
            }
        }


        private LogFilter FindFilterByKey(char key)
        {
            foreach (var f in _filters)
            {
                if (char.ToUpperInvariant(f.Key) == char.ToUpperInvariant(key))
                    return f;
            }

            return null;
        }

        private void JumpToNextAnyFilterMatch()
        {
            if (_visibleLines.Count == 0)
                return;

            int start = _lineList.SelectedIndex;
            int idx = start < 0 ? 0 : start + 1;

            for (int i = 0; i < _visibleLines.Count; i++)
            {
                int j = (idx + i) % _visibleLines.Count;
                var line = _visibleLines[j];
                if (LineMatchesAnyEnabledFilter(line))
                {
                    _lineList.SelectedIndex = j;
                    _lineList.TopIndex = Math.Max(j - 3, 0);
                    break;
                }
            }
        }

        private void JumpToPreviousAnyFilterMatch()
        {
            if (_visibleLines.Count == 0)
                return;

            int start = _lineList.SelectedIndex;
            int idx = start < 0 ? _visibleLines.Count - 1 : start - 1;

            for (int i = 0; i < _visibleLines.Count; i++)
            {
                int j = (idx - i);
                if (j < 0)
                    j += _visibleLines.Count;

                var line = _visibleLines[j];
                if (LineMatchesAnyEnabledFilter(line))
                {
                    _lineList.SelectedIndex = j;
                    _lineList.TopIndex = Math.Max(j - 3, 0);
                    break;
                }
            }
        }

        private bool LineMatchesAnyEnabledFilter(LogLine line)
        {
            foreach (var f in _filters)
            {
                if (!f.Enabled) continue;
                if (f.IsMatch(line.Text))
                    return true;
            }
            return false;
        }

        private void JumpToNextFilterMatch(LogFilter filter)
        {
            if (_visibleLines.Count == 0)
                return;

            int start = _lineList.SelectedIndex;
            int idx = start < 0 ? 0 : start + 1;

            for (int i = 0; i < _visibleLines.Count; i++)
            {
                int j = (idx + i) % _visibleLines.Count;
                var line = _visibleLines[j];
                if (filter.IsMatch(line.Text))
                {
                    _lineList.SelectedIndex = j;
                    _lineList.TopIndex = Math.Max(j - 3, 0);
                    break;
                }
            }
        }

        private void JumpToPreviousFilterMatch(LogFilter filter)
        {
            if (_visibleLines.Count == 0)
                return;

            int start = _lineList.SelectedIndex;
            int idx = start < 0 ? _visibleLines.Count - 1 : start - 1;

            for (int i = 0; i < _visibleLines.Count; i++)
            {
                int j = (idx - i);
                if (j < 0)
                    j += _visibleLines.Count;

                var line = _visibleLines[j];
                if (filter.IsMatch(line.Text))
                {
                    _lineList.SelectedIndex = j;
                    _lineList.TopIndex = Math.Max(j - 3, 0);
                    break;
                }
            }
        }

        private void OnScrollMapPanelPaint(object sender, PaintEventArgs e)   // NEW
        {
            if (_visibleLines.Count == 0)
                return;

            var enabledFilters = _filters
                .Where(f => f.Enabled && !string.IsNullOrEmpty(f.Pattern))
                .ToList();

            int enabledCount = enabledFilters.Count;
            if (enabledCount == 0)
                return;

            int width = _scrollMapPanel.ClientSize.Width;
            int height = _scrollMapPanel.ClientSize.Height;
            if (width <= 0 || height <= 0)
                return;

            int nLines = _visibleLines.Count;
            if (nLines == 0)
                return;

            int stripeWidth = Math.Max(1, width / enabledCount);

            for (int lineIndex = 0; lineIndex < nLines; lineIndex++)
            {
                var line = _visibleLines[lineIndex];
                float ratio = (float)lineIndex / Math.Max(1, nLines - 1);
                int y = (int)(ratio * (height - 1));

                int stripeIndex = 0;
                foreach (var f in enabledFilters)
                {
                    if (f.IsMatch(line.Text))
                    {
                        var color = FilterColorPalette[stripeIndex % FilterColorPalette.Length];
                        using (var pen = new Pen(color))
                        {
                            int x0 = stripeIndex * stripeWidth;
                            int x1 = Math.Min(x0 + stripeWidth - 1, width - 1);
                            e.Graphics.DrawLine(pen, x0, y, x1, y);
                        }
                    }

                    stripeIndex++;
                }
            }
        }

        private void OnScrollMapPanelMouseDown(object sender, MouseEventArgs e)   // NEW
        {
            if (e.Button != MouseButtons.Left)
                return;

            _scrollMapDragging = true;
            ScrollMapJumpTo(e.Y);
        }

        private void OnScrollMapPanelMouseMove(object sender, MouseEventArgs e)   // NEW
        {
            if (_scrollMapDragging && e.Button == MouseButtons.Left)
            {
                ScrollMapJumpTo(e.Y);
            }
        }

        private void OnScrollMapPanelMouseUp(object sender, MouseEventArgs e)     // NEW
        {
            if (e.Button == MouseButtons.Left)
            {
                _scrollMapDragging = false;
            }
        }

        private void ScrollMapJumpTo(int y)                                       // NEW
        {
            if (_visibleLines.Count == 0)
                return;

            int height = _scrollMapPanel.ClientSize.Height;
            if (height <= 0)
                return;

            float ratio = Math.Clamp((float)y / height, 0f, 1f);
            int index = (int)(ratio * (_visibleLines.Count - 1));

            _lineList.SelectedIndex = index;
            _lineList.TopIndex = Math.Max(index - 3, 0);
        }


        // --- Filter edit dialog ---

        private sealed class FilterEditDialog : Form
        {
            private RadioButton _rbInclude;
            private RadioButton _rbExclude;
            private RadioButton _rbBasic;
            private RadioButton _rbWildcard;
            private RadioButton _rbRegex;
            private TextBox _txtPattern;
            private Button _btnOk;
            private Button _btnCancel;

            public bool Include { get; set; } = true;
            public FilterMatchMode Mode { get; set; } = FilterMatchMode.Basic;
            public string Pattern { get; set; } = string.Empty;

            public FilterEditDialog()
            {
                Text = "Edit Filter";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MinimizeBox = false;
                MaximizeBox = false;
                ClientSize = new Size(420, 220);
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
                    Height = 80
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

                _btnOk = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Left = 230,
                    Top = 170,
                    Width = 80
                };
                _btnOk.Click += (_, __) => OnOk();

                _btnCancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Left = 320,
                    Top = 170,
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
                };
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

                Close();
            }
        }
    }
}
