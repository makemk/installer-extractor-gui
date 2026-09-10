using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace InstallerExtractorGUI.Core
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error
    }

    public static class AppLogger
    {
        private static readonly object lockObj = new object();
        private static bool isEnabled = true;
        private static string currentLogPath = null;
        public static Action<string> OnLogAppended;

        public static bool IsEnabled
        {
            get { return isEnabled; }
            set { isEnabled = value; }
        }

        public static string CurrentLogFilePath
        {
            get { return currentLogPath; }
        }

        public static string GetDefaultLogDirectory()
        {
            // 优先选择程序所在目录下的 logs 文件夹
            try
            {
                string localLogs = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!Directory.Exists(localLogs)) Directory.CreateDirectory(localLogs);
                return localLogs;
            }
            catch
            {
                // 若无写权限，降级至 LocalAppData
                string appDataLogs = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"InstallerExtractorGUI\logs"
                );
                if (!Directory.Exists(appDataLogs)) Directory.CreateDirectory(appDataLogs);
                return appDataLogs;
            }
        }

        public static void Initialize(bool enabled, string customLogPath = null)
        {
            lock (lockObj)
            {
                isEnabled = enabled;
                if (!enabled)
                {
                    currentLogPath = null;
                    return;
                }

                if (!string.IsNullOrEmpty(customLogPath))
                {
                    currentLogPath = Path.GetFullPath(customLogPath);
                    string dir = Path.GetDirectoryName(currentLogPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                }
                else
                {
                    string dir = GetDefaultLogDirectory();
                    string fileName = string.Format("extractor_{0}_{1}.log",
                        DateTime.Now.ToString("yyyyMMdd_HHmmss"),
                        Process.GetCurrentProcess().Id);
                    currentLogPath = Path.Combine(dir, fileName);
                }

                // 写入日志头部环境信息，极大方便 AI 排错诊断
                WriteRawLine("================================================================================");
                WriteRawLine("InstallerExtractor Diagnostic & AI Debugging Log");
                WriteRawLine("Timestamp   : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                WriteRawLine("OS Version  : " + Environment.OSVersion.VersionString + (Environment.Is64BitOperatingSystem ? " (64-bit)" : " (32-bit)"));
                WriteRawLine("CLR Version : " + Environment.Version);
                WriteRawLine("Machine     : " + Environment.MachineName);
                WriteRawLine("User Domain : " + Environment.UserDomainName + "\\" + Environment.UserName);
                WriteRawLine("Process PID : " + Process.GetCurrentProcess().Id);
                WriteRawLine("Command Line: " + Environment.CommandLine);
                WriteRawLine("================================================================================\n");
            }
        }

        public static void Debug(string message)
        {
            Log(LogLevel.Debug, message);
        }

        public static void Info(string message)
        {
            Log(LogLevel.Info, message);
        }

        public static void Warn(string message)
        {
            Log(LogLevel.Warn, message);
        }

        public static void Error(string message, Exception ex = null)
        {
            if (ex != null)
            {
                message = string.Format("{0} -> Exception: {1}\nStackTrace:\n{2}", message, ex.Message, ex.StackTrace);
            }
            Log(LogLevel.Error, message);
        }

        public static void Log(LogLevel level, string message)
        {
            if (!isEnabled) return;

            string line = string.Format("[{0}] [{1,-5}] [Thread-{2:D2}] {3}",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                level.ToString().ToUpperInvariant(),
                System.Threading.Thread.CurrentThread.ManagedThreadId,
                message);

            lock (lockObj)
            {
                WriteRawLine(line);
            }

            if (OnLogAppended != null)
            {
                try { OnLogAppended(line); } catch { }
            }
        }

        private static void WriteRawLine(string text)
        {
            if (string.IsNullOrEmpty(currentLogPath)) return;
            try
            {
                using (var fs = new FileStream(currentLogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (var sw = new StreamWriter(fs, Encoding.UTF8))
                {
                    sw.WriteLine(text);
                }
            }
            catch
            {
                // 防止日志异常导致业务崩溃
            }
        }
    }
}
