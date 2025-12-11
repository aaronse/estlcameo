using System;
using System.Windows.Forms;

namespace EstlCameo
{
    public partial class TrayForm : Form
    {
        private NotifyIcon trayIcon;
        private SnapshotManager snapshot;
        private KeyboardHook keyboard;
        private SnapshotViewerForm _snapshotViewer;

        public TrayForm()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;

            trayIcon = new NotifyIcon
            {
                // Project → Properties → Resources → Add Existing File...
                Icon =  Properties.Resources.estlcameo,
                //Icon = new Icon("Assets/estlcameo.ico"),
                Visible = true,
                Text = "EstlCameo – Snapshot Undo for Estlcam"
            };

            trayIcon.ContextMenuStrip = BuildMenu();

            snapshot = new SnapshotManager();
            keyboard = new KeyboardHook();

            keyboard.CtrlZPressed += OnUndo;
            keyboard.CtrlYPressed += OnRedo;
            keyboard.CtrlRPressed += OnReviewSnapshots;
            keyboard.CtrlSPressed += OnCtrlS;
            snapshot.SaveExpectedButNotObserved += OnSaveExpectedButNotObserved;
        }


        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();

            // Open Snapshot Viewer
            menu.Items.Add(new ToolStripMenuItem("Open Snapshot Viewer", null, (_, __) =>
            {
                if (_snapshotViewer == null || _snapshotViewer.IsDisposed)
                {
                    _snapshotViewer = new SnapshotViewerForm(snapshot);
                }

                _snapshotViewer.BringToFrontAndFocus();
            }));

            // 🔍 NEW: Open Log Folder
            menu.Items.Add(new ToolStripMenuItem("Open Log Folder…", null, (_, __) =>
            {
                try
                {
                    string logDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "EstlCameo");

                    Directory.CreateDirectory(logDir); // Just to be safe

                    // Explorer will select the .log file if it exists
                    string logPath = Path.Combine(logDir, "EstlCameo.log");
                    if (File.Exists(logPath))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{logPath}\"");
                    }
                    else
                    {
                        // Fallback: open the folder
                        System.Diagnostics.Process.Start("explorer.exe", $"\"{logDir}\"");
                    }
                }
                catch (Exception ex)
                {
                    Toast.Show("Unable to open log folder.\n" + ex.Message);
                }
            }));

            // Exit
            menu.Items.Add("Exit", null, (_, __) => Application.Exit());

            return menu;
        }


        private void OnUndo()
        {
            if (!EstlcamInterop.IsEstlcamForeground()) return;
            snapshot.Undo();
        }


        private void OnRedo()
        {
            if (!EstlcamInterop.IsEstlcamForeground()) return;
            snapshot.Redo();
        }


        private void OnReviewSnapshots()
        {
            // 0) If we are already tracking a project, check if Estlcam appears
            // to be showing a *different* .E12 project now.
            if (!string.IsNullOrEmpty(snapshot.WorkingFilePath) &&
                IsEstlcamProjectFile(snapshot.WorkingFilePath) &&
                TryGetActiveEstlcamProjectFileName(out var activeProjectFileName))
            {
                string trackedBase = Path.GetFileNameWithoutExtension(snapshot.WorkingFilePath);
                string activeBase = Path.GetFileNameWithoutExtension(activeProjectFileName);

                if (!trackedBase.Equals(activeBase, StringComparison.OrdinalIgnoreCase))
                {
                    Toast.Show(
                        "EstlCameo: I think you switched projects…\n" +
                        $"I was creating snapshots for: {trackedBase}\n" +
                        $"Estlcam now shows: {activeBase}\n\n" +
                        "I’ve paused snapshots for the old file.\n" +
                        "Press Ctrl+S on this new project to start tracking it,\n" +
                        "then press Ctrl+R to review its snapshots."
                    );

                    snapshot.SetWorkingFile(null);
                    return;
                }
            }

            // 1) If we don't yet have a working file, try to attach to the active .E12 project.
            if (string.IsNullOrEmpty(snapshot.WorkingFilePath))
            {
                // If Estlcam doesn't have an active .E12 file (blank workspace or DXF, etc.),
                // quietly do nothing — no dialog, no toast.
                if (!TryGetActiveEstlcamProjectFileName(out activeProjectFileName))
                {
                    return;
                }

                // 1a) Try auto-resolve from foreground Estlcam (no prompt yet)
                string path = TryResolveProjectForForegroundEstlcam(promptIfUnknown: false);

                // 1b) If still unknown, use same branded dialog + file picker
                if (string.IsNullOrEmpty(path))
                {
                    if (!ShowProjectResolutionDialog(activeProjectFileName, out var userPath))
                    {
                        Toast.Show(
                            "EstlCameo: No project selected.\n" +
                            "I couldn’t auto-detect a project. Press Ctrl+R again\n" +
                            "and pick a .E12 file to review its snapshots."
                        );
                        return;
                    }

                    path = userPath;
                }

                // 1c) Only accept .E12 files as valid projects.
                if (!IsEstlcamProjectFile(path) || !File.Exists(path))
                {
                    Toast.Show(
                        "EstlCameo: Snapshot review not available.\n" +
                        "EstlCameo only tracks Estlcam .E12 project files.\n" +
                        "Please pick a .E12 file in the Project Selection dialog."
                    );
                    return;
                }

                snapshot.SetWorkingFile(path);

                string projectName = Path.GetFileName(path);
                Toast.Show(
                    "EstlCameo: Now tracking this project\n" +
                    projectName
                );
            }

            // 2) If we *still* don't have a valid .E12 working file, give up quietly.
            if (string.IsNullOrEmpty(snapshot.WorkingFilePath) ||
                !IsEstlcamProjectFile(snapshot.WorkingFilePath))
                return;

            // 3) Show viewer for the currently bound .E12 project
            if (_snapshotViewer == null || _snapshotViewer.IsDisposed)
            {
                _snapshotViewer = new SnapshotViewerForm(snapshot);
            }

            _snapshotViewer.BringToFrontAndFocus();
        }



        private void OnCtrlS()
        {
            // 0) If Estlcam isn't showing an .E12 project (blank workspace, DXF, SVG, JPG, etc.),
            // silently ignore the hotkey. No dialog, no toast.
            if (!TryGetActiveEstlcamProjectFileName(out var activeProjectFileName))
            {
                // No active Estlcam project → nothing to do.
                return;
            }

            bool hadWorkingFile = !string.IsNullOrEmpty(snapshot.WorkingFilePath);

            // 1) If we're already tracking a project, check for a project switch.
            if (hadWorkingFile)
            {
                // Safety: only treat the tracked file as valid if it is an .E12 as well.
                if (!IsEstlcamProjectFile(snapshot.WorkingFilePath))
                {
                    // Legacy or bad state – detach defensively.
                    snapshot.SetWorkingFile(null);
                    return;
                }

                string trackedBase = Path.GetFileNameWithoutExtension(snapshot.WorkingFilePath);
                string activeBase = Path.GetFileNameWithoutExtension(activeProjectFileName);

                if (!trackedBase.Equals(activeBase, StringComparison.OrdinalIgnoreCase))
                {
                    // Estlcam is now showing a different project than the one we're tracking.
                    // Detach, explain what's happening, and let the user explicitly re-attach.
                    Toast.Show(
                        "EstlCameo: I think you switched projects…\n" +
                        $"I was creating snapshots for: {trackedBase}\n" +
                        $"Estlcam now shows: {activeBase}\n\n" +
                        "I’ve paused snapshots for the old file.\n" +
                        "Press Ctrl+S again on this new project to start tracking it."
                    );

                    snapshot.SetWorkingFile(null);
                    return;
                }

                // Still on the same project we're tracking → normal path.
                snapshot.ExpectSaveFromCtrlS();
                return;
            }

            // 2) First-attach flow (we know Estlcam has an active file, but EstlCameo
            // is not yet bound to any WorkingFilePath).

            // 2a) Best effort: resolve full path from foreground Estlcam without prompting.
            string path = TryResolveProjectForForegroundEstlcam(promptIfUnknown: false);

            // 2b) If still unknown, ask the user once via the project resolution dialog.
            if (string.IsNullOrEmpty(path))
            {
                if (!ShowProjectResolutionDialog(activeProjectFileName, out var userPath))
                {
                    Toast.Show(
                        "EstlCameo: Snapshot not created.\n" +
                        "I couldn’t identify this project.\n" +
                        "Press Ctrl+R or use the tray menu to link a project."
                    );
                    return;
                }

                path = userPath;
            }

            // 2c) Only accept .E12 files as valid projects.
            if (!IsEstlcamProjectFile(path) || !File.Exists(path))
            {
                Toast.Show(
                    "EstlCameo: Snapshot not created.\n" +
                    "EstlCameo only tracks Estlcam .E12 project files.\n" +
                    "Please pick a .E12 file in the Project Selection dialog."
                );
                return;
            }

            // 2d) Bind and create an immediate snapshot for this .E12 project.
            snapshot.SetWorkingFile(path);
            snapshot.CreateSnapshotNow("First attach via Ctrl+S");

            string projectName = Path.GetFileName(path);
            Toast.Show(
                "EstlCameo: Now tracking this project\n" +
                projectName
            );
        }


        /// <summary>
        /// Try to resolve the current Estlcam project full path for the foreground Estlcam window.
        /// Returns null if it cannot be resolved.
        /// </summary>
        private string TryResolveProjectForForegroundEstlcam(bool promptIfUnknown)
        {
            if (!EstlcamInterop.TryGetForegroundEstlcamInfo(out var pid, out var caption))
                return null;

            string fileName = EstlcamInterop.ExtractFileNameFromCaption(caption);
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            // 1) Best-effort from State CAM
            string fullPath = StateCamResolver.ResolveProjectPath(fileName);
            if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
                return fullPath;

            if (!promptIfUnknown)
                return null;

            // TODO: Remove if promptIfUnknown always false
            // 2) Prompt user to pick project file
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Select Estlcam project file for snapshots (Resolving Active EstlCam)";
                dlg.Filter = "Estlcam 12 project (*.e12)|*.e12|Estlcam 11 project (*.e10)|*.e10|All files (*.*)|*.*";
                dlg.FileName = fileName;

                // Optional: seed with Dir projects from State CAM
                var state = StateCamResolver.Load(fileName);
                if (!string.IsNullOrEmpty(state.DirProjects) && Directory.Exists(state.DirProjects))
                {
                    dlg.InitialDirectory = state.DirProjects;
                }

                var result = dlg.ShowDialog(this);
                if (result == DialogResult.OK && File.Exists(dlg.FileName))
                    return dlg.FileName;
            }

            return null;
        }


        private void OnSaveExpectedButNotObserved()
        {
            if (string.IsNullOrEmpty(snapshot.WorkingFilePath))
                return; // Shouldn’t happen with new logic, but be defensive

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(OnSaveExpectedButNotObserved));
                return;
            }

            // Double-check Estlcam foreground; if user alt-tabbed away, we can quietly ignore
            if (!EstlcamInterop.TryGetForegroundEstlcamInfo(out var pid, out var caption))
                return;

            string fileName = EstlcamInterop.ExtractFileNameFromCaption(caption) ?? "";

            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Select Estlcam 12 project file for snapshots (Expected Save)";
                dlg.Filter = "Estlcam 12 project (*.e12)|*.e12|Estlcam 11 project (*.e10)|*.e10|All files (*.*)|*.*";
                dlg.FileName = fileName;

                // Try to seed initial directory from State CAM
                var state = StateCamResolver.Load(fileName);
                if (!string.IsNullOrEmpty(state.DirProjects) && Directory.Exists(state.DirProjects))
                {
                    dlg.InitialDirectory = state.DirProjects;
                }

                var result = dlg.ShowDialog(this);
                if (result == DialogResult.OK && File.Exists(dlg.FileName))
                {
                    snapshot.SetWorkingFile(dlg.FileName);
                    Toast.Show("EstlCameo: Now tracking project\n" + dlg.FileName);
                }
            }
        }


        private bool ShowProjectResolutionDialog(string activeProjectFileName, out string selectedPath)
        {
            selectedPath = null;

            using (var dlg = new ProjectResolutionDialog())
            {
                var result = dlg.ShowDialog(this);
                if (result != DialogResult.OK)
                {
                    // User cancelled at the explanatory dialog
                    return false;
                }
            }

            // User chose "Select project file…" → now show file picker
            using (var picker = new OpenFileDialog())
            {
                picker.Title = "Select Estlcam project file for snapshots (Resolving)";
                picker.Filter = "Estlcam 12 project (*.e12)|*.e12|Estlcam 11 project (*.e10)|*.e10|All files (*.*)|*.*";

                // Seed from State CAM if possible
                var state = StateCamResolver.Load(activeProjectFileName);
                if (!string.IsNullOrEmpty(state.DirProjects) && System.IO.Directory.Exists(state.DirProjects))
                {
                    picker.InitialDirectory = state.DirProjects;
                }

                var result = picker.ShowDialog(this);
                if (result == DialogResult.OK && System.IO.File.Exists(picker.FileName))
                {
                    selectedPath = picker.FileName;
                    return true;
                }
            }

            return false;
        }



        /// <summary>
        /// Returns true if the foreground Estlcam window has an active .E12 project.
        /// Ignores non-project imports like .DXF, .SVG, .JPG, etc.
        /// </summary>
        private bool TryGetActiveEstlcamProjectFileName(out string activeProjectFileName)
        {
            activeProjectFileName = null;

            if (!EstlcamInterop.TryGetForegroundEstlcamInfo(out var pid, out var caption))
                return false;

            string fileName = EstlcamInterop.ExtractFileNameFromCaption(caption);
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var isProjectFile = IsEstlcamProjectFile(fileName);
            if (isProjectFile)
            {
                activeProjectFileName = fileName;
            }

            return isProjectFile;
        }


        private static bool IsEstlcamProjectFile(string pathOrFileName)
        {
            if (string.IsNullOrWhiteSpace(pathOrFileName))
                return false;

            return string.Equals(
                Path.GetExtension(pathOrFileName),
                ".e12",
                StringComparison.OrdinalIgnoreCase
            ) ||
            string.Equals(
                Path.GetExtension(pathOrFileName),
                ".e10",
                StringComparison.OrdinalIgnoreCase
            );
        }



    }
}
