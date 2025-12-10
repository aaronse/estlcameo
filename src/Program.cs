namespace EstlCameo
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // For dev:
            Log.Verbose = true;
            Log.ConsoleOutput = false; // WinForms tray, usually false
            Log.TraceOutput = true;

            // Optional: log file in %LOCALAPPDATA%\EstlCameo\EstlCameo.log
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EstlCameo");
            Directory.CreateDirectory(logDir);
            Log.SetLogFile(Path.Combine(logDir, "EstlCameo.log"), append: true);

            Log.Info("EstlCameo starting up");

            Application.Run(new TrayForm());
        }
    }
}