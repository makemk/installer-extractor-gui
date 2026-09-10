using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using InstallerExtractorGUI.Core;

namespace InstallerExtractorGUI
{
    public class MainForm : Form
    {
        private TextBox txtSourceFile;
        private Button btnBrowseSource;
        private TextBox txtTargetDir;
        private Button btnBrowseTarget;
        private Label lblDetection;

        private RadioButton rbExtractOnly;
        private RadioButton rbSilentInstall;

        private CheckBox chkForceRunAsInvoker;
        private CheckBox chkAllowRegistry;
        private CheckBox chkPreferNative;
        private CheckBox chkEnableLog;
        private CheckBox chkInstallDrivers;

        private TextBox txtCustomArgs;
        private Label lblCustomArgs;

        private RichTextBox rtbLog;
        private ProgressBar progressBar;
        private Label lblStatus;
        private Button btnStart;
        private Button btnOpenTarget;
        private Button btnInstallDrivers;
        private Button btnPlugins;
        private Button btnOpenLogs;
        private Button btnClearLog;

        private bool isProcessing = false;

        public MainForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "exe / msi 解压安装工具 (集成驱动安装 / 7-Zip 全套 / AI 诊断)";
            this.Size = new Size(880, 720);
            this.MinimumSize = new Size(780, 630);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.AllowDrop = true;
            this.BackColor = Color.FromArgb(248, 249, 250);

            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;

            // 顶部面板：文件与目录选择
            GroupBox grpFiles = new GroupBox
            {
                Text = " 文件与路径 (支持直接把 exe / msi 安装包拖入本窗口) ",
                Location = new Point(16, 12),
                Size = new Size(830, 155),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Color.FromArgb(33, 37, 41)
            };

