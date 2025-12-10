using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
// if you use ScreenshotHelper: using System.Drawing; using System.Drawing.Imaging; live in ScreenshotHelper.cs, not here


namespace EstlCameo
{
    public class SnapshotManager
    {
        private string workingFile;
        private string workingExt;
        private string snapshotDir;
        private readonly List<SnapshotInfo> snapshots = new();
        private readonly HashSet<string> snapshotPaths = new(StringComparer.OrdinalIgnoreCase);

        private int index = -1;
        
        private FileSystemWatcher watcher;
        private DateTime lastSnapshotTime = DateTime.MinValue;

        // Timer to monitor for File Save expectation after recent Save key shortcut detected.
        private bool waitingForSave;
        private System.Threading.Timer saveExpectationTimer;
        private readonly object saveLock = new();
        private const int EXPECTED_FILE_WRITE_TIMER_SECS = 3;

        public event Action SaveExpectedButNotObserved;

        public string WorkingFilePath => workingFile;


        public SnapshotManager()
        {
            Log.Debug("SnapshotManager ctr()");
            // Initially unconfigured. Snapshot viewing will be empty
            // until SetWorkingFile() is called.
        }

        public void SetWorkingFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException(nameof(path));

            if (string.Equals(workingFile, path, StringComparison.OrdinalIgnoreCase))
                return; // already tracking

            workingFile = path;
            workingExt = Path.GetExtension(workingFile);

            snapshotDir = Path.Combine(
                Path.GetDirectoryName(workingFile) ?? "",
                ".snapshots",
                Path.GetFileNameWithoutExtension(workingFile) ?? "default");

            Directory.CreateDirectory(snapshotDir);

            LoadExistingSnapshotsFromDisk();

            // Recreate watcher
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            watcher = new FileSystemWatcher(
                Path.GetDirectoryName(workingFile) ?? "",
                Path.GetFileName(workingFile) ?? "*.*");

            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
            watcher.Changed += (_, __) => OnFileSaved();
            watcher.EnableRaisingEvents = true;

            Log.Debug($"SetWorkingFile, now tracking: {workingFile}");
        }


        private void LoadExistingSnapshotsFromDisk()
        {
            snapshots.Clear();
            snapshotPaths.Clear();

            if (!Directory.Exists(snapshotDir))
            {
                Log.Debug($"LoadExistingSnapshotsFromDisk, snapshotDir does not exist: {snapshotDir}");
                return;
            }

            try
            {
                // Only files matching the same extension as workingFile
                var files = Directory.GetFiles(snapshotDir, "*" + workingExt);

                foreach (var file in files)
                {
                    var info = CreateSnapshotInfoFromPath(file);
                    snapshots.Add(info);
                    snapshotPaths.Add(file);
                }

                // Sort ascending by timestamp
                snapshots.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                index = snapshots.Count - 1;

                Log.Debug($"LoadExistingSnapshotsFromDisk, loaded {snapshots.Count} snapshots from {snapshotDir}");
            }
            catch (Exception ex)
            {
                Log.Debug($"LoadExistingSnapshotsFromDisk, error: {ex}");
            }
        }


        private SnapshotInfo CreateSnapshotInfoFromPath(string snapshotPath)
        {
            string baseName = Path.GetFileNameWithoutExtension(snapshotPath) ?? "";
            DateTime ts = ParseTimestampFromBaseName(baseName)
                          ?? File.GetCreationTime(snapshotPath);

            return new SnapshotInfo
            {
                Timestamp = ts,
                SnapshotPath = snapshotPath,
                PreviewImagePath = Path.Combine(
                    Path.GetDirectoryName(snapshotPath) ?? "",
                    baseName + ".png"),
                RelativeText = FormatRelativeTime(ts)
            };
        }


        private DateTime? ParseTimestampFromBaseName(string baseName)
        {
            // We expect something like: 20251205_091230 or 20251205_091230_1
            // So we take the first 15 chars "yyyyMMdd_HHmmss"
            if (string.IsNullOrWhiteSpace(baseName) || baseName.Length < 15)
                return null;

            string stamp = baseName.Substring(0, 15); // "yyyyMMdd_HHmmss"

            if (DateTime.TryParseExact(
                    stamp,
                    "yyyyMMdd_HHmmss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var parsed))
            {
                return parsed;
            }

            return null;
        }


        public IReadOnlyList<SnapshotInfo> GetSnapshots()
        {
            // Ensure we have the latest snapshots from disk
            LoadExistingSnapshotsFromDisk();
            return snapshots.AsReadOnly();
        }


        public void ExpectSaveFromCtrlS(TimeSpan? timeout = null)
        {
            if (string.IsNullOrEmpty(workingFile))
            {
                Log.Debug("ExpectSaveFromCtrlS called but workingFile is not set.");
                return;
            }

            var to = timeout ?? TimeSpan.FromSeconds(EXPECTED_FILE_WRITE_TIMER_SECS);

            lock (saveLock)
            {
                waitingForSave = true;
                saveExpectationTimer?.Dispose();

                saveExpectationTimer = new System.Threading.Timer(_ =>
                {
                    lock (saveLock)
                    {
                        if (!waitingForSave)
                        {
                            Log.Debug("ExpectSaveFromCtrlS, timed out with snapshot observed.");
                        }
                        else
                        {
                            waitingForSave = false;
                            Log.Debug("ExpectSaveFromCtrlS, timed out without snapshot.");
                            SaveExpectedButNotObserved?.Invoke();
                        }
                    }
                }, null, to, System.Threading.Timeout.InfiniteTimeSpan);
            }
        }

        public void CreateSnapshotNow(string reason = null)
        {
            if (string.IsNullOrEmpty(workingFile))
                return;

            Log.Debug($"CreateSnapshotNow: {reason ?? "(no reason)"}");
            CreateSnapshotInternal();
        }

        private void CreateSnapshotInternal()
        {
            var now = DateTime.Now;

            if (string.IsNullOrEmpty(workingFile))
            {
                Log.Debug("CreateSnapshotInternal, aborting, workingFile is null/empty");
                return;
            }

            if (!File.Exists(workingFile))
            {
                Log.Debug($"CreateSnapshotInternal, aborting, workingFile no longer exists: {workingFile}");
                return;
            }

            // Ensure path for snapshot directory exists
            EnsureSnapshotDirectory();

            try
            {
                string stamp = now.ToString("yyyyMMdd_HHmmss");
                string baseName = stamp;

                string dest = Path.Combine(snapshotDir, $"{baseName}{workingExt}");
                string pngPath = Path.Combine(snapshotDir, $"{baseName}.png");

                Log.Debug($"CreateSnapshotInternal, saving snapshot: {dest}");

                const int maxAttempts = 10;
                const int delayMs = 200;

                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        if (attempt == 1)
                        {
                            Thread.Sleep(delayMs);
                        }

                        File.Copy(workingFile, dest, overwrite: true);

                        Log.Debug($"CreateSnapshotInternal, success after {attempt} attempt(s): {dest}");

                        // Only after successful copy:
                        lastSnapshotTime = now;

                        if (!snapshotPaths.Contains(dest))
                        {
                            var info = new SnapshotInfo
                            {
                                Timestamp = now,
                                SnapshotPath = dest,
                                PreviewImagePath = pngPath,
                                RelativeText = FormatRelativeTime(now)
                            };

                            snapshots.Add(info);
                            snapshotPaths.Add(dest);
                            snapshots.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                            index = snapshots.Count - 1;
                        }

                        // Screenshot (best-effort)
                        try
                        {
                            ScreenshotHelper.CaptureEstlcamWindow(pngPath);
                            Log.Debug($"CreateSnapshotInternal, screenshot saved: {pngPath}");
                        }
                        catch (Exception exShot)
                        {
                            Log.Debug($"CreateSnapshotInternal, screenshot error: {exShot.Message}");
                        }

                        // Toast (best-effort)
                        try
                        {
                            Toast.ShowSnapshot("EstlCameo: Snapshot saved", dest, pngPath);
                            Log.Debug("CreateSnapshotInternal, toast shown");
                        }
                        catch (Exception exToast)
                        {
                            Log.Debug($"CreateSnapshotInternal, toast error: {exToast.Message}");
                        }

                        return;
                    }
                    catch (IOException ex)
                    {
                        Log.Debug($"CreateSnapshotInternal, attempt {attempt} failed: {ex.Message}");

                        if (attempt == maxAttempts)
                        {
                            Log.Debug($"CreateSnapshotInternal, giving up after {maxAttempts} attempts, no snapshot created for: {dest}");
                            return;
                        }

                        Thread.Sleep(delayMs);
                    }
                    catch (Exception ex)
                    {
                        Log.Debug($"CreateSnapshotInternal, unexpected error: {ex}");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("CreateSnapshotInternal outer exception: " + ex);
            }
        }


        private void OnFileSaved()
        {
            if (string.IsNullOrEmpty(workingFile))
            {
                Log.Debug($"OnFileSaved, skipping, no workingFile");
                return;
            }

            lock (saveLock)
            {
                waitingForSave = false;
            }

            var now = DateTime.Now;
            if ((now - lastSnapshotTime).TotalSeconds < 1)
            {
                Log.Debug("OnFileSaved, skipping, too soon since last snapshot");
                return;
            }

            CreateSnapshotInternal();
        }




        public void Undo()
        {
            if (string.IsNullOrEmpty(workingFile))
            {
                Log.Debug($"Undo, skipping, no workingFile");
                return;
            }

            if (index <= 0) return;
            index--;
            Restore(snapshots[index].SnapshotPath);
        }

        public void Redo()
        {
            if (string.IsNullOrEmpty(workingFile))
            {
                Log.Debug($"Redo, skipping, no workingFile");
                return;
            }

            if (index >= snapshots.Count - 1) return;
            index++;
            Restore(snapshots[index].SnapshotPath);
        }


        private void Restore(string snapshot)
        {
            try
            {
                if (string.IsNullOrEmpty(workingFile))
                {
                    Log.Debug("Restore, aborting, workingFile is null/empty");
                    Toast.Show("EstlCameo: No project file to restore into.");
                    return;
                }

                if (!File.Exists(snapshot))
                {
                    Log.Debug($"Restore, snapshot not found: {snapshot}");
                    Toast.Show("EstlCameo: Snapshot file no longer exists.");
                    return;
                }

                var targetDir = Path.GetDirectoryName(workingFile);
                if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
                {
                    Log.Debug($"Restore, target directory missing: {targetDir}");
                    Toast.Show("EstlCameo: Project folder is missing, cannot restore.");
                    return;
                }

                File.Copy(snapshot, workingFile, overwrite: true);
                EstlcamInterop.ReopenFile(workingFile);
                Log.Debug($"Restored {snapshot}");

                Toast.Show("EstlCameo: Restored snapshot");
            }
            catch (Exception ex)
            {
                Log.Debug($"Restore error: {ex}");
                Toast.Show("EstlCameo: Failed to restore snapshot.");
            }
        }


        public void OpenFolder()
        {
            try
            {
                EnsureSnapshotDirectory();

                if (!string.IsNullOrEmpty(snapshotDir) && Directory.Exists(snapshotDir))
                {
                    Process.Start("explorer.exe", snapshotDir);
                }
                else
                {
                    Toast.Show("EstlCameo: Snapshot folder not found");
                    Log.Debug($"OpenFolder, snapshotDir not found: {snapshotDir}");
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"OpenFolder error: {ex}");
                Toast.Show("EstlCameo: Unable to open snapshot folder");
            }
        }


        public string RestoreSnapshotAsCopy(SnapshotInfo snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            if (string.IsNullOrEmpty(workingFile))
                throw new InvalidOperationException("No working file set.");

            if (!File.Exists(snapshot.SnapshotPath))
                throw new FileNotFoundException("Snapshot file not found.", snapshot.SnapshotPath);

            string originalDir = Path.GetDirectoryName(workingFile);
            if (string.IsNullOrEmpty(originalDir) || !Directory.Exists(originalDir))
                throw new DirectoryNotFoundException($"Original project directory missing: {originalDir}");

            string originalBase = Path.GetFileNameWithoutExtension(workingFile);
            string ext = Path.GetExtension(workingFile);

            // Build base restored name with timestamp
            string stamp = snapshot.Timestamp.ToString("yyyyMMdd_HHmmss");
            string baseName = $"{originalBase}_restored_{stamp}";
            string candidate = Path.Combine(originalDir, baseName + ext);

            int suffix = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(originalDir, $"{baseName}_{suffix}{ext}");
                suffix++;
            }
        
            // Optionally mark restored file as read-only too (hard immutability)
            // var attr = File.GetAttributes(candidate);
            // File.SetAttributes(candidate, attr | FileAttributes.ReadOnly);

            File.Copy(snapshot.SnapshotPath, candidate, overwrite: false);
            return candidate;
        }


        private static string FormatRelativeTime(DateTime t)
        {
            var delta = DateTime.Now - t;

            if (delta.TotalSeconds < 60) return "just now";
            if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes} mins ago";
            if (delta.TotalHours < 24) return $"{(int)delta.TotalHours} hours ago";
            if (delta.TotalDays < 7) return $"{(int)delta.TotalDays} days ago";
            if (delta.TotalDays < 30) return $"{(int)(delta.TotalDays / 7)} weeks ago";
            return t.ToShortDateString();
        }


        private void EnsureSnapshotDirectory()
        {
            if (string.IsNullOrEmpty(snapshotDir))
                return;

            try
            {
                if (!Directory.Exists(snapshotDir))
                {
                    Directory.CreateDirectory(snapshotDir);
                    Log.Debug($"EnsureSnapshotDirectory, created snapshotDir: {snapshotDir}");
                }
            }
            catch (Exception ex)
            {
                // Best-effort: if this fails, snapshotting for this file is effectively disabled
                Log.Debug($"EnsureSnapshotDirectory, failed to create snapshotDir '{snapshotDir}': {ex}");
            }
        }


    }
}
