using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;

namespace TestProgram
{
    /// <summary>
    /// 文件处理状态枚举
    /// </summary>
    public enum FileProcessStatus
    {
        Detected,       // 检测到新文件
        Moving,         // 正在移动
        Moved,         // 移动成功
        Busy,          // 文件被占用
        Failed,        // 处理失败
        Retrying       // 正在重试
    }

    /// <summary>
    /// 文件处理事件参数类
    /// </summary>
    public class FileProcessEventArgs : EventArgs
    {
        public string FileName { get; private set; }
        public string SourcePath { get; private set; }
        public string TargetPath { get; private set; }
        public FileProcessStatus Status { get; private set; }
        public string Message { get; private set; }
        public Exception Error { get; private set; }

        public FileProcessEventArgs(string fileName, string sourcePath, string targetPath, 
            FileProcessStatus status, string message, Exception error)
        {
            FileName = fileName;
            SourcePath = sourcePath;
            TargetPath = targetPath;
            Status = status;
            Message = message;
            Error = error;
        }
    }

    /// <summary>
    /// 文件处理器类，负责处理文件的移动和队列管理
    /// </summary>
    public class FileProcessor : IDisposable
    {
        private readonly string targetPath;
        private const int MAX_RETRIES = 10;    // 最大重试次数
        private const int RETRY_DELAY = 100;   // 每次重试间隔（毫秒）

        // 文件处理队列
        private readonly Queue<FileInfo> fileQueue = new Queue<FileInfo>();
        private readonly object queueLock = new object();
        private readonly AutoResetEvent fileAddedEvent = new AutoResetEvent(false);
        private volatile bool isProcessing = true;
        private readonly Thread processThread;
        private readonly FileWatcher watcher;  // 文件监视器实例

        // 文件处理事件
        public event EventHandler<FileProcessEventArgs> FileProcessStatusChanged;

        /// <summary>
        /// 用于存储文件信息的内部类
        /// </summary>
        private class FileInfo
        {
            public string SourcePath { get; set; }
            public string TargetPath { get; set; }
            public string FileName { get; set; }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="targetPath">目标文件夹路径</param>
        /// <param name="watcher">文件监视器实例</param>
        /// <param name="scanExistingFiles">是否扫描现有文件</param>
        public FileProcessor(string targetPath, FileWatcher watcher, bool scanExistingFiles = false)
        {
            this.targetPath = targetPath;
            this.watcher = watcher;
            
            // 创建并启动处理线程
            processThread = new Thread(ProcessFileQueue)
            {
                IsBackground = true
            };
            processThread.Start();

            // 如果需要扫描现有文件，立即执行扫描
            if (scanExistingFiles)
            {
                ScanExistingFiles();
            }
        }

        /// <summary>
        /// 扫描并处理现有文件
        /// </summary>
        private void ScanExistingFiles()
        {
            try
            {
                OnFileProcessStatusChanged(null, null, null, FileProcessStatus.Detected, 
                    "正在扫描现有文件...", null);

                if (string.IsNullOrEmpty(watcher.WatchPath) || !Directory.Exists(watcher.WatchPath))
                {
                    OnFileProcessStatusChanged(null, null, null, FileProcessStatus.Failed, 
                        "监视路径无效或不存在", null);
                    return;
                }

                // 获取所有文件并筛选符合条件的文件
                var files = Directory.GetFiles(watcher.WatchPath);
                int matchCount = 0;

                foreach (string filePath in files)
                {
                    if (watcher.IsFileValid(filePath))
                    {
                        EnqueueFile(filePath);
                        matchCount++;
                    }
                }

                OnFileProcessStatusChanged(null, null, null, FileProcessStatus.Detected, 
                    string.Format("找到 {0} 个符合条件的现有文件", matchCount), null);
            }
            catch (Exception ex)
            {
                OnFileProcessStatusChanged(null, null, null, FileProcessStatus.Failed, 
                    "扫描现有文件时发生错误", ex);
            }
        }

        /// <summary>
        /// 触发文件处理状态改变事件
        /// </summary>
        private void OnFileProcessStatusChanged(string fileName, string sourcePath, string targetPath, 
            FileProcessStatus status, string message, Exception error)
        {
            if (FileProcessStatusChanged != null)
            {
                FileProcessStatusChanged(this, 
                    new FileProcessEventArgs(fileName, sourcePath, targetPath, status, message, error));
            }
        }

        /// <summary>
        /// 添加文件到处理队列
        /// </summary>
        /// <param name="sourcePath">源文件路径</param>
        public void EnqueueFile(string sourcePath)
        {
            try
            {
                string fileName = Path.GetFileName(sourcePath);
                string targetFilePath = Path.Combine(targetPath, fileName);

                OnFileProcessStatusChanged(fileName, sourcePath, targetFilePath, FileProcessStatus.Detected, null, null);
                
                var fileInfo = new FileInfo 
                { 
                    SourcePath = sourcePath,
                    TargetPath = targetFilePath,
                    FileName = fileName
                };

                lock (queueLock)
                {
                    fileQueue.Enqueue(fileInfo);
                }
                
                fileAddedEvent.Set();
            }
            catch (Exception ex)
            {
                OnFileProcessStatusChanged(Path.GetFileName(sourcePath), sourcePath, null, 
                    FileProcessStatus.Failed, "添加文件到队列时发生错误", ex);
            }
        }

        /// <summary>
        /// 处理文件队列的线程方法
        /// </summary>
        private void ProcessFileQueue()
        {
            while (isProcessing)
            {
                fileAddedEvent.WaitOne();

                while (true)
                {
                    FileInfo fileInfo = null;
                    lock (queueLock)
                    {
                        if (fileQueue.Count > 0)
                        {
                            fileInfo = fileQueue.Dequeue();
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (fileInfo != null)
                    {
                        try
                        {
                            OnFileProcessStatusChanged(fileInfo.FileName, fileInfo.SourcePath, 
                                fileInfo.TargetPath, FileProcessStatus.Moving, null, null);

                            if (TryMoveFile(fileInfo.SourcePath, fileInfo.TargetPath))
                            {
                                OnFileProcessStatusChanged(fileInfo.FileName, fileInfo.SourcePath, 
                                    fileInfo.TargetPath, FileProcessStatus.Moved, null, null);
                            }
                            else
                            {
                                OnFileProcessStatusChanged(fileInfo.FileName, fileInfo.SourcePath, 
                                    fileInfo.TargetPath, FileProcessStatus.Failed, 
                                    string.Format("无法移动文件，已超过最大重试次数({0}次)", MAX_RETRIES), null);
                            }
                        }
                        catch (Exception ex)
                        {
                            OnFileProcessStatusChanged(fileInfo.FileName, fileInfo.SourcePath, 
                                fileInfo.TargetPath, FileProcessStatus.Failed, 
                                "移动文件时发生错误", ex);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 尝试将文件移动到目标位置，如果文件被占用则进行重试
        /// </summary>
        /// <param name="sourcePath">源文件路径</param>
        /// <param name="targetPath">目标文件路径</param>
        /// <returns>是否成功移动文件</returns>
        private bool TryMoveFile(string sourcePath, string targetPath)
        {
            int retryCount = 0;
            string fileName = Path.GetFileName(sourcePath);
            
            while (retryCount < MAX_RETRIES)
            {
                try
                {
                    using (FileStream fs = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        fs.Close();
                    }

                    File.Move(sourcePath, targetPath);
                    return true;
                }
                catch (IOException)
                {
                    retryCount++;
                    if (retryCount < MAX_RETRIES)
                    {
                        OnFileProcessStatusChanged(fileName, sourcePath, targetPath, 
                            FileProcessStatus.Retrying, 
                            string.Format("文件正在被占用，{0}毫秒后进行第{1}次重试", RETRY_DELAY, retryCount + 1), null);
                        Thread.Sleep(RETRY_DELAY);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            isProcessing = false;
            fileAddedEvent.Set(); // 确保处理线程能够退出
            fileAddedEvent.Close();
        }
    }
} 