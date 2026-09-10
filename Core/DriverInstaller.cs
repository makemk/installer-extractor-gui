using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace InstallerExtractorGUI.Core
{
    public class DriverScanResult
    {
        public List<string> InfFiles { get; set; }
        public List<string> InstallerExes { get; set; }
        public bool HasDrivers { get { return InfFiles.Count > 0 || InstallerExes.Count > 0; } }

        public DriverScanResult()
        {
            InfFiles = new List<string>();
            InstallerExes = new List<string>();
        }
    }

    public static class DriverInstaller
    {
        public static DriverScanResult ScanDrivers(string directory)
        {
            var result = new DriverScanResult();
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return result;
            }

            try
            {
                // 递归查找 .inf 文件
                string[] infs = Directory.GetFiles(directory, "*.inf", SearchOption.AllDirectories);
                bool is64Bit = Environment.Is64BitOperatingSystem;

                foreach (var inf in infs)
                {
                    string pathLower = inf.ToLowerInvariant();
                    // 如果是 64 位系统，过滤掉明显的 32 位/XP/Win2K 旧驱动目录
                    if (is64Bit && (pathLower.Contains("x86") || pathLower.Contains("win2k") || pathLower.Contains(@"\xp\") || pathLower.Contains("xp_")))
                    {
                        continue;
                    }
                    result.InfFiles.Add(inf);
                }

                // 查找专用的驱动安装器 (如 InstDrivers.exe, dpinst_x64.exe)
                string[] exes = Directory.GetFiles(directory, "*.exe", SearchOption.AllDirectories);
                foreach (var exe in exes)
                {
                    string pathLower = exe.ToLowerInvariant();
                    string nameLower = Path.GetFileName(exe).ToLowerInvariant();

                    if (is64Bit && (pathLower.Contains("win2k") || pathLower.Contains("x86")))
                    {
                        continue;
                    }

                    if (nameLower.Equals("instdrivers.exe") ||
                        nameLower.Equals("dpinst_x64.exe") ||
                        nameLower.Equals("dpinst.exe"))
                    {
                        result.InstallerExes.Add(exe);
                    }
                }

                // 排序专用安装器：InstDrivers.exe 最优先，其次是 dpinst_x64.exe
                result.InstallerExes.Sort((a, b) =>
                {
                    string na = Path.GetFileName(a).ToLowerInvariant();
                    string nb = Path.GetFileName(b).ToLowerInvariant();
                    int scoreA = na.Equals("instdrivers.exe") ? 0 : (na.Equals("dpinst_x64.exe") ? 1 : 2);
                    int scoreB = nb.Equals("instdrivers.exe") ? 0 : (nb.Equals("dpinst_x64.exe") ? 1 : 2);
                    return scoreA.CompareTo(scoreB);
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error("Error scanning drivers in " + directory, ex);
            }

            return result;
        }

        public static bool InstallDrivers(string directory, Action<string> log)
        {
            var scan = ScanDrivers(directory);
            if (!scan.HasDrivers)
            {
                log("[驱动检测] 在目标目录未发现任何硬件驱动包 (.inf 或专用安装器)。");
                AppLogger.Info("No driver packages found in " + directory);
                return false;
            }

            log(string.Format("[驱动检测] 发现 {0} 个驱动文件与 {1} 个专用安装器，准备安装...",
                scan.InfFiles.Count, scan.InstallerExes.Count));
            AppLogger.Info(string.Format("Found {0} infs and {1} installer exes in {2}",
                scan.InfFiles.Count, scan.InstallerExes.Count, directory));

            // 1. 优先调用原厂驱动安装器 (如 SEGGER InstDrivers.exe)
            if (scan.InstallerExes.Count > 0)
            {
                foreach (var installer in scan.InstallerExes)
                {
                    log("[驱动安装] 正在调用官方原厂驱动安装程序: " + Path.GetFileName(installer));
                    int exitCode = ExecuteElevated(installer, "/silent", log);
                    if (exitCode == 0)
                    {
                        log("[驱动成功] 官方驱动程序安装完成！");
                        AppLogger.Info("Driver installer succeeded: " + installer);
                        return true;
                    }
                }
            }

            // 2. 调用 Windows 系统自带 pnputil 批量注册安装 .inf 文件
            if (scan.InfFiles.Count > 0)
            {
                bool anySuccess = false;
                foreach (var inf in scan.InfFiles)
                {
                    log("[驱动安装] 正在通过 pnputil 注入驱动: " + Path.GetFileName(inf));
                    string args = string.Format("/add-driver \"{0}\" /install", inf);
                    int exitCode = ExecuteElevated("pnputil.exe", args, log);

                    if (exitCode == 0 || exitCode == 3010)
                    {
                        anySuccess = true;
                        string statusMsg = exitCode == 3010 ? " (需重启生效)" : "";
                        log("[驱动成功] 驱动成功注入系统 DriverStore: " + Path.GetFileName(inf) + statusMsg);
                        AppLogger.Info("pnputil successfully installed driver: " + inf + statusMsg);
                    }
                    else
                    {
                        log("[驱动提示] pnputil 执行返回代码: " + exitCode);
                        AppLogger.Warn("pnputil returned exitCode: " + exitCode + " for " + inf);
                    }
                }
                return anySuccess;
            }

            return false;
        }

        private static int ExecuteElevated(string fileName, string arguments, Action<string> log)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true, // 需要 ShellExecute 以支持 UAC "runas" 提权
                    Verb = "runas"
                };

                log(string.Format("[提权执行] 请求管理员权限安装驱动: {0} {1}", Path.GetFileName(fileName), arguments));
                using (var process = Process.Start(psi))
                {
                    process.WaitForExit();
                    return process.ExitCode;
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // 用户点击取消或拒绝提权
                if (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
                {
                    log("[驱动警告] 用户取消了管理员提权确认 (UAC)，驱动安装已跳过。");
                    AppLogger.Warn("User cancelled UAC elevation for driver installation.");
                    return 1223;
                }
                log("[驱动异常] 驱动安装进程启动失败: " + ex.Message);
                AppLogger.Error("Driver installation failed to start", ex);
                return -1;
            }
            catch (Exception ex)
            {
                log("[驱动异常] 驱动安装出现错误: " + ex.Message);
                AppLogger.Error("Driver installation exception", ex);
                return -1;
            }
        }
    }
}
