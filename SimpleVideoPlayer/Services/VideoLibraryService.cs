using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using LibVLCSharp.Shared;
using SimpleVideoPlayer.Models;

namespace SimpleVideoPlayer.Services;

public class VideoLibraryService : IDisposable
{
    private readonly string[] _videoExtensions = { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg", ".3gp", ".ts", ".m2ts" };
    private readonly string _thumbnailFolder;
    private readonly string _ffmpegPath;
    private LibVLC? _libVLC;
    private bool _disposed;

    public VideoLibraryService()
    {
        _thumbnailFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SimpleVideoPlayer",
            "thumbnails");
        if (!Directory.Exists(_thumbnailFolder))
        {
            Directory.CreateDirectory(_thumbnailFolder);
        }

        _ffmpegPath = FindFFmpegPath();
    }

    private string FindFFmpegPath()
    {
        // 优先使用打包的 FFmpeg（放在程序目录的 ffmpeg 文件夹中）
        var bundledPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "ffmpeg.exe");
        if (File.Exists(bundledPath))
            return bundledPath;

        // 其次检查程序根目录
        var rootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(rootPath))
            return rootPath;

        // 最后检查系统 PATH
        return "ffmpeg.exe";
    }

    private void EnsureLibVLC()
    {
        if (_libVLC == null)
        {
            Core.Initialize();
            _libVLC = new LibVLC();
        }
    }

    public async Task<ObservableCollection<VideoFolder>> ScanFolderAsync(string rootPath, IProgress<string>? progress = null)
    {
        var folders = new ObservableCollection<VideoFolder>();
        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
        {
            return folders;
        }

        // 先快速扫描文件结构并创建 VideoFile（不解析时长）以加快启动速度
        await Task.Run(() =>
        {
            ScanFolderRecursive(rootPath, folders, progress);
        });

        return folders;
    }

    private void ScanFolderRecursive(string path, ObservableCollection<VideoFolder> folders, IProgress<string>? progress)
    {
        try
        {
            progress?.Report(path);

            var dirInfo = new DirectoryInfo(path);
            var folder = new VideoFolder
            {
                FolderPath = path,
                FolderName = dirInfo.Name
            };

            foreach (var file in dirInfo.GetFiles())
            {
                if (_videoExtensions.Contains(file.Extension.ToLowerInvariant()))
                {
                    var videoFile = CreateVideoFile(file.FullName);
                    if (videoFile != null)
                    {
                        folder.Videos.Add(videoFile);
                    }
                }
            }

            foreach (var subDir in dirInfo.GetDirectories())
            {
                var subFolders = new ObservableCollection<VideoFolder>();
                ScanFolderRecursive(subDir.FullName, subFolders, progress);
                foreach (var sub in subFolders)
                {
                    folder.SubFolders.Add(sub);
                }
            }

            folder.VideoCount = folder.Videos.Count + folder.SubFolders.Sum(f => f.VideoCount);

            if (folder.VideoCount > 0 || folder.Videos.Count > 0)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    folders.Add(folder);
                });
            }
        }
        catch { }
    }

    private VideoFile? CreateVideoFile(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            var video = new VideoFile
            {
                FilePath = filePath,
                FileName = fileInfo.Name,
                FolderPath = fileInfo.DirectoryName ?? string.Empty,
                FileSize = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime
            };
            return video;
        }
        catch
        {
            return null;
        }
    }

    private IEnumerable<VideoFile> GetAllVideos(VideoFolder folder)
    {
        foreach (var v in folder.Videos)
            yield return v;

        foreach (var sub in folder.SubFolders)
        {
            foreach (var sv in GetAllVideos(sub))
                yield return sv;
        }
    }

    private TimeSpan GetVideoDuration(string videoPath)
    {
        try
        {
            EnsureLibVLC();
            if (_libVLC == null) return TimeSpan.Zero;

            using var media = new Media(_libVLC, videoPath, FromType.FromPath);
            media.Parse(MediaParseOptions.ParseLocal);
            var duration = media.Duration;

            if (duration > 0)
            {
                return TimeSpan.FromMilliseconds(duration);
            }
            // 如果 LibVLC 未能返回时长，使用 ffmpeg 输出作为后备解析
            var ffmpegFallback = GetDurationWithFFmpeg(videoPath);
            if (ffmpegFallback > TimeSpan.Zero)
                return ffmpegFallback;
        }
        catch { }
        return TimeSpan.Zero;
    }

    private TimeSpan GetDurationWithFFmpeg(string videoPath)
    {
        try
        {
            if (!File.Exists(_ffmpegPath)) return TimeSpan.Zero;

            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = _ffmpegPath;
            process.StartInfo.Arguments = $"-i \"{videoPath}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;

            process.Start();
            // 读取全部 stderr（ffmpeg 会打印 media info then exit with code 1)
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(3000);

            // 寻找类似 "Duration: 00:03:45.12," 的片段
            var marker = "Duration:";
            var idx = stderr.IndexOf(marker);
            if (idx >= 0)
            {
                var substr = stderr.Substring(idx + marker.Length);
                // 提取到逗号之前
                var comma = substr.IndexOf(',');
                if (comma > 0)
                {
                    var timeStr = substr.Substring(0, comma).Trim();
                    // 可能格式为 00:03:45.12
                    if (TimeSpan.TryParse(timeStr, out var ts))
                    {
                        return ts;
                    }
                    else
                    {
                        // 有时包含毫秒用小数点，TryParse 依然适用; as fallback try manual parse
                        var parts = timeStr.Split(':');
                        if (parts.Length == 3)
                        {
                            if (int.TryParse(parts[0], out var h) &&
                                int.TryParse(parts[1], out var m))
                            {
                                var secPart = parts[2];
                                if (double.TryParse(secPart, out var secondsDouble))
                                {
                                    var s = (int)Math.Floor(secondsDouble);
                                    var ms = (int)((secondsDouble - s) * 1000);
                                    return new TimeSpan(0, h, m, s, ms);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch { }
        return TimeSpan.Zero;
    }

    public async Task<string?> GenerateThumbnailAsync(string videoPath, CancellationToken cancellationToken = default, int desiredWidth = 480)
    {
        return await Task.Run(() =>
        {
            try
            {
                var thumbnailPath = GetThumbnailPath(videoPath);
                if (File.Exists(thumbnailPath))
                {
                    return thumbnailPath;
                }

                if (TryExtractThumbnailWithFFmpeg(videoPath, thumbnailPath, desiredWidth))
                {
                    return thumbnailPath;
                }
                
                return null;
            }
            catch { }
            return null;
        }, cancellationToken);
    }

    private bool TryExtractThumbnailWithFFmpeg(string videoPath, string outputPath, int width)
    {
        try
        {
            if (!File.Exists(_ffmpegPath))
            {
                System.Diagnostics.Debug.WriteLine($"FFmpeg not found at: {_ffmpegPath}");
                return false;
            }

            var arguments = $"-ss 00:00:05 -i \"{videoPath}\" -skip_frame nokey -vf \"select=key(n),scale={width}:-1\" -vframes 1 -q:v 2 -y \"{outputPath}\"";
            
            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = _ffmpegPath;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            
            process.Start();
            process.WaitForExit(10000);
            
            if (process.ExitCode == 0 && File.Exists(outputPath))
            {
                System.Diagnostics.Debug.WriteLine($"Thumbnail created: {outputPath}");
                return true;
            }

            System.Diagnostics.Debug.WriteLine($"FFmpeg exit code: {process.ExitCode}");
            
            arguments = $"-ss 00:00:10 -i \"{videoPath}\" -vf \"scale={width}:-1\" -vframes 1 -q:v 2 -y \"{outputPath}\"";
            
            using var process2 = new System.Diagnostics.Process();
            process2.StartInfo.FileName = _ffmpegPath;
            process2.StartInfo.Arguments = arguments;
            process2.StartInfo.UseShellExecute = false;
            process2.StartInfo.CreateNoWindow = true;
            process2.StartInfo.RedirectStandardError = true;
            process2.StartInfo.RedirectStandardOutput = true;
            
            process2.Start();
            process2.WaitForExit(10000);
            
            var success = process2.ExitCode == 0 && File.Exists(outputPath);
            if (success)
                System.Diagnostics.Debug.WriteLine($"Thumbnail created (fallback): {outputPath}");
            else
                System.Diagnostics.Debug.WriteLine($"FFmpeg fallback exit code: {process2.ExitCode}");
            
            return success;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FFmpeg error: {ex.Message}");
            return false;
        }
    }

    public async Task GenerateThumbnailsForVideosAsync(IEnumerable<VideoFile> videos, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        foreach (var video in videos)
        {
            if (cancellationToken.IsCancellationRequested) break;
            
            var thumbnail = await GenerateThumbnailAsync(video.FilePath, cancellationToken);
            if (!string.IsNullOrEmpty(thumbnail) && File.Exists(thumbnail))
            {
                video.ThumbnailPath = thumbnail;
            }
        }
    }

    public async Task<string?> GetVideoDurationAsync(string videoPath)
    {
        return await Task.Run(() =>
        {
            try
            {
                EnsureLibVLC();
                if (_libVLC == null) return null;

                using var media = new Media(_libVLC, videoPath, FromType.FromPath);
                media.Parse(MediaParseOptions.ParseLocal);
                var duration = media.Duration;

                if (duration > 0)
                {
                    var ts = TimeSpan.FromMilliseconds(duration);
                    return ts.TotalHours >= 1
                        ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                        : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
                }
                // ffmpeg fallback returns TimeSpan; convert to formatted string
                var ffmpegTs = GetDurationWithFFmpeg(videoPath);
                if (ffmpegTs > TimeSpan.Zero)
                {
                    var ts2 = ffmpegTs;
                    return ts2.TotalHours >= 1
                        ? $"{(int)ts2.TotalHours}:{ts2.Minutes:D2}:{ts2.Seconds:D2}"
                        : $"{ts2.Minutes:D2}:{ts2.Seconds:D2}";
                }
            }
            catch { }
            return null;
        });
    }

    private string GetThumbnailPath(string videoPath)
    {
        var hash = videoPath.GetHashCode().ToString("X8");
        return Path.Combine(_thumbnailFolder, $"{hash}.jpg");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _libVLC?.Dispose();
            _disposed = true;
        }
    }
}
