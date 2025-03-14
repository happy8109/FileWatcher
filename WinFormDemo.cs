using System;
using System.IO;
using System.Windows.Forms;
using FileWatcherLib;

namespace WinFormDemo
{
    public partial class MainForm : Form
    {
        private FileWatcher watcher;
        private FileProcessor fileProcessor;
        private bool isWatching;

        public MainForm()
        {
            InitializeComponent();
            InitializeWatcher();
        }

        private void InitializeWatcher()
        {
            watcher = new FileWatcher();
            isWatching = false;

            // 设置要监视的文件类型
            watcher.SetFileTypes(".txt", ".jpg", ".png", ".doc", ".docx", ".pdf");
            
            // 设置文件名匹配模式（18位身份证号码）
            watcher.SetFileNamePattern(@"^\d{17}[\dXx]$");
        }

        private void btnSelectWatchFolder_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择要监视的文件夹";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtWatchFolder.Text = dialog.SelectedPath;
                    UpdateStartButtonState();
                }
            }
        }

        private void btnSelectTargetFolder_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择目标文件夹";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtTargetFolder.Text = dialog.SelectedPath;
                    UpdateStartButtonState();
                }
            }
        }

        private void btnStartStop_Click(object sender, EventArgs e)
        {
            if (!isWatching)
            {
                StartWatching();
            }
            else
            {
                StopWatching();
            }
        }

        private void StartWatching()
        {
            try
            {
                // 创建目标文件夹（如果不存在）
                if (!Directory.Exists(txtTargetFolder.Text))
                {
                    Directory.CreateDirectory(txtTargetFolder.Text);
                }

                // 设置监视路径
                watcher.SetWatchPath(txtWatchFolder.Text);

                // 创建文件处理器，传入是否扫描现有文件的设置
                fileProcessor = new FileProcessor(txtTargetFolder.Text, watcher, chkScanExisting.Checked);
                fileProcessor.FileProcessStatusChanged += OnFileProcessStatusChanged;

                // 注册文件创建事件处理程序
                watcher.FileCreated += (sender, e) => fileProcessor.EnqueueFile(e.FilePath);

                // 开始监视
                watcher.StartWatch();

                // 更新界面状态
                isWatching = true;
                btnStartStop.Text = "停止监视";
                txtWatchFolder.Enabled = false;
                txtTargetFolder.Enabled = false;
                btnSelectWatchFolder.Enabled = false;
                btnSelectTargetFolder.Enabled = false;
                chkScanExisting.Enabled = false;
                statusLabel.Text = "正在监视文件夹...";

                AddLogMessage(string.Format("开始监视文件夹: {0}", txtWatchFolder.Text));
                AddLogMessage(string.Format("目标文件夹: {0}", txtTargetFolder.Text));
                AddLogMessage("监视模式：仅处理指定类型且文件名为18位身份证号码的文件");
                AddLogMessage(string.Format("支持的文件类型：{0}", string.Join(", ", watcher.AllowedExtensions)));

                // 如果启用了扫描现有文件选项，FileProcessor会在构造时自动执行扫描
                if (chkScanExisting.Checked)
                {
                    AddLogMessage("正在扫描现有文件...");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("启动监视时发生错误: {0}", ex.Message),
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopWatching()
        {
            try
            {
                // 停止监视
                watcher.StopWatch();

                // 释放文件处理器
                if (fileProcessor != null)
                {
                    fileProcessor.Dispose();
                    fileProcessor = null;
                }

                // 更新界面状态
                isWatching = false;
                btnStartStop.Text = "开始监视";
                txtWatchFolder.Enabled = true;
                txtTargetFolder.Enabled = true;
                btnSelectWatchFolder.Enabled = true;
                btnSelectTargetFolder.Enabled = true;
                chkScanExisting.Enabled = true;
                statusLabel.Text = "已停止监视";

                AddLogMessage("已停止监视");
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("停止监视时发生错误: {0}", ex.Message),
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnFileProcessStatusChanged(object sender, FileProcessEventArgs e)
        {
            switch (e.Status)
            {
                case FileProcessStatus.Detected:
                    AddLogMessage(string.Format("检测到新文件: {0}", e.FileName ?? e.Message));
                    break;

                case FileProcessStatus.Moving:
                    AddLogMessage(string.Format("正在移动文件: {0}", e.FileName));
                    break;

                case FileProcessStatus.Moved:
                    AddLogMessage(string.Format("已将文件移动到目标文件夹: {0}", e.FileName));
                    break;

                case FileProcessStatus.Retrying:
                    AddLogMessage(string.Format("重试中: {0}", e.Message));
                    break;

                case FileProcessStatus.Failed:
                    AddLogMessage(string.Format("错误: {0}", e.Message));
                    if (e.Error != null)
                    {
                        AddLogMessage(string.Format("详细信息: {0}", e.Error.Message));
                    }
                    break;
            }
        }

        private void UpdateStartButtonState()
        {
            btnStartStop.Enabled = !string.IsNullOrEmpty(txtWatchFolder.Text) &&
                                 !string.IsNullOrEmpty(txtTargetFolder.Text);
        }

        private void AddLogMessage(string message)
        {
            if (lstLog.InvokeRequired)
            {
                lstLog.Invoke(new Action<string>(AddLogMessage), message);
                return;
            }

            string timeStamp = DateTime.Now.ToString("HH:mm:ss");
            lstLog.Items.Insert(0, string.Format("[{0}] {1}", timeStamp, message));

            // 限制日志条数
            while (lstLog.Items.Count > 1000)
            {
                lstLog.Items.RemoveAt(lstLog.Items.Count - 1);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (isWatching)
            {
                StopWatching();
            }

            if (watcher != null)
            {
                watcher.Dispose();
            }
        }

        private void txtWatchFolder_TextChanged(object sender, EventArgs e)
        {
            UpdateStartButtonState();
        }

        private void txtTargetFolder_TextChanged(object sender, EventArgs e)
        {
            UpdateStartButtonState();
        }

        private void btnClearLog_Click(object sender, EventArgs e)
        {
            lstLog.Items.Clear();
        }
    }
} 