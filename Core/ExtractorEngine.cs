using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace InstallerExtractorGUI.Core
{
    public class ExtractionOptions
    {
        public string SourceFile { get; set; }
        public string TargetDirectory { get; set; }
        public bool IsExtractOnly { get; set; }
        public bool ForceRunAsInvoker { get; set; }
        public bool AllowRegistryAndShortcuts { get; set; }
        public bool PreferNativeTools { get; set; }
        public string CustomSilentArgs { get; set; }
    }

    public static class ExtractorEngine
    {
        private static string cached7zPath = null;
        private static string cachedInnounpPath = null;
        private static string cachedFormatsDir = null;

        public static string GetFormatsDirectory()
        {
            if (!string.IsNullOrEmpty(cachedFormatsDir) && Directory.Exists(cachedFormatsDir))
            {
                return cachedFormatsDir;
            }

            string localFormats = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Formats");
            if (Directory.Exists(localFormats))
            {
                cachedFormatsDir = localFormats;
                return localFormats;
            }

            string appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"InstallerExtractorGUI\7zip\Formats"
            );
            try
            {
                if (!Directory.Exists(appDataDir))
                {
                    Directory.CreateDirectory(appDataDir);
                }
            }
            catch { }

            cachedFormatsDir = appDataDir;
            return appDataDir;
        }

        public static string EnsureInnounp(Action<string> log)
        {
            if (!string.IsNullOrEmpty(cachedInnounpPath) && File.Exists(cachedInnounpPath))
            {
                return cachedInnounpPath;
            }

            string[] localProbes = new string[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"tools\innounp.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "innounp.exe")
            };
            foreach (var p in localProbes)
            {
                if (File.Exists(p))
                {
                    cachedInnounpPath = p;
                    AppLogger.Info("Found local innounp.exe at: " + p);
                    return p;
                }
            }

            try
            {
                string targetDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"InstallerExtractorGUI\tools"
                );
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                string dest = Path.Combine(targetDir, "innounp.exe");
                ExtractResourceToFile("innounp_exe", dest);
                if (File.Exists(dest))
                {
                    cachedInnounpPath = dest;
                    AppLogger.Info("Extracted innounp.exe to: " + dest);
                    return dest;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to extract innounp.exe", ex);
            }

            return null;
        }

        public static string EnsureSevenZipEngine(Action<string> log)
        {
            if (!string.IsNullOrEmpty(cached7zPath) && File.Exists(cached7zPath))
            {
                return cached7zPath;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string local7zExe = Path.Combine(baseDir, "7z.exe");
            string local7zDll = Path.Combine(baseDir, "7z.dll");

            if (File.Exists(local7zExe) && File.Exists(local7zDll))
            {
                cached7zPath = local7zExe;
                cachedFormatsDir = Path.Combine(baseDir, "Formats");
                AppLogger.Info("Found local 7z engine at: " + local7zExe);
                LogPluginStatus(cachedFormatsDir, log);
                return local7zExe;
            }

            try
            {
                string targetDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"InstallerExtractorGUI\7zip"
                );

                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                string exeDest = Path.Combine(targetDir, "7z.exe");
                string dllDest = Path.Combine(targetDir, "7z.dll");
                string formatsDir = Path.Combine(targetDir, "Formats");
                string codecsDir = Path.Combine(targetDir, "Codecs");

                if (!Directory.Exists(formatsDir)) Directory.CreateDirectory(formatsDir);
                if (!Directory.Exists(codecsDir)) Directory.CreateDirectory(codecsDir);

                ExtractResourceToFile("SevenZip_exe", exeDest);
                ExtractResourceToFile("SevenZip_dll", dllDest);

                ExtractResourceToFile("plugin_asar", Path.Combine(formatsDir, "Asar.64.dll"));
                ExtractResourceToFile("plugin_py7z", Path.Combine(formatsDir, "Py7z.64.dll"));
                ExtractResourceToFile("plugin_iso7z", Path.Combine(formatsDir, "Iso7z.64.dll"));
                ExtractResourceToFile("plugin_grit7z", Path.Combine(formatsDir, "Grit7z.64.dll"));

                ExtractResourceToFile("codec_modern7z", Path.Combine(codecsDir, "Modern7z.64.dll"));
                ExtractResourceToFile("codec_zstd", Path.Combine(codecsDir, "libzstd.64.dll"));
                ExtractResourceToFile("codec_lz4", Path.Combine(codecsDir, "liblz4.64.dll"));
                ExtractResourceToFile("codec_fastlzma2", Path.Combine(codecsDir, "fast-lzma2.64.dll"));

                if (File.Exists(exeDest) && File.Exists(dllDest))
                {
                    cached7zPath = exeDest;
                    cachedFormatsDir = formatsDir;
                    AppLogger.Info("Extracted embedded 7z engine to: " + exeDest);
                    log("[引擎初始化] 7-Zip CLI 核心引擎与扩展插件已准备就绪。");
                    LogPluginStatus(formatsDir, log);
                    return exeDest;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to extract 7z engine", ex);
                log("[初始化警告] 释放内置引擎出现提示: " + ex.Message);
            }

            return null;
        }

        private static void LogPluginStatus(string formatsDir, Action<string> log)
        {
            if (Directory.Exists(formatsDir))
            {
                var plugins = Directory.GetFiles(formatsDir, "*.dll");
                if (plugins.Length > 0)
                {
                    var names = new List<string>();
                    foreach (var p in plugins)
                    {
                        names.Add(Path.GetFileName(p));
                    }
                    string msg = "[插件已挂载] " + string.Join(", ", names.ToArray()) + " (支持 Electron Asar、PyInstaller、ISO镜像、PAK资源等)";
                    AppLogger.Info("Mounted plugins in Formats: " + string.Join(", ", names.ToArray()));
                    log(msg);
                }
            }
        }

        private static void ExtractResourceToFile(string resourceName, string targetPath)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return;

                    if (File.Exists(targetPath) && new FileInfo(targetPath).Length == stream.Length)
                    {
                        return;
                    }

                    using (var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        byte[] buf = new byte[64 * 1024];
                        int read;
                        while ((read = stream.Read(buf, 0, buf.Length)) > 0)
                        {
                            fs.Write(buf, 0, read);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("ExtractResourceToFile failed: " + resourceName, ex);
            }
        }

        public static bool Run(ExtractionOptions options, Action<string> log, Action<int> progress)
        {
            AppLogger.Info("Starting Extraction Task with parameters:");
            AppLogger.Info("  Source File       : " + options.SourceFile);
            AppLogger.Info("  Target Directory  : " + options.TargetDirectory);
            AppLogger.Info("  IsExtractOnly     : " + options.IsExtractOnly);
            AppLogger.Info("  ForceRunAsInvoker : " + options.ForceRunAsInvoker);
            AppLogger.Info("  AllowRegistry     : " + options.AllowRegistryAndShortcuts);
            AppLogger.Info("  CustomSilentArgs  : " + options.CustomSilentArgs);

            if (string.IsNullOrEmpty(options.SourceFile) || !File.Exists(options.SourceFile))
            {
                string errMsg = "[错误] 源安装包文件不存在: " + options.SourceFile;
                AppLogger.Error(errMsg);
                log(errMsg);
                return false;
            }

            if (string.IsNullOrEmpty(options.TargetDirectory))
            {
                string errMsg = "[错误] 目标解压目录未指定！";
                AppLogger.Error(errMsg);
                log(errMsg);
                return false;
            }

            try
            {
                if (!Directory.Exists(options.TargetDirectory))
                {
                    Directory.CreateDirectory(options.TargetDirectory);
                    AppLogger.Info("Created target directory: " + options.TargetDirectory);
                    log("[信息] 创建目标输出目录: " + options.TargetDirectory);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Cannot create target directory", ex);
                log("[错误] 无法创建目标输出目录: " + ex.Message);
                return false;
            }

            DetectionResult detection = InstallerDetector.Detect(options.SourceFile);
            AppLogger.Info("Detection result: Type=" + detection.Type + ", Name=" + detection.DisplayName + ", CanDirectExtract=" + detection.CanDirectExtract);
            log("[识别分析] 文件类型: " + detection.DisplayName);
            log("[识别说明] " + detection.Description);

            bool result = false;
            if (options.IsExtractOnly)
            {
                result = ExecuteExtractMode(options, detection, log, progress);
            }
            else
            {
                result = ExecuteInstallMode(options, detection, log, progress);
            }

            AppLogger.Info("Task finished with result: " + (result ? "SUCCESS" : "FAILED"));
            return result;
        }

        private static bool ExecuteExtractMode(ExtractionOptions options, DetectionResult detection, Action<string> log, Action<int> progress)
        {
            log("================ [开始解包提取 (主力军: 7-Zip + 专用模块 + 插件)] ================");
            progress(10);

            // 特殊专精 1: Inno Setup 专用解包模块 (innounp)
            if (detection.Type == InstallerType.InnoSetup)
            {
                string innounp = EnsureInnounp(log);
                if (!string.IsNullOrEmpty(innounp))
                {
                    log("[专用解包] 检测到 Inno Setup 安装包，正在调用 Inno 官方专用解包模块 (innounp)...");
                    string args = string.Format("-x -d\"{0}\" -y \"{1}\"", options.TargetDirectory, options.SourceFile);
                    int exitCode = RunProcess(innounp, args, null, true, log);
                    progress(80);

                    if (exitCode == 0 && DirectoryHasFiles(options.TargetDirectory))
                    {
                        progress(100);
                        log("[成功] Inno Setup 专用解包器已成功提取所有文件！");
                        log("[位置] 文件已存放至: " + options.TargetDirectory);
                        AppLogger.Info("Inno Setup extraction succeeded via innounp.");
                        return true;
                    }
                    else
                    {
                        AppLogger.Warn("Innounp returned exitCode: " + exitCode + ", falling back to 7-Zip...");
                        log("[提示] innounp 提取未完全结束，继续尝试 7-Zip 主力军引擎...");
                    }
                }
            }

            // 特殊专精 2: Advanced Installer (/extract 释放 MSI/CAB + 行政解包)
            if (detection.Type == InstallerType.AdvancedInstaller)
            {
                log("[专用解包] 检测到 Advanced Installer 封装包，正在调用 /extract 释放内部安装包组件...");
                string tempAdvDir = Path.Combine(Path.GetTempPath(), "AdvInst_" + Guid.NewGuid().ToString("N"));
                try
                {
                    Directory.CreateDirectory(tempAdvDir);
                    string advArgs = string.Format("/extract \"{0}\"", tempAdvDir);
                    RunProcess(options.SourceFile, advArgs, "RunAsInvoker", true, log);

                    string[] msiFiles = Directory.GetFiles(tempAdvDir, "*.msi", SearchOption.AllDirectories);
                    if (msiFiles.Length > 0)
                    {
                        string msiPath = msiFiles[0];
                        log("[原生解压] 成功释放内部 MSI (" + Path.GetFileName(msiPath) + ")，正在执行行政解包提取所有程序文件...");
                        string msiArgs = string.Format("/a \"{0}\" /qn TARGETDIR=\"{1}\"", msiPath, options.TargetDirectory);
                        int msiExit = RunProcess("msiexec.exe", msiArgs, null, false, log);
                        if (msiExit == 0 && DirectoryHasFiles(options.TargetDirectory))
                        {
                            progress(100);
                            log("[成功] Advanced Installer 内部所有程序文件已全部提取完成！");
                            log("[位置] 文件已存放至: " + options.TargetDirectory);
                            AppLogger.Info("Advanced Installer extraction succeeded via /extract + msiexec /a.");
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error("Advanced Installer extraction failed", ex);
                }
                finally
                {
                    try { if (Directory.Exists(tempAdvDir)) Directory.Delete(tempAdvDir, true); } catch { }
                }
            }

            // 主力军 2: 7-Zip CLI 原生引擎 + Formats 插件扩展 (NSIS, CAB, SFX, Asar, PyInstaller, Iso, MSI 等)
            string sevenZip = EnsureSevenZipEngine(log);
            if (!string.IsNullOrEmpty(sevenZip))
            {
                log("[主力军] 调用 7-Zip 内置引擎直接解压提取 (包含 Asar/Py7z/Iso7z/Grit/Codecs 插件扩展)...");
                string typeSwitch = (detection.Type == InstallerType.Nsis) ? " -tNsis" : "";
                string args = string.Format("x \"{0}\" -o\"{1}\"{2} -y", options.SourceFile, options.TargetDirectory, typeSwitch);
                int exitCode = RunProcess(sevenZip, args, null, true, log);
                progress(75);

                if ((exitCode == 0 || exitCode == 1) && DirectoryHasFiles(options.TargetDirectory))
                {
                    PostProcessNestedPayloads(options.TargetDirectory, sevenZip, log);
                    progress(100);
                    log("[成功] 7-Zip 引擎成功解压提取完成！");
                    log("[位置] 文件已存放至: " + options.TargetDirectory);
                    AppLogger.Info("7-Zip extraction succeeded with exit code: " + exitCode);
                    return true;
                }
                else
                {
                    AppLogger.Warn("7-Zip extraction returned exitCode: " + exitCode + ", checking native fallbacks...");
                    log("[提示] 7-Zip 原生解压完成 (状态码: " + exitCode + ")，进入系统原生与智能保底验证...");
                }
            }

            // 保底策略 3: MSI 原生行政解压 (msiexec /a)
            if (detection.Type == InstallerType.Msi || options.SourceFile.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
            {
                log("[原生解压] 正在调用 Windows Installer 行政解压 (msiexec /a)...");
                log("[特性说明] 该模式仅提取内部组件至目标文件夹，绝对不向系统注册表写入任何安装项。");

                string args = string.Format("/a \"{0}\" /qn TARGETDIR=\"{1}\"", options.SourceFile, options.TargetDirectory);
                int exitCode = RunProcess("msiexec.exe", args, null, false, log);
                progress(90);

                if (exitCode == 0 && DirectoryHasFiles(options.TargetDirectory))
                {
                    progress(100);
                    log("[成功] Windows 原生行政解压提取完成！");
                    AppLogger.Info("msiexec /a administrative extraction succeeded.");
                    return true;
                }
                else
                {
                    AppLogger.Warn("msiexec /a returned exitCode: " + exitCode);
                }
            }

            // 保底策略 4: Windows 内置 tar (适用于各类 SFX 与标准归档)
            if (options.PreferNativeTools)
            {
                log("[原生解压] 尝试 Windows 内置 tar 工具提取...");
                string args = string.Format("-xf \"{0}\" -C \"{1}\"", options.SourceFile, options.TargetDirectory);
                int exitCode = RunProcess("tar.exe", args, null, false, log);
                if (exitCode == 0 && DirectoryHasFiles(options.TargetDirectory))
                {
                    progress(100);
                    log("[成功] 系统 tar 工具成功解包！");
                    AppLogger.Info("tar.exe extraction succeeded.");
                    return true;
                }
            }

            progress(100);
            if (DirectoryHasFiles(options.TargetDirectory))
            {
                log("[完成] 目标目录中已存在解压出的文件: " + options.TargetDirectory);
                AppLogger.Info("Target directory has files, marking as completed.");
                return true;
            }

            string failMsg = "[提示] 未能纯解包此安装程序。请尝试切换到【降权静默安装】模式。";
            AppLogger.Warn(failMsg);
            log(failMsg);
            return false;
        }

        private static bool ExecuteInstallMode(ExtractionOptions options, DetectionResult detection, Action<string> log, Action<int> progress)
        {
            log("================ [开始降权静默安装 (RunAsInvoker)] ================");
            progress(10);

            string exePath = options.SourceFile;
            string targetDir = options.TargetDirectory;
            string args = "";

            if (!string.IsNullOrEmpty(options.CustomSilentArgs))
            {
                args = options.CustomSilentArgs;
                log("[自定义静默参数] " + args);
            }
            else
            {
                switch (detection.Type)
                {
                    case InstallerType.Msi:
                        exePath = "msiexec.exe";
                        args = string.Format("/i \"{0}\" /qn INSTALLDIR=\"{1}\" TARGETDIR=\"{2}\"",
                            options.SourceFile, targetDir, targetDir);
                        break;

                    case InstallerType.InnoSetup:
                        args = string.Format("/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR=\"{0}\"", targetDir);
                        if (!options.AllowRegistryAndShortcuts)
                        {
                            args += " /MERGETASKS=\"!desktopicon,!quicklaunchicon\"";
                        }
                        break;

                    case InstallerType.Nsis:
                        args = string.Format("/S /D={0}", targetDir);
                        break;

                    case InstallerType.WixBurn:
                        args = string.Format("/quiet /passive /norestart InstallFolder=\"{0}\"", targetDir);
                        break;

                    case InstallerType.InstallShield:
                        args = string.Format("/s /v\"/qn INSTALLDIR=\\\"{0}\\\"\"", targetDir);
                        break;

                    case InstallerType.AdvancedInstaller:
                        args = string.Format("/quiet /qn APPDIR=\"{0}\" TARGETDIR=\"{0}\"", targetDir);
                        break;

                    case InstallerType.SevenZipSfx:
                    case InstallerType.ZipSfx:
                    case InstallerType.RarSfx:
                        args = string.Format("-o\"{0}\" -y", targetDir);
                        break;

                    default:
                        args = string.Format("/S /quiet /qn TARGETDIR=\"{0}\" /DIR=\"{0}\"", targetDir);
                        break;
                }
            }

            log("[执行程序] " + exePath);
            log("[参数序列] " + args);
            if (options.ForceRunAsInvoker)
            {
                AppLogger.Info("Injecting __COMPAT_LAYER=RunAsInvoker to suppress UAC prompts.");
                log("[UAC规避机制] 注入 __COMPAT_LAYER=RunAsInvoker，强制用户态运行，绝对阻断管理员 UAC 弹窗。");
            }

            int exitCode = RunProcess(exePath, args, options.ForceRunAsInvoker ? "RunAsInvoker" : null, true, log);
            progress(100);

            AppLogger.Info("Installer process exited with code: " + exitCode);
            if (exitCode == 0 || exitCode == 3010)
            {
                log("[成功] 安装程序执行完毕 (退出码: " + exitCode + ")！");
                log("[完成] 目标路径: " + targetDir);
                return true;
            }
            else
            {
                log("[完成] 安装程序执行完毕 (退出码: " + exitCode + ")");
                return true;
            }
        }

        private static bool DirectoryHasFiles(string dir)
        {
            try
            {
                return Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static void PostProcessNestedPayloads(string targetDir, string sevenZip, Action<string> log)
        {
            try
            {
                // 1. 深度处理 Electron-Builder NSIS 双层嵌套架构 ($PLUGINSDIR\app-*.7z)
                string pluginsDir = Path.Combine(targetDir, "$PLUGINSDIR");
                if (Directory.Exists(pluginsDir))
                {
                    string[] nestedArchives = Directory.GetFiles(pluginsDir, "app-*.7z", SearchOption.TopDirectoryOnly);
                    foreach (var archive in nestedArchives)
                    {
                        log("[双层嵌套探测] 检测到 Electron-Builder 核心载荷 (" + Path.GetFileName(archive) + ")，正在启动二级深度物理解包...");
                        AppLogger.Info("Detected nested archive: " + archive);
                        string subArgs = string.Format("x \"{0}\" -o\"{1}\" -y", archive, targetDir);
                        int subExit = RunProcess(sevenZip, subArgs, null, true, log);
                        if (subExit == 0 || subExit == 1)
                        {
                            log("[成功] 二级核心载荷解压完成！");
                            AppLogger.Info("Secondary nested archive extracted: " + archive);
                        }
                    }

                    // 2. 提取 $R0 目录下释放的官方卸载程序（如有）
                    string r0Dir = Path.Combine(targetDir, "$R0");
                    if (Directory.Exists(r0Dir))
                    {
                        foreach (var uninst in Directory.GetFiles(r0Dir, "Uninstall*.exe"))
                        {
                            string dest = Path.Combine(targetDir, Path.GetFileName(uninst));
                            try
                            {
                                File.Copy(uninst, dest, true);
                                AppLogger.Info("Moved uninstaller to root: " + dest);
                            }
                            catch { }
                        }
                        try { Directory.Delete(r0Dir, true); } catch { }
                    }

                    // 3. 彻底清理 $PLUGINSDIR 临时引导残留
                    try { Directory.Delete(pluginsDir, true); } catch { }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("PostProcessNestedPayloads encountered warning: " + ex.Message);
            }
        }

        private static int RunProcess(string fileName, string arguments, string compatLayer, bool logOutput, Action<string> log)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.Default,
                    StandardErrorEncoding = Encoding.Default
                };

                if (!string.IsNullOrEmpty(compatLayer))
                {
                    psi.EnvironmentVariables["__COMPAT_LAYER"] = compatLayer;
                    AppLogger.Debug("Set Environment Variable: __COMPAT_LAYER=" + compatLayer);
                }

                using (var process = new Process())
                {
                    process.StartInfo = psi;

                    if (logOutput)
                    {
                        process.OutputDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                AppLogger.Debug("[STDOUT] " + e.Data);
                                log("[输出] " + e.Data);
                            }
                        };
                        process.ErrorDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                AppLogger.Warn("[STDERR] " + e.Data);
                                log("[日志] " + e.Data);
                            }
                        };
                    }

                    AppLogger.Info(string.Format("Executing: \"{0}\" {1}", fileName, arguments));
                    log(string.Format("[执行命令] {0} {1}", fileName, arguments));
                    process.Start();

                    if (logOutput)
                    {
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                    }

                    while (!process.WaitForExit(1000))
                    {
                    }

                    AppLogger.Info("Process finished. ExitCode=" + process.ExitCode);
                    return process.ExitCode;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Process execution failed: " + fileName, ex);
                log("[启动失败] " + ex.Message);
                return -1;
            }
        }
    }
}
