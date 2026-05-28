using System.IO;

namespace SimpleVideoPlayer.Models;

public class VideoFolder
{
    public string FolderPath { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; }
    public int VideoCount { get; set; }
    public List<VideoFile> Videos { get; set; } = new();
    public List<VideoFolder> SubFolders { get; set; } = new();

    public string DisplayName => string.IsNullOrEmpty(FolderName)
        ? new DirectoryInfo(FolderPath).Name
        : FolderName;
}