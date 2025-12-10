using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace EstlCameo
{
    /// <summary>
    /// Simple static logger for EstlCameo.
    /// 
    /// Output format (by default):
    ///   [D] 12:34:56.78 message...
    ///
    /// Writes to:
    ///   - Debug / Trace listeners (always, unless TraceOutput = false)
    ///   - Optional log file via SetLogFile(...)
    ///   - Optional console (ConsoleOutput = true) for debugging
    /// </summary>
    internal static class Log
    {
        private static readonly object _lock = new();

        private static string _logFile = null;
        private static bool _logTime = true;
        private static bool _logMessageType = true;
        private static bool _logRelativeTime = false;
        private static readonly DateTime _startTime = DateTime.Now;
        private static bool _verbose = true;
        private static bool _consoleOutput = false;
        private static bool _traceOutput = true;

        /// <summary>
        /// Include timestamp in each log line.
        /// </summary>
        public static bool LogTime
        {
            get => _logTime;
            set => _logTime = value;
        }

        /// <summary>
        /// Include the [E]/[W]/[I]/[D] prefix.
        /// </summary>
        public static bool LogMessageType
        {
            get => _logMessageType;
            set => _logMessageType = value;
        }

        /// <summary>
        /// If true, time is relative to process start; otherwise wall-clock time.
        /// </summary>
        public static bool LogRelativeTime
        {
            get => _logRelativeTime;
            set => _logRelativeTime = value;
        }

        /// <summary>
        /// If false, Debug(...) calls become no-ops.
        /// </summary>
        public static bool Verbose
        {
            get => _verbose;
            set => _verbose = value;
        }

        /// <summary>
        /// If true, also write to Console.Out (useful when running from console).
        /// </summary>
        public static bool ConsoleOutput
        {
            get => _consoleOutput;
            set => _consoleOutput = value;
        }

        /// <summary>
        /// If false, nothing is written to System.Diagnostics.Trace listeners.
        /// </summary>
        public static bool TraceOutput
        {
            get => _traceOutput;
            set => _traceOutput = value;
        }

        /// <summary>
        /// Configure a log file. Passing null or empty disables file logging.
        /// </summary>
        public static void SetLogFile(string logFile, bool append = false)
        {
            lock (_lock)
            {
                _logFile = logFile;

                // Clear existing file listeners; keep any others user may have set up.
                for (int i = Trace.Listeners.Count - 1; i >= 0; i--)
                {
                    if (Trace.Listeners[i] is TextWriterTraceListener)
                        Trace.Listeners.RemoveAt(i);
                }

                if (!string.IsNullOrEmpty(_logFile))
                {
                    var writer = new StreamWriter(_logFile, append, Encoding.UTF8)
                    {
                        AutoFlush = true
                    };
                    Trace.Listeners.Add(new TextWriterTraceListener(writer));
                }
            }
        }

        /// <summary>
        /// Flush all Trace listeners (e.g. before exit).
        /// </summary>
        public static void Flush()
        {
            lock (_lock)
            {
                foreach (TraceListener tl in Trace.Listeners)
                {
                    try { tl.Flush(); } catch { /* ignore */ }
                }
            }
        }

        // -------- Public logging API --------

        public static void Error(string msg) => Write("[E]", msg, ConsoleColor.Red);
        public static void Error(string msg, params object[] args) => Error(string.Format(msg, args));

        public static void Warn(string msg) => Write("[W]", msg, ConsoleColor.Yellow);
        public static void Warn(string msg, params object[] args) => Warn(string.Format(msg, args));

        public static void Info(string msg) => Write("[I]", msg, ConsoleColor.White);
        public static void Info(string msg, params object[] args) => Info(string.Format(msg, args));

        public static void Debug(string msg)
        {
            if (!_verbose) return;
            Write("[D]", msg, ConsoleColor.DarkGray);
        }

        public static void Debug(string msg, params object[] args)
        {
            if (!_verbose) return;
            Debug(string.Format(msg, args));
        }

        // -------- Core writer --------

        private static void Write(string msgType, string msg, ConsoleColor color)
        {
            var line = BuildLine(msgType, msg);

            lock (_lock)
            {
                // Debug / Trace output
                if (_traceOutput)
                {
                    Trace.WriteLine(line);
                    Debugger.Log(0, null, line + Environment.NewLine);
                }

                if (_consoleOutput)
                {
                    var oldColor = Console.ForegroundColor;
                    try
                    {
                        Console.ForegroundColor = color;
                        Console.WriteLine(line);
                    }
                    catch
                    {
                        // Console may not be available in WinForms, ignore.
                    }
                    finally
                    {
                        Console.ForegroundColor = oldColor;
                    }
                }
            }
        }

        private static string BuildLine(string msgType, string msg)
        {
            var sb = new StringBuilder();

            if (_logMessageType)
            {
                sb.Append(msgType);
                sb.Append(' ');
            }

            if (_logTime)
            {
                if (!_logRelativeTime)
                {
                    sb.Append(DateTime.Now.ToString("HH:mm:ss.ff"));
                }
                else
                {
                    TimeSpan ts = DateTime.Now - _startTime;
                    sb.AppendFormat(
                        "{0:D2}:{1:D2}:{2:D2}.{3:D3}",
                        ts.Hours,
                        ts.Minutes,
                        ts.Seconds,
                        ts.Milliseconds);
                }
                sb.Append(' ');
            }

            sb.Append(msg);
            return sb.ToString();
        }
    }
}
