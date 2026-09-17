using System;
using System.IO;
using System.Text;

namespace InstallerExtractorGUI.Core
{
    public enum InstallerType
    {
        Unknown,
        Msi,
        InnoSetup,
        Nsis,
        SevenZipSfx,
        RarSfx,
        ZipSfx,
        InstallShield,
        WixBurn,
        AdvancedInstaller,
        GenericExe
    }

    public class DetectionResult
    {
        public InstallerType Type { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string RecommendedSilentArgs { get; set; }
        public bool CanDirectExtract { get; set; }
    }

    public static class InstallerDetector
    {
        public static DetectionResult Detect(string filePath)
        {
            var result = new DetectionResult
            {
                Type = InstallerType.Unknown,
                DisplayName = "未知文件类型",
                Description = "未识别的安装包类型",
                RecommendedSilentArgs = "/S",
                CanDirectExtract = false
            };

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return result;
            }

            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (ext == ".msi")
            {
                result.Type = InstallerType.Msi;
                result.DisplayName = "Windows Installer (MSI)";
                result.Description = "支持系统原生行政解压 (msiexec /a)，可完全免提权直接提取所有文件。";
                result.RecommendedSilentArgs = "/qn";
                result.CanDirectExtract = true;
                return result;
            }

            if (ext == ".exe")
            {
                // 读取文件头部或前 4MB 内容进行特征码扫描
                try
                {
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        byte[] buffer = new byte[Math.Min(fs.Length, 4 * 1024 * 1024)];
                        int read = fs.Read(buffer, 0, buffer.Length);
                        string ascii = Encoding.ASCII.GetString(buffer, 0, read);

                        // Inno Setup (必须优先于纯压缩特征探测)
                        if (ascii.IndexOf("Inno Setup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ascii.IndexOf("InnoCallback", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            result.Type = InstallerType.InnoSetup;
                            result.DisplayName = "Inno Setup 安装包";
                            result.Description = "支持降权静默部署 (/VERYSILENT /DIR=...) 或借助解包工具直接提取。";
                            result.RecommendedSilentArgs = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART";
                            result.CanDirectExtract = true; // 7z / innounp
                            return result;
                        }

                        // NSIS (包含 Electron-Builder 双层嵌套 NSIS，必须优先于内部嵌合的 7z SFX 卷探测)
                        if (ascii.IndexOf("NullsoftInst", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ascii.IndexOf("Nullsoft", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            result.Type = InstallerType.Nsis;
                            bool isElectron = (ascii.IndexOf("electron-builder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               ascii.IndexOf("app-64.7z", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               ascii.IndexOf("app-32.7z", StringComparison.OrdinalIgnoreCase) >= 0);

                            result.DisplayName = isElectron ? "NSIS (Electron-Builder 双层架构)" : "NSIS (Nullsoft) 安装包";
                            result.Description = isElectron
                                ? "Electron-Builder 嵌套安装包，内部嵌合 7z 核心载荷，套件已原生适配自动二级深度脱壳。"
                                : "支持 7-Zip 直接解压提取，或使用降权静默参数 (/S /D=...)。";
                            result.RecommendedSilentArgs = "/S";
                            result.CanDirectExtract = true;
                            return result;
                        }

                        // 7z SFX 独立自解压程序 (37 7A BC AF 27 1C)
                        if (ContainsBytes(buffer, new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C }))
                        {
                            result.Type = InstallerType.SevenZipSfx;
                            result.DisplayName = "7-Zip 自解压程序 (7z SFX)";
                            result.Description = "内置 7z 压缩卷，支持 7z CLI 或 tar 直接解压提取文件。";
                            result.RecommendedSilentArgs = "-y";
                            result.CanDirectExtract = true;
                            return result;
                        }

                        // WiX Burn
                        if (ascii.IndexOf("WixBundleOriginalSource", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ascii.IndexOf("BurnPipe", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            result.Type = InstallerType.WixBurn;
                            result.DisplayName = "WiX Burn 引导安装包";
                            result.Description = "支持降权静默安装参数 (/quiet /passive)。";
                            result.RecommendedSilentArgs = "/quiet /passive /norestart";
                            result.CanDirectExtract = false;
                            return result;
                        }

                        // InstallShield
                        if (ascii.IndexOf("InstallShield", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            result.Type = InstallerType.InstallShield;
                            result.DisplayName = "InstallShield 安装程序";
                            result.Description = "支持静默运行 (/s /v\"/qn\")。";
                            result.RecommendedSilentArgs = "/s /v\"/qn\"";
                            result.CanDirectExtract = false;
                            return result;
                        }

                        // Advanced Installer (Caphyon)
                        if (ascii.IndexOf("Advanced Installer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ascii.IndexOf("Caphyon", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            result.Type = InstallerType.AdvancedInstaller;
                            result.DisplayName = "Advanced Installer 封装包";
                            result.Description = "基于 Advanced Installer 引导封装，支持 /extract 释放内部 MSI/CAB，并配合行政解包。";
                            result.RecommendedSilentArgs = "/quiet /qn";
                            result.CanDirectExtract = true;
                            return result;
                        }

                        // WinRAR SFX
                        if (ContainsBytes(buffer, new byte[] { 0x52, 0x61, 0x72, 0x21 }))
                        {
                            result.Type = InstallerType.RarSfx;
                            result.DisplayName = "WinRAR 自解压程序 (RAR SFX)";
                            result.Description = "包含 RAR 压缩包，支持 7-Zip 直接提取。";
                            result.RecommendedSilentArgs = "-s";
                            result.CanDirectExtract = true;
                            return result;
                        }

                        // Zip SFX (PK..)
                        if (ContainsBytes(buffer, new byte[] { 0x50, 0x4B, 0x03, 0x04 }))
                        {
                            result.Type = InstallerType.ZipSfx;
                            result.DisplayName = "ZIP 自解压程序 (Zip SFX)";
                            result.Description = "包含 ZIP 归档，支持 Windows 自带组件或 7-Zip 直接解压。";
                            result.RecommendedSilentArgs = "-s";
                            result.CanDirectExtract = true;
                            return result;
                        }
                    }
                }
                catch
                {
                    // 忽略读取异常，按通用 exe 处理
                }

                result.Type = InstallerType.GenericExe;
                result.DisplayName = "通用 Windows 可执行程序 / 安装包";
                result.Description = "未明确打包工具，支持降权运行 (RunAsInvoker) 配合常见静默参数，或尝试 7z 兜底解压。";
                result.RecommendedSilentArgs = "/S /quiet /qn";
                result.CanDirectExtract = true;
                return result;
            }

            return result;
        }

        private static bool ContainsBytes(byte[] haystack, byte[] needle)
        {
            if (haystack == null || needle == null || haystack.Length < needle.Length)
                return false;

            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return true;
            }
            return false;
        }
    }
}

