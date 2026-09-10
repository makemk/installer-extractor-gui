# InstallerExtractor Build Script (GUI & CLI for AI)
$cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $cscPath)) {
    $cscPath = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (-not (Test-Path $cscPath)) {
    Write-Error "csc.exe compiler not found in Windows directory!"
    exit 1
}

$references = "System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll"

$resArgs = @(
    "/res:7z.exe,SevenZip_exe",
    "/res:7z.dll,SevenZip_dll",
    "/res:tools\innounp.exe,innounp_exe",
    "/res:Formats\Asar.64.dll,plugin_asar",
    "/res:Formats\Py7z.64.dll,plugin_py7z",
    "/res:Formats\Grit7z.64.dll,plugin_grit7z",
    "/res:Formats\Iso7z.64.dll,plugin_iso7z",
    "/res:Codecs\Modern7z.64.dll,codec_modern7z",
    "/res:Codecs\libzstd.64.dll,codec_zstd",
    "/res:Codecs\liblz4.64.dll,codec_lz4",
    "/res:Codecs\fast-lzma2.64.dll,codec_fastlzma2"
)

# 1. 编译 GUI 版本
Write-Host "Compiling InstallerExtractorGUI.exe (WinForms GUI)..." -ForegroundColor Cyan
$guiSources = @("Program.cs", "MainForm.cs", "Core\InstallerDetector.cs", "Core\ExtractorEngine.cs", "Core\AppLogger.cs", "Core\DriverInstaller.cs")
& $cscPath /target:winexe /out:InstallerExtractorGUI.exe /win32manifest:app.manifest /optimize+ /platform:anycpu $resArgs "/r:$references" $guiSources
if ($LASTEXITCODE -ne 0) { Write-Error "GUI build failed!"; exit 1 }

# 2. 编译 CLI 版本 (专为 AI Agent 与自动化脚本调用设计)
Write-Host "Compiling InstallerExtractorCLI.exe (Console CLI for AI)..." -ForegroundColor Cyan
$cliSources = @("CliProgram.cs", "Core\InstallerDetector.cs", "Core\ExtractorEngine.cs", "Core\AppLogger.cs", "Core\DriverInstaller.cs")
& $cscPath /target:exe /out:InstallerExtractorCLI.exe /win32manifest:app.manifest /optimize+ /platform:anycpu $resArgs "/r:$references" $cliSources
if ($LASTEXITCODE -ne 0) { Write-Error "CLI build failed!"; exit 1 }

Write-Host "`n[SUCCESS] Build Completed for Both GUI & CLI!" -ForegroundColor Green
$guiItem = Get-Item "InstallerExtractorGUI.exe"
$cliItem = Get-Item "InstallerExtractorCLI.exe"
Write-Host "1. GUI: $($guiItem.Name) - $([math]::Round($guiItem.Length / 1MB, 2)) MB (Double-click GUI for humans)" -ForegroundColor Green
Write-Host "2. CLI: $($cliItem.Name) - $([math]::Round($cliItem.Length / 1MB, 2)) MB (Command-line tool for AI Agents)" -ForegroundColor Green
