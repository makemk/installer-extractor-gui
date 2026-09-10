@echo off
title Build InstallerExtractor (GUI and CLI for AI)
echo Compiling InstallerExtractorGUI and InstallerExtractorCLI...

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo [ERROR] csc.exe not found in Windows framework directory!
    pause
    exit /b 1
)

set RES=/res:7z.exe,SevenZip_exe /res:7z.dll,SevenZip_dll /res:tools\innounp.exe,innounp_exe /res:Formats\Asar.64.dll,plugin_asar /res:Formats\Py7z.64.dll,plugin_py7z /res:Formats\Grit7z.64.dll,plugin_grit7z /res:Formats\Iso7z.64.dll,plugin_iso7z /res:Codecs\Modern7z.64.dll,codec_modern7z /res:Codecs\libzstd.64.dll,codec_zstd /res:Codecs\liblz4.64.dll,codec_lz4 /res:Codecs\fast-lzma2.64.dll,codec_fastlzma2
set REFS=/r:System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll

echo [1/2] Building InstallerExtractorGUI.exe (WinForms GUI)...
"%CSC%" /target:winexe /out:InstallerExtractorGUI.exe /win32manifest:app.manifest /optimize+ /platform:anycpu %RES% %REFS% Program.cs MainForm.cs Core\InstallerDetector.cs Core\ExtractorEngine.cs Core\AppLogger.cs Core\DriverInstaller.cs
if %ERRORLEVEL% neq 0 (
    echo [ERROR] GUI build failed!
    pause
    exit /b 1
)

echo [2/2] Building InstallerExtractorCLI.exe (Console CLI for AI)...
"%CSC%" /target:exe /out:InstallerExtractorCLI.exe /win32manifest:app.manifest /optimize+ /platform:anycpu %RES% %REFS% CliProgram.cs Core\InstallerDetector.cs Core\ExtractorEngine.cs Core\AppLogger.cs Core\DriverInstaller.cs
if %ERRORLEVEL% neq 0 (
    echo [ERROR] CLI build failed!
    pause
    exit /b 1
)

echo.
echo ======================================================
echo [SUCCESS] Both GUI and CLI built successfully!
echo 1. InstallerExtractorGUI.exe (GUI for Users)
echo 2. InstallerExtractorCLI.exe (CLI for AI Agents)
echo ======================================================
pause