            Label lblSource = new Label { Text = "安装包文件:", Location = new Point(16, 28), AutoSize = true };
            txtSourceFile = new TextBox { Location = new Point(110, 25), Size = new Size(590, 23), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            txtSourceFile.TextChanged += (s, e) => UpdateFileDetection();
            btnBrowseSource = new Button { Text = "选择文件...", Location = new Point(710, 24), Size = new Size(104, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnBrowseSource.Click += BtnBrowseSource_Click;

            Label lblTarget = new Label { Text = "输出目录:", Location = new Point(16, 62), AutoSize = true };
            txtTargetDir = new TextBox { Location = new Point(110, 59), Size = new Size(590, 23), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            btnBrowseTarget = new Button { Text = "浏览目录...", Location = new Point(710, 58), Size = new Size(104, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnBrowseTarget.Click += BtnBrowseTarget_Click;

            lblDetection = new Label
            {
                Text = "提示: 请选择或拖入 .exe / .msi 安装包文件，系统将自动识别类型与特征。",
                Location = new Point(110, 95),
                Size = new Size(700, 50),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Color.FromArgb(13, 110, 253)
            };

            grpFiles.Controls.AddRange(new Control[] { lblSource, txtSourceFile, btnBrowseSource, lblTarget, txtTargetDir, btnBrowseTarget, lblDetection });

            // 中部面板：模式与核心选项
            GroupBox grpOptions = new GroupBox
            {
                Text = " 处理策略与高级选项 ",
                Location = new Point(16, 175),
                Size = new Size(830, 165),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Color.FromArgb(33, 37, 41)
            };

            rbExtractOnly = new RadioButton
            {
                Text = "【主力军】纯解包提取 (7-Zip CLI + innounp + Formats 插件 + 原生行政解压，绿色免安装，不写注册表)",
                Location = new Point(18, 24),
                Size = new Size(790, 24),
                Checked = true,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(13, 110, 253)
            };
            rbExtractOnly.CheckedChanged += Mode_CheckedChanged;

            rbSilentInstall = new RadioButton
            {
                Text = "【备用】降权静默安装 (通过 RunAsInvoker 强制普通用户权限，拦截管理员提权，部署至指定目录)",
                Location = new Point(18, 50),
                Size = new Size(790, 24),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
            };
            rbSilentInstall.CheckedChanged += Mode_CheckedChanged;

            chkForceRunAsInvoker = new CheckBox
            {
                Text = "强制注入 RunAsInvoker 环境变量 (避免触发 Windows 管理员 UAC 弹窗)",
                Location = new Point(40, 78),
                Size = new Size(460, 22),
                Checked = true
            };

            chkAllowRegistry = new CheckBox
            {
                Text = "允许写入注册表和快捷方式 (仅在静默安装模式生效)",
                Location = new Point(40, 102),
                Size = new Size(460, 22),
                Checked = false,
                Enabled = false
            };

            chkPreferNative = new CheckBox
            {
                Text = "启用 Windows 原生保底 (MSI 行政解压 msiexec /a 与系统 tar 备选)",
                Location = new Point(40, 126),
                Size = new Size(460, 22),
                Checked = true
            };

            chkInstallDrivers = new CheckBox
            {
                Text = "若检测到硬件驱动 (.inf / dpinst) 解压后自动安装 [需提权]",
                Location = new Point(510, 102),
                Size = new Size(310, 22),
                Checked = false
            };

            chkEnableLog = new CheckBox
            {
                Text = "记录详细诊断日志到文件 (便于 AI 自动化排错)",
                Location = new Point(510, 126),
                Size = new Size(310, 22),
                Checked = true
            };
            chkEnableLog.CheckedChanged += (s, e) => AppLogger.IsEnabled = chkEnableLog.Checked;

            lblCustomArgs = new Label { Text = "可选自定义参数:", Location = new Point(510, 79), AutoSize = true, Visible = false };
            txtCustomArgs = new TextBox { Location = new Point(615, 76), Size = new Size(195, 23), Visible = false };

            grpOptions.Controls.AddRange(new Control[] {
                rbExtractOnly, rbSilentInstall,
                chkForceRunAsInvoker, chkAllowRegistry,
                chkPreferNative, chkInstallDrivers, chkEnableLog,
                lblCustomArgs, txtCustomArgs
            });

            // 底部日志控制区
            Label lblLogTitle = new Label { Text = "实时输出日志 (包含解压进程与驱动注入流):", Location = new Point(16, 348), AutoSize = true };

            rtbLog = new RichTextBox
            {
                Location = new Point(16, 370),
                Size = new Size(830, 215),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ReadOnly = true,
                BackColor = Color.FromArgb(24, 26, 27),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Consolas", 9.5F, FontStyle.Regular),
                BorderStyle = BorderStyle.FixedSingle
            };

            progressBar = new ProgressBar
            {
                Location = new Point(16, 595),
                Size = new Size(830, 14),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            lblStatus = new Label
            {
                Text = "状态: 就绪",
                Location = new Point(16, 625),
                Size = new Size(200, 24),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft
            };

            btnStart = new Button
            {
                Text = "▶ 开始执行",
                Location = new Point(220, 618),
                Size = new Size(100, 36),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                BackColor = Color.FromArgb(13, 110, 253),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold)
            };
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.Click += BtnStart_Click;

            btnOpenTarget = new Button
            {
                Text = "📂 打开目标",
                Location = new Point(330, 618),
                Size = new Size(95, 36),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnOpenTarget.Click += BtnOpenTarget_Click;

            btnInstallDrivers = new Button
            {
                Text = "⚡ 安装目录驱动",
                Location = new Point(435, 618),
                Size = new Size(115, 36),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnInstallDrivers.Click += BtnInstallDrivers_Click;

            btnPlugins = new Button
            {
                Text = "🧩 插件管理",
                Location = new Point(560, 618),
                Size = new Size(95, 36),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnPlugins.Click += BtnPlugins_Click;

            btnOpenLogs = new Button
            {
                Text = "📄 查看日志",
                Location = new Point(665, 618),
                Size = new Size(95, 36),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnOpenLogs.Click += BtnOpenLogs_Click;

            btnClearLog = new Button
            {
                Text = "清空显示",
                Location = new Point(770, 618),
                Size = new Size(76, 36),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnClearLog.Click += (s, e) => rtbLog.Clear();

            this.Controls.AddRange(new Control[] {
                grpFiles, grpOptions,
                lblLogTitle, rtbLog,
                progressBar, lblStatus,
                btnStart, btnOpenTarget, btnInstallDrivers, btnPlugins, btnOpenLogs, btnClearLog
            });

            AppLogger.Initialize(chkEnableLog.Checked);
            AppLogger.Info("MainForm GUI initialized with driver support.");

            Log("==================================================");
            Log("欢迎使用 exe / msi 解压安装工具 (驱动安装与 AI 诊断版)");
            Log("主力军：7-Zip CLI + innounp + 专用 Formats 插件");
            Log("设备驱动：支持解压后一键调用 pnputil 安装硬件驱动");
            Log("==================================================");

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    ExtractorEngine.EnsureSevenZipEngine(msg => this.BeginInvoke(new Action(() => Log(msg))));
                    ExtractorEngine.EnsureInnounp(msg => this.BeginInvoke(new Action(() => Log(msg))));
                }
                catch (Exception ex)
                {
                    AppLogger.Error("Engine startup error", ex);
                }
            });
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                txtSourceFile.Text = files[0];
            }
        }

        private void BtnBrowseSource_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "选择 exe 或 msi 安装包";
                ofd.Filter = "安装程序包 (*.exe;*.msi)|*.exe;*.msi|可执行文件 (*.exe)|*.exe|MSI 安装包 (*.msi)|*.msi|所有文件 (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtSourceFile.Text = ofd.FileName;
                }
            }
        }

        private void BtnBrowseTarget_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "选择文件输出目录";
                if (!string.IsNullOrEmpty(txtTargetDir.Text) && Directory.Exists(txtTargetDir.Text))
                {
                    fbd.SelectedPath = txtTargetDir.Text;
                }
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtTargetDir.Text = fbd.SelectedPath;
                }
            }
        }

