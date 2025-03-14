using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text.RegularExpressions;

namespace FileWatcherLib
{
    /// <summary>
    /// 文件监视器类，用于监控指定目录下的文件变化
    /// 实现 IDisposable 接口以确保资源正确释放
    /// </summary>
    public class FileWatcher : IDisposable
    {
        private FileSystemWatcher _watcher;                // 系统文件监视器实例
        private HashSet<string> _allowedExtensions;        // 允许监视的文件扩展名集合
        private bool _watchAllTypes;                       // 是否监视所有类型的文件
        private bool _isWatching;                         // 当前是否正在监视
        private bool _includeSubdirectories;              // 是否监视子目录
        private Regex fileNamePattern;                      // 文件名匹配模式

        // 文件变化事件委托
        public event EventHandler<FileChangeEventArgs> FileChanged;    // 文件修改事件
        public event EventHandler<FileChangeEventArgs> FileCreated;    // 文件创建事件
        public event EventHandler<FileChangeEventArgs> FileDeleted;    // 文件删除事件
        public event EventHandler<FileChangeEventArgs> FileRenamed;    // 文件重命名事件

        // 属性
        public string WatchPath { get; private set; }                 // 被监视的文件夹路径
        public bool IsWatching { get { return _isWatching; } }       // 是否正在监视文件
        public bool IncludeSubdirectories                            // 是否监视子目录
        {
            get { return _includeSubdirectories; }
            set 
            { 
                _includeSubdirectories = value;
                if (_watcher != null)
                {
                    _watcher.IncludeSubdirectories = value;
                }
            }
        }

        /// <summary>
        /// 是否正在监视所有文件（无类型和名称限制）
        /// </summary>
        public bool IsWatchingAllFiles
        {
            get { return _watchAllTypes && fileNamePattern == null; }
        }

        /// <summary>
        /// 是否设置了文件类型过滤
        /// </summary>
        public bool HasFileTypeFilter
        {
            get { return !_watchAllTypes; }
        }

        /// <summary>
        /// 是否设置了文件名模式
        /// </summary>
        public bool HasFileNamePattern
        {
            get { return fileNamePattern != null; }
        }

        /// <summary>
        /// 获取允许的文件扩展名列表
        /// </summary>
        public string[] AllowedExtensions
        {
            get { return _allowedExtensions.ToArray(); }
        }

        /// <summary>
        /// 构造函数：初始化监视器
        /// </summary>
        public FileWatcher()
        {
            _allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _watchAllTypes = true;
            _isWatching = false;
            _includeSubdirectories = false;
        }

        /// <summary>
        /// 设置要监视的文件夹路径
        /// </summary>
        /// <param name="path">文件夹路径</param>
        public void SetWatchPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException("path");

            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException("Specified directory does not exist.");

            WatchPath = path;
        }

        /// <summary>
        /// 设置要监视的文件类型
        /// </summary>
        /// <param name="extensions">文件扩展名数组，如：.txt, .doc 等</param>
        public void SetFileTypes(params string[] extensions)
        {
            _allowedExtensions.Clear();
            foreach (string ext in extensions)
            {
                _allowedExtensions.Add(ext.StartsWith(".") ? ext.ToLower() : "." + ext.ToLower());
            }
        }

        /// <summary>
        /// 设置文件名匹配模式
        /// </summary>
        /// <param name="pattern">正则表达式模式</param>
        public void SetFileNamePattern(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                fileNamePattern = null;
            }
            else
            {
                fileNamePattern = new Regex(pattern, RegexOptions.Compiled);
            }
        }

        /// <summary>
        /// 开始监视文件变化
        /// </summary>
        public void StartWatch()
        {
            if (_isWatching)
                return;

            if (string.IsNullOrEmpty(WatchPath))
                throw new InvalidOperationException("Watch path is not set.");

            // 初始化系统文件监视器
            _watcher = new FileSystemWatcher(WatchPath);
            _watcher.IncludeSubdirectories = _includeSubdirectories;  // 设置是否监视子目录
            _watcher.EnableRaisingEvents = true;           // 启用事件触发

            // 注册文件系统事件处理器
            _watcher.Created += OnFileCreated;
            _watcher.Changed += OnFileChanged;
            _watcher.Deleted += OnFileDeleted;
            _watcher.Renamed += OnFileRenamed;

            _isWatching = true;
        }

        /// <summary>
        /// 停止监视文件变化
        /// </summary>
        public void StopWatch()
        {
            if (!_isWatching)
                return;

            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                // 注销事件处理器
                _watcher.Created -= OnFileCreated;
                _watcher.Changed -= OnFileChanged;
                _watcher.Deleted -= OnFileDeleted;
                _watcher.Renamed -= OnFileRenamed;
                _watcher.Dispose();
                _watcher = null;
            }

            _isWatching = false;
        }

        /// <summary>
        /// 检查文件是否符合所有验证条件（文件类型和文件名模式）
        /// </summary>
        /// <param name="filePath">要检查的文件路径</param>
        /// <returns>如果文件符合所有验证条件则返回 true，否则返回 false</returns>
        public bool IsFileValid(string filePath)
        {
            try
            {
                // 如果没有设置任何限制（文件类型和文件名模式），则接受所有文件
                if (_watchAllTypes && fileNamePattern == null)
                    return true;

                // 检查文件类型
                bool typeAllowed = IsFileTypeAllowed(filePath);
                if (!typeAllowed)
                    return false;

                // 如果设置了文件名模式，则检查文件名是否匹配
                if (fileNamePattern != null)
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    return fileNamePattern.IsMatch(fileName);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查文件类型是否在监视范围内
        /// </summary>
        private bool IsFileTypeAllowed(string filePath)
        {
            if (_watchAllTypes)
                return true;

            string extension = Path.GetExtension(filePath);
            return _allowedExtensions.Contains(extension);
        }

        /// <summary>
        /// 处理文件创建事件
        /// </summary>
        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            if (!IsFileValid(e.FullPath))
                return;

            // 使用线程池处理事件，避免阻塞文件系统监视器
            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (FileCreated != null)
                    FileCreated(this, new FileChangeEventArgs(e.FullPath, FileChangeType.Created));
            });
        }

        /// <summary>
        /// 处理文件修改事件
        /// </summary>
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (!IsFileValid(e.FullPath))
                return;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (FileChanged != null)
                    FileChanged(this, new FileChangeEventArgs(e.FullPath, FileChangeType.Modified));
            });
        }

        /// <summary>
        /// 处理文件删除事件
        /// </summary>
        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            if (!IsFileValid(e.FullPath))
                return;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (FileDeleted != null)
                    FileDeleted(this, new FileChangeEventArgs(e.FullPath, FileChangeType.Deleted));
            });
        }

        /// <summary>
        /// 处理文件重命名事件
        /// </summary>
        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (!IsFileValid(e.FullPath))
                return;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (FileRenamed != null)
                    FileRenamed(this, new FileChangeEventArgs(e.FullPath, FileChangeType.Renamed, e.OldFullPath));
            });
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            StopWatch();
        }
    }
} 