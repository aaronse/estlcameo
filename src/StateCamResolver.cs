using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EstlCameo
{
    public class StateCamInfo
    {
        public string DirProjects { get; set; }
        public List<string> RecentFiles { get; } = new();
    }

    public static class StateCamResolver
    {
        private static StateCamInfo _cached;
        private static DateTime _lastLoad;
        private static string _lastFilePath;

        public static StateCamInfo Load()
        {
            // Reload at most every 5 seconds
            if (_cached != null && (DateTime.Now - _lastLoad).TotalSeconds < 5)
                return _cached;

            string baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Estlcam"
            );

            if (!Directory.Exists(baseDir))
                return _cached ?? new StateCamInfo();

            // Search all profiles for "State CAM.txt"
            var candidates = Directory.GetFiles(baseDir, "State CAM.txt", SearchOption.AllDirectories);
            if (candidates.Length == 0)
                return _cached ?? new StateCamInfo();

            // Prefer the most recently written
            string statePath = candidates
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .First();

            _lastFilePath = statePath;

            var info = new StateCamInfo();

            try
            {
                foreach (var line in File.ReadAllLines(statePath))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("Dir projects=", StringComparison.OrdinalIgnoreCase))
                    {
                        info.DirProjects = trimmed.Substring("Dir projects=".Length).Trim();
                    }
                    else if (trimmed.StartsWith("Recent files=", StringComparison.OrdinalIgnoreCase))
                    {
                        var value = trimmed.Substring("Recent files=".Length).Trim();
                        if (!string.IsNullOrEmpty(value))
                        {
                            info.RecentFiles.AddRange(
                                value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(s => s.Trim()));
                        }
                    }
                }
            }
            catch
            {
                // Best-effort: keep old cache if present
                return _cached ?? new StateCamInfo();
            }

            _cached = info;
            _lastLoad = DateTime.Now;
            return info;
        }

        /// <summary>
        /// Try to resolve a full project path given just the file name (from window title).
        /// </summary>
        public static string ResolveProjectPath(string fileNameOnly)
        {
            if (string.IsNullOrWhiteSpace(fileNameOnly))
                return null;

            var state = Load();

            // 1) MRU match by file name
            var candidates = state.RecentFiles
                .Where(p => string.Equals(
                    Path.GetFileName(p),
                    fileNameOnly,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count == 1)
                return candidates[0];

            if (candidates.Count > 1)
            {
                // Prefer one in DirProjects if possible
                if (!string.IsNullOrEmpty(state.DirProjects))
                {
                    var fromDir = candidates
                        .FirstOrDefault(p =>
                            Path.GetDirectoryName(p)
                                ?.TrimEnd(Path.DirectorySeparatorChar)
                                .Equals(state.DirProjects.TrimEnd(Path.DirectorySeparatorChar),
                                        StringComparison.OrdinalIgnoreCase) == true);
                    if (!string.IsNullOrEmpty(fromDir))
                        return fromDir;
                }

                // Otherwise prefer the most recently written physical file
                var best = candidates
                    .OrderByDescending(p =>
                    {
                        try { return File.GetLastWriteTimeUtc(p); }
                        catch { return DateTime.MinValue; }
                    })
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(best))
                    return best;
            }

            // 2) If we have DirProjects, try combining it
            if (!string.IsNullOrEmpty(state.DirProjects))
            {
                var combined = Path.Combine(state.DirProjects, fileNameOnly);
                if (File.Exists(combined))
                    return combined;
            }

            // 3) Give up, caller can prompt user
            return null;
        }
    }
}