        private void UpdateFileDetection()
        {
            string path = txtSourceFile.Text.Trim('"', ' ');
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                lblDetection.Text = "提示: 请选择或拖入 .exe / .msi 安装包文件。";
                lblDetection.ForeColor = Color.FromArgb(108, 117, 125);
                return;
            }

            if (string.IsNullOrEmpty(txtTargetDir.Text) || txtTargetDir.Text.EndsWith("_extracted"))
            {
                string dir = Path.GetDirectoryName(path);
                string fileName = Path.GetFileNameWithoutExtension(path);
                txtTargetDir.Text = Path.Combine(dir, fileName + "_extracted");
            }

            DetectionResult res = InstallerDetector.Detect(path);
            lblDetection.Text = string.Format("【{0}】{1}", res.DisplayName, res.Description);
            lblDetection.ForeColor = Color.FromArgb(13, 110, 253);

            if (!string.IsNullOrEmpty(res.RecommendedSilentArgs) && string.IsNullOrEmpty(txtCustomArgs.Text))
            {
                txtCustomArgs.Text = res.RecommendedSilentArgs;
            }
        }

        private void Mode_CheckedChanged(object sender, EventArgs e)
        {
            chkAllowRegistry.Enabled = rbSilentInstall.Checked;
            lblCustomArgs.Visible = rbSilentInstall.Checked;
            txtCustomArgs.Visible = rbSilentInstall.Checked;
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (isProcessing) return;

            string source = txtSourceFile.Text.Trim('"', ' ');
            string target = txtTargetDir.Text.Trim('"', ' ');

            if (string.IsNullOrEmpty(source) || !File.Exists(source))
            {
                MessageBox.Show(this, "请先选择有效的安装包文件！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(target))
            {
                MessageBox.Show(this, "请指定目标输出文件夹！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var options = new ExtractionOptions
            {
                SourceFile = source,
                TargetDirectory = target,
                IsExtractOnly = rbExtractOnly.Checked,
                ForceRunAsInvoker = chkForceRunAsInvoker.Checked,
                AllowRegistryAndShortcuts = chkAllowRegistry.Checked,
                PreferNativeTools = chkPreferNative.Checked,
                CustomSilentArgs = txtCustomArgs.Text.Trim()
            };

            bool autoInstallDrivers = chkInstallDrivers.Checked;

            SetUiBusy(true);
            Log(string.Format("\n[{0}] 任务已启动...", DateTime.Now.ToString("HH:mm:ss")));

            ThreadPool.QueueUserWorkItem(_ =>
            {
                bool success = false;
                try
                {
                    success = ExtractorEngine.Run(
                        options,
                        msg => this.BeginInvoke(new Action(() => Log(msg))),
                        pct => this.BeginInvoke(new Action(() => progressBar.Value = Math.Min(100, Math.Max(0, pct))))
                    );

                    if (success && autoInstallDrivers)
                    {
                        var scan = DriverInstaller.ScanDrivers(target);
                        if (scan.HasDrivers)
                        {
                            this.BeginInvoke(new Action(() => Log("\n[自动驱动安装] 检测到硬件驱动，正在启动提权安装...")));
                            DriverInstaller.InstallDrivers(target, msg => this.BeginInvoke(new Action(() => Log(msg))));
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error("Execution task exception", ex);
                    this.BeginInvoke(new Action(() => Log("[未处理异常] " + ex.Message)));
                }
                finally
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        SetUiBusy(false);
                        lblStatus.Text = success ? "状态: 执行成功！" : "状态: 执行完毕（部分步骤可能未完成）";
                        if (success)
                        {
                            var dr = MessageBox.Show(this, "解压/部署完成！是否立即打开目标输出目录？", "操作完成", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                            if (dr == DialogResult.Yes)
                            {
                                OpenTargetDirectory();
                            }
                        }
                    }));
                }
            });
        }

        private void BtnInstallDrivers_Click(object sender, EventArgs e)
        {
            string target = txtTargetDir.Text.Trim('"', ' ');
            if (string.IsNullOrEmpty(target) || !Directory.Exists(target))
            {
                MessageBox.Show(this, "目标目录尚不存在，请先选择或解压出目标文件夹！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var scan = DriverInstaller.ScanDrivers(target);
            if (!scan.HasDrivers)
            {
                MessageBox.Show(this, "在目标目录中未找到硬件驱动文件 (.inf 或 InstDrivers.exe)！", "未发现驱动", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(this,
                string.Format("在目标目录中检测到 {0} 个驱动配置文件与 {1} 个原厂安装器。\n\n安装驱动需要管理员权限 (UAC 确认)。是否立即安装？",
                scan.InfFiles.Count, scan.InstallerExes.Count),
                "确认安装驱动", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            Log("\n[驱动安装] 启动硬件驱动安装流水线: " + target);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                DriverInstaller.InstallDrivers(target, msg => this.BeginInvoke(new Action(() => Log(msg))));
            });
        }

        private void SetUiBusy(bool busy)
        {
            isProcessing = busy;
            btnStart.Enabled = !busy;
            btnBrowseSource.Enabled = !busy;
            btnBrowseTarget.Enabled = !busy;
            btnInstallDrivers.Enabled = !busy;
            rbExtractOnly.Enabled = !busy;
            rbSilentInstall.Enabled = !busy;
            lblStatus.Text = busy ? "状态: 正在处理中，请稍候..." : "状态: 就绪";
            progressBar.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
            if (!busy) progressBar.Value = 100;
        }

        private void BtnOpenTarget_Click(object sender, EventArgs e)
        {
            OpenTargetDirectory();
        }

        private void BtnPlugins_Click(object sender, EventArgs e)
        {
            string dir = ExtractorEngine.GetFormatsDirectory();
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            Process.Start("explorer.exe", dir);
        }

        private void BtnOpenLogs_Click(object sender, EventArgs e)
        {
            string dir = AppLogger.GetDefaultLogDirectory();
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            Process.Start("explorer.exe", dir);
        }

        private void OpenTargetDirectory()
        {
            string target = txtTargetDir.Text.Trim('"', ' ');
            if (Directory.Exists(target))
            {
                Process.Start("explorer.exe", target);
            }
            else
            {
                MessageBox.Show(this, "目标目录尚不存在: " + target, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void Log(string message)
        {
            if (rtbLog.IsDisposed) return;
            rtbLog.AppendText(message + Environment.NewLine);
            rtbLog.SelectionStart = rtbLog.Text.Length;
            rtbLog.ScrollToCaret();
        }
    }
}
