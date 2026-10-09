namespace Laf.CourseService.Models;

public enum VideoProcessingStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}

public class VideoMetadata
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public double DurationSeconds { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Resolution { get; set; } = string.Empty; // e.g. "1920x1080 (1080p)"
    public string VideoCodec { get; set; } = string.Empty; // e.g. "h264"
    public string AudioCodec { get; set; } = string.Empty; // e.g. "aac"
    public long Bitrate { get; set; }
    public double FrameRate { get; set; } // e.g. 29.97, 60.0
    public string UploadedBy { get; set; } = string.Empty;
    public VideoProcessingStatus Status { get; set; } = VideoProcessingStatus.Pending;
    public string? ErrorMessage { get; set; }
    public string HlsMasterPlaylistPath { get; set; } = string.Empty;
    public string ThumbnailPath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
