using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using InstallerExtractorGUI.Core;

namespace InstallerExtractorGUI
{
    public class CliProgram
    {
        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            string source = null;
            string target = null;
            string mode = "extract";
            string customArgs = null;
            string customLogFile = null;
            bool enableLog = true;
            bool detectOnly = false;
            bool jsonOutput = false;
            bool quiet = false;
            bool allowRegistry = false;
            bool installDrivers = false;
            bool scanDriversOnly = false;
            bool showHelp = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.Equals("-h", StringComparison.OrdinalIgnoreCase) || arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("/?"))
                {
                    showHelp = true;
                }
                else if ((arg.Equals("-i", StringComparison.OrdinalIgnoreCase) || arg.Equals("--input", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    source = args[++i].Trim('"', ' ');
                }
                else if ((arg.Equals("-o", StringComparison.OrdinalIgnoreCase) || arg.Equals("--output", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    target = args[++i].Trim('"', ' ');
                }
                else if ((arg.Equals("-m", StringComparison.OrdinalIgnoreCase) || arg.Equals("--mode", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    mode = args[++i].Trim().ToLowerInvariant();
                }
                else if (arg.Equals("-d", StringComparison.OrdinalIgnoreCase) || arg.Equals("--detect", StringComparison.OrdinalIgnoreCase))
                {
                    detectOnly = true;
                }
                else if (arg.Equals("-j", StringComparison.OrdinalIgnoreCase) || arg.Equals("--json", StringComparison.OrdinalIgnoreCase))
                {
                    jsonOutput = true;
                }
                else if (arg.Equals("-q", StringComparison.OrdinalIgnoreCase) || arg.Equals("--quiet", StringComparison.OrdinalIgnoreCase))
                {
                    quiet = true;
                }
                else if (arg.Equals("--no-log", StringComparison.OrdinalIgnoreCase) || arg.Equals("--disable-log", StringComparison.OrdinalIgnoreCase))
                {
                    enableLog = false;
                }
                else if ((arg.Equals("--log-file", StringComparison.OrdinalIgnoreCase) || arg.Equals("-l", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    customLogFile = args[++i].Trim('"', ' ');
                    enableLog = true;
                }
                else if (arg.Equals("--install-drivers", StringComparison.OrdinalIgnoreCase))
                {
                    installDrivers = true;
                }
                else if (arg.Equals("--scan-drivers", StringComparison.OrdinalIgnoreCase))
                {
                    scanDriversOnly = true;
                }
                else if (arg.Equals("--allow-registry", StringComparison.OrdinalIgnoreCase))
                {
                    allowRegistry = true;
                }
                else if (arg.Equals("--silent-args", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    customArgs = args[++i];
                }
                else if (source == null && !arg.StartsWith("-"))
                {
                    source = arg.Trim('"', ' ');
                }
            }

            AppLogger.Initialize(enableLog, customLogFile);
            if (enableLog)
            {
                AppLogger.Info("CLI launched with command: " + string.Join(" ", args));
            }

            if (showHelp || (string.IsNullOrEmpty(source) && !detectOnly && !scanDriversOnly))
            {
                PrintHelp(jsonOutput);
                return showHelp ? 0 : 1;
            }

            // 单独的驱动扫描模式
            if (scanDriversOnly)
            {
                string scanDir = !string.IsNullOrEmpty(target) ? target : source;
                var scan = DriverInstaller.ScanDrivers(scanDir);
                if (jsonOutput)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("{");
                    sb.Append("\"success\":true,");
                    sb.Append("\"action\":\"scan_drivers\",");
                    sb.AppendFormat("\"directory\":\"{0}\",", EscapeJson(Path.GetFullPath(scanDir)));
                    sb.AppendFormat("\"hasDrivers\":{0},", scan.HasDrivers ? "true" : "false");
                    sb.Append("\"infFiles\":[");
                    for (int k = 0; k < scan.InfFiles.Count; k++)
                    {
                        sb.Append("\"" + EscapeJson(scan.InfFiles[k]) + "\"");
                        if (k < scan.InfFiles.Count - 1) sb.Append(",");
                    }
                    sb.Append("],");
                    sb.Append("\"installerExes\":[");
                    for (int k = 0; k < scan.InstallerExes.Count; k++)
                    {
                        sb.Append("\"" + EscapeJson(scan.InstallerExes[k]) + "\"");
                        if (k < scan.InstallerExes.Count - 1) sb.Append(",");
                    }
                    sb.Append("]");
                    sb.Append("}");
                    Console.WriteLine(sb.ToString());
                }
                else
                {
                    Console.WriteLine("Driver Scan Results in: " + scanDir);
                    Console.WriteLine("  Has Drivers: " + scan.HasDrivers);
                    Console.WriteLine("  INF Files  : " + scan.InfFiles.Count);
                    foreach (var inf in scan.InfFiles) Console.WriteLine("    - " + inf);
                    Console.WriteLine("  Installers : " + scan.InstallerExes.Count);
                    foreach (var exe in scan.InstallerExes) Console.WriteLine("    - " + exe);
                }
                return 0;
            }

            if (!File.Exists(source))
            {
                string errMsg = "Source file not found: " + source;
                AppLogger.Error(errMsg);
                if (jsonOutput)
                {
                    Console.WriteLine(string.Format("{{\"success\":false,\"error\":\"{0}\",\"logFile\":{1},\"exitCode\":1}}",
                        EscapeJson(errMsg),
                        enableLog ? "\"" + EscapeJson(AppLogger.CurrentLogFilePath) + "\"" : "null"));
                }
                else
                {
                    Console.Error.WriteLine("[Error] " + errMsg);
                }
                return 1;
            }

            if (detectOnly)
            {
                DetectionResult det = InstallerDetector.Detect(source);
                AppLogger.Info("Detect result for " + source + ": " + det.DisplayName);
                if (jsonOutput)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("{");
                    sb.Append("\"success\":true,");
                    sb.Append("\"action\":\"detect\",");
                    sb.AppendFormat("\"source\":\"{0}\",", EscapeJson(Path.GetFullPath(source)));
                    sb.AppendFormat("\"type\":\"{0}\",", det.Type.ToString());
                    sb.AppendFormat("\"displayName\":\"{0}\",", EscapeJson(det.DisplayName));
                    sb.AppendFormat("\"description\":\"{0}\",", EscapeJson(det.Description));
                    sb.AppendFormat("\"canDirectExtract\":{0},", det.CanDirectExtract ? "true" : "false");
                    sb.AppendFormat("\"recommendedSilentArgs\":\"{0}\",", EscapeJson(det.RecommendedSilentArgs));
                    sb.AppendFormat("\"logFile\":{0}", enableLog ? "\"" + EscapeJson(AppLogger.CurrentLogFilePath) + "\"" : "null");
                    sb.Append("}");
                    Console.WriteLine(sb.ToString());
                }
                else
                {
                    Console.WriteLine("Installer Analysis:");
                    Console.WriteLine("  File: " + Path.GetFullPath(source));
                    Console.WriteLine("  Type: " + det.Type);
                    Console.WriteLine("  Name: " + det.DisplayName);
                    Console.WriteLine("  Desc: " + det.Description);
                    Console.WriteLine("  Can Direct Extract: " + det.CanDirectExtract);
                    Console.WriteLine("  Recommended Silent Args: " + det.RecommendedSilentArgs);
                    if (enableLog)
                    {
                        Console.WriteLine("  Log File: " + AppLogger.CurrentLogFilePath);
                    }
                }
                return 0;
            }

            if (string.IsNullOrEmpty(target))
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(source));
                string name = Path.GetFileNameWithoutExtension(source);
                target = Path.Combine(dir, name + "_extracted");
            }
            target = Path.GetFullPath(target);

            List<string> logs = new List<string>();
            Action<string> logger = msg =>
            {
                logs.Add(msg);
                if (!quiet && !jsonOutput)
                {
                    Console.WriteLine(msg);
                }
            };

            var options = new ExtractionOptions
            {
                SourceFile = Path.GetFullPath(source),
                TargetDirectory = target,
                IsExtractOnly = (mode == "extract"),
                ForceRunAsInvoker = true,
                AllowRegistryAndShortcuts = allowRegistry,
                PreferNativeTools = true,
                CustomSilentArgs = customArgs
            };

            bool success = false;
            try
            {
                success = ExtractorEngine.Run(options, logger, pct => { });
            }
            catch (Exception ex)
            {
                AppLogger.Error("Unhandled exception in CLI", ex);
                logger("[Exception] " + ex.Message);
                success = false;
            }

            // 驱动扫描与可选安装
            DriverScanResult driverScan = DriverInstaller.ScanDrivers(target);
            bool driverInstallAttempted = false;
            bool driverInstallSuccess = false;

            if (installDrivers && driverScan.HasDrivers)
            {
                logger("[可选任务] 启动驱动安装流程...");
                driverInstallAttempted = true;
                driverInstallSuccess = DriverInstaller.InstallDrivers(target, logger);
            }

            int fileCount = 0;
            long totalBytes = 0;
            List<string> allExes = new List<string>();
            string mainExe = null;
            bool nestedPayloadsDetected = logs.Exists(delegate(string l) { return l.Contains("[双层嵌套探测]"); });

            if (Directory.Exists(target))
            {
                try
                {
                    var files = Directory.GetFiles(target, "*.*", SearchOption.AllDirectories);
                    fileCount = files.Length;
                    foreach (var f in files)
                    {
                        totalBytes += new FileInfo(f).Length;
                        if (f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            allExes.Add(f);
                        }
                    }
                    mainExe = DetectMainExecutable(target, source, allExes);
                }
                catch { }
            }

            if (jsonOutput)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("{");
                sb.AppendFormat("\"success\":{0},", success ? "true" : "false");
                sb.AppendFormat("\"mode\":\"{0}\",", EscapeJson(mode));
                sb.AppendFormat("\"source\":\"{0}\",", EscapeJson(Path.GetFullPath(source)));
                sb.AppendFormat("\"target\":\"{0}\",", EscapeJson(target));
                sb.AppendFormat("\"fileCount\":{0},", fileCount);
                sb.AppendFormat("\"totalBytes\":{0},", totalBytes);

                sb.AppendFormat("\"mainExecutable\":{0},", mainExe != null ? "\"" + EscapeJson(mainExe) + "\"" : "null");
                sb.Append("\"executables\":[");
                for (int e = 0; e < allExes.Count; e++)
                {
                    sb.Append("\"" + EscapeJson(allExes[e]) + "\"");
                    if (e < allExes.Count - 1) sb.Append(",");
                }
                sb.Append("],");
                sb.AppendFormat("\"nestedPayloadsDetected\":{0},", nestedPayloadsDetected ? "true" : "false");

                // 驱动信息
                sb.Append("\"drivers\":{");
                sb.AppendFormat("\"detected\":{0},", driverScan.HasDrivers ? "true" : "false");
                sb.AppendFormat("\"count\":{0},", driverScan.InfFiles.Count);
                sb.AppendFormat("\"installAttempted\":{0},", driverInstallAttempted ? "true" : "false");
                sb.AppendFormat("\"installSuccess\":{0}", driverInstallSuccess ? "true" : "false");
                sb.Append("},");

                sb.AppendFormat("\"logFile\":{0},", enableLog ? "\"" + EscapeJson(AppLogger.CurrentLogFilePath) + "\"" : "null");
                sb.AppendFormat("\"exitCode\":{0},", success ? 0 : 2);
                sb.Append("\"logs\":[");
                for (int j = 0; j < logs.Count; j++)
                {
                    sb.Append("\"" + EscapeJson(logs[j]) + "\"");
                    if (j < logs.Count - 1) sb.Append(",");
                }
                sb.Append("]}");
                Console.WriteLine(sb.ToString());
            }
            else
            {
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine(success ? "[Result] Success!" : "[Result] Execution finished with warnings or failure.");
                Console.WriteLine("Target Directory : " + target);
                Console.WriteLine("Extracted Files  : " + fileCount);
                Console.WriteLine("Total Size       : " + (totalBytes / 1024.0 / 1024.0).ToString("F2") + " MB");
                if (!string.IsNullOrEmpty(mainExe))
                {
                    Console.WriteLine("Main Executable  : " + mainExe);
                }
                if (nestedPayloadsDetected)
                {
                    Console.WriteLine("Nested Unpacked  : Yes (Electron-Builder 2-stage payload fully extracted)");
                }
                Console.WriteLine("Drivers Detected : " + (driverScan.HasDrivers ? (driverScan.InfFiles.Count + " inf files") : "None"));
                if (driverInstallAttempted)
                {
                    Console.WriteLine("Drivers Install  : " + (driverInstallSuccess ? "Success" : "Failed / Cancelled"));
                }
                if (enableLog)
                {
                    Console.WriteLine("Diagnostic Log   : " + AppLogger.CurrentLogFilePath);
                }
                Console.WriteLine("--------------------------------------------------");
            }

            return success ? 0 : 2;
        }

        private static void PrintHelp(bool json)
        {
            if (json)
            {
                Console.WriteLine("{\"usage\":\"InstallerExtractorCLI [options] -i <installer_path>\",\"options\":[\"-i, --input <path>\",\"-o, --output <dir>\",\"-m, --mode <extract|install>\",\"-d, --detect\",\"--install-drivers\",\"--scan-drivers\",\"-j, --json\",\"-q, --quiet\",\"--no-log\",\"--log-file <path>\",\"--allow-registry\",\"--silent-args <args>\"]}");
                return;
            }

            Console.WriteLine("================================================================================");
            Console.WriteLine("InstallerExtractorCLI - AI & Automation Tool for EXE/MSI Extraction");
            Console.WriteLine("================================================================================");
            Console.WriteLine("Usage:");
            Console.WriteLine("  InstallerExtractorCLI.exe -i <installer.exe|msi> [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -i, --input <file>       Path to installer (.exe / .msi) [Required]");
            Console.WriteLine("  -o, --output <dir>       Target output directory (default: <file_dir>\\<name>_extracted)");
            Console.WriteLine("  -m, --mode <mode>        Execution mode: 'extract' (default) or 'install'");
            Console.WriteLine("                             extract : Pure unpack (no registry, no admin)");
            Console.WriteLine("                             install : Silent de-elevated deploy (RunAsInvoker)");
            Console.WriteLine("  -d, --detect             Analyze and identify installer package type without extracting");
            Console.WriteLine("      --install-drivers    Automatically scan & install device drivers (.inf/dpinst) if found");
            Console.WriteLine("      --scan-drivers       Scan directory for device drivers without extracting or installing");
            Console.WriteLine("  -j, --json               Output structured JSON (Recommended for AI agents & scripts)");
            Console.WriteLine("  -q, --quiet              Suppress progress and verbose logs");
            Console.WriteLine("      --no-log             Disable diagnostic file logging");
            Console.WriteLine("  -l, --log-file <path>    Specify custom diagnostic log file path");
            Console.WriteLine("      --allow-registry     Allow writing registry/shortcuts (only in install mode)");
            Console.WriteLine("      --silent-args <args> Override custom silent parameters");
            Console.WriteLine("  -h, --help               Show this help message");
            Console.WriteLine();
            Console.WriteLine("AI Call Examples:");
            Console.WriteLine("  1. Extract package and automatically install detected hardware drivers:");
            Console.WriteLine("     InstallerExtractorCLI.exe -i \"setup.exe\" -o \"C:\\app\" --install-drivers --json");
            Console.WriteLine();
            Console.WriteLine("  2. Scan an existing directory for device driver packages:");
            Console.WriteLine("     InstallerExtractorCLI.exe --scan-drivers -o \"C:\\app\" --json");
            Console.WriteLine("================================================================================");
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\"': sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32)
                        {
                            sb.AppendFormat("\\u{0:x4}", (int)c);
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        private static string DetectMainExecutable(string targetDir, string sourceFile, List<string> allExes)
        {
            if (allExes == null || allExes.Count == 0) return null;

            var candidates = new List<string>();
            foreach (var exe in allExes)
            {
                string name = Path.GetFileName(exe).ToLowerInvariant();
                if (name.StartsWith("uninstall") || name.StartsWith("unins") ||
                    name == "elevate.exe" || name == "7z.exe" || name == "innounp.exe" ||
                    name == "crashpad_handler.exe" || name == "notification_helper.exe")
                {
                    continue;
                }
                candidates.Add(exe);
            }

            if (candidates.Count == 0) return allExes[0];

            // 1. 优先检查根目录下的候选可执行文件
            var rootCandidates = new List<string>();
            foreach (var c in candidates)
            {
                if (string.Equals(Path.GetDirectoryName(c), targetDir, StringComparison.OrdinalIgnoreCase))
                {
                    rootCandidates.Add(c);
                }
            }

            if (rootCandidates.Count == 1) return rootCandidates[0];

            // 2. 针对 Electron 等跨平台应用，扫描 package.json 中的名称或 product 名称
            string pkgJson = Path.Combine(targetDir, @"resources\app\package.json");
            if (!File.Exists(pkgJson))
            {
                pkgJson = Path.Combine(targetDir, "package.json");
            }
            if (File.Exists(pkgJson))
            {
                try
                {
                    string content = File.ReadAllText(pkgJson);
                    foreach (var c in (rootCandidates.Count > 0 ? rootCandidates : candidates))
                    {
                        string baseName = Path.GetFileNameWithoutExtension(c);
                        if (content.IndexOf(baseName, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return c;
                        }
                    }
                }
                catch { }
            }

            // 3. 匹配原始安装包的基础名称（去掉 setup/win/x64 等后缀）
            string srcBase = Path.GetFileNameWithoutExtension(sourceFile).ToLowerInvariant()
                .Replace("-setup", "").Replace("_setup", "").Replace("setup", "")
                .Replace("-windows", "").Replace("-win", "").Replace("-x64", "").Replace("-x86", "");

            foreach (var c in (rootCandidates.Count > 0 ? rootCandidates : candidates))
            {
                string cBase = Path.GetFileNameWithoutExtension(c).ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");
                string sBase = srcBase.Replace(" ", "").Replace("-", "").Replace("_", "");
                if (cBase.Contains(sBase) || sBase.Contains(cBase))
                {
                    return c;
                }
            }

            return rootCandidates.Count > 0 ? rootCandidates[0] : candidates[0];
        }
    }
}
