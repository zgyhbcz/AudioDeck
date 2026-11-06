using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace AudioDeck
{
    internal static class Program
    {
        private const string MutexName = @"Local\AudioDeck.SingleInstance.7A1C";
        private const string ShowEventName = @"Local\AudioDeck.Show.7A1C";

        [STAThread]
        private static void Main(string[] args)
        {
            string diagnosePath = GetDiagnosePath(args);
            if (diagnosePath != null)
            {
                Diagnostics.Write(diagnosePath);
                return;
            }

            bool silentStartup = HasArgument(args, "--startup");

            bool createdNew;
            Mutex mutex = new Mutex(true, MutexName, out createdNew);

            bool eventCreated;
            EventWaitHandle showEvent = new EventWaitHandle(false, EventResetMode.AutoReset,
                ShowEventName, out eventCreated);

            if (!createdNew)
            {
                if (!silentStartup)
                {
                    try
                    {
                        showEvent.Set();
                    }
                    catch
                    {
                    }
                }

                showEvent.Dispose();
                mutex.Dispose();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += ThreadException;
            AppDomain.CurrentDomain.UnhandledException += UnhandledException;

            try
            {
                using (TrayContext context = new TrayContext(showEvent))
                {
                    Application.Run(context);
                }
            }
            catch (Exception ex)
            {
                LogError("Startup", ex);
                MessageBox.Show("声控台启动失败。\r\n\r\n" + ex.Message, "声控台",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                try
                {
                    showEvent.Dispose();
                }
                catch
                {
                }

                try
                {
                    mutex.ReleaseMutex();
                }
                catch
                {
                }

                mutex.Dispose();
            }
        }

        private static string GetDiagnosePath(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--diagnose", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        return args[i + 1];
                    }

                    return Path.Combine(Path.GetTempPath(), "AudioDeck.diagnose.txt");
                }
            }

            return null;
        }

        private static bool HasArgument(string[] args, string argument)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], argument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            LogError("UI thread", e.Exception);
            MessageBox.Show("声控台遇到一个错误，但仍会继续运行。\r\n\r\n" + e.Exception.Message,
                "声控台", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            LogError("Unhandled", ex);
        }

        private static void LogError(string source, Exception ex)
        {
            try
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AudioDeck");
                Directory.CreateDirectory(directory);
                StringBuilder text = new StringBuilder();
                text.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + source + "]");
                text.AppendLine(ex == null ? "(no exception object)" : ex.ToString());
                text.AppendLine();
                File.AppendAllText(Path.Combine(directory, "error.log"), text.ToString(),
                    new UTF8Encoding(true));
            }
            catch
            {
            }
        }
    }
}
