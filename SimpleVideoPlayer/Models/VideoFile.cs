using System.ComponentModel;
using System.IO;

namespace SimpleVideoPlayer.Models;

public class VideoFile : INotifyPropertyChanged
{
    private string? _thumbnailPath;

    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    
    public string? ThumbnailPath
    {
        get => _thumbnailPath;
        set
        {
            if (_thumbnailPath != value)
            {
                _thumbnailPath = value;
                OnPropertyChanged(nameof(ThumbnailPath));
            }
        }
    }
    
    private TimeSpan _duration;
    public TimeSpan Duration
    {
        get => _duration;
        set
        {
            if (_duration != value)
            {
                _duration = value;
                OnPropertyChanged(nameof(Duration));
                OnPropertyChanged(nameof(DurationText));
            }
        }
    }
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }

    public string DisplayName => Path.GetFileNameWithoutExtension(FileName);
    public string DurationText => Duration.TotalHours >= 1
        ? $"{(int)Duration.TotalHours}:{Duration.Minutes:D2}:{Duration.Seconds:D2}"
        : $"{Duration.Minutes:D2}:{Duration.Seconds:D2}";
    public string FileSizeText
    {
        get
        {
            double size = FileSize;
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int unitIndex = 0;
            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }
            return $"{size:F1} {units[unitIndex]}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}