using System;

namespace FileWatcherLib
{
    /// <summary>
    /// 文件变化类型枚举
    /// </summary>
    public enum FileChangeType
    {
        Created,    // 文件被创建
        Modified,   // 文件被修改
        Deleted,    // 文件被删除
        Renamed     // 文件被重命名
    }

    /// <summary>
    /// 文件变化事件参数类
    /// 用于在文件发生变化时传递相关信息
    /// </summary>
    public class FileChangeEventArgs : EventArgs
    {
        /// <summary>
        /// 发生变化的文件路径
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// 文件变化类型
        /// </summary>
        public FileChangeType ChangeType { get; private set; }

        /// <summary>
        /// 文件的原始路径（仅在重命名事件中使用）
        /// </summary>
        public string OldFilePath { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="filePath">发生变化的文件路径</param>
        /// <param name="changeType">变化类型</param>
        /// <param name="oldFilePath">原始文件路径（可选，用于重命名事件）</param>
        public FileChangeEventArgs(string filePath, FileChangeType changeType, string oldFilePath = null)
        {
            FilePath = filePath;
            ChangeType = changeType;
            OldFilePath = oldFilePath;
        }
    }
} 