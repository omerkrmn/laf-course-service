namespace Laf.CourseService.Models;

public class VideoResponseDto
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public double DurationSeconds { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Resolution { get; set; } = string.Empty;
    public string VideoCodec { get; set; } = string.Empty;
    public string AudioCodec { get; set; } = string.Empty;
    public long Bitrate { get; set; }
    public double FrameRate { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public VideoProcessingStatus Status { get; set; }
    public string StatusText => Status.ToString();
    public string? ErrorMessage { get; set; }
    public string? ManifestUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public static VideoResponseDto FromEntity(VideoMetadata entity, string baseUrl = "")
    {
        var cleanBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "" : baseUrl.TrimEnd('/');
        var manifestUrl = entity.Status == VideoProcessingStatus.Completed
            ? $"{cleanBaseUrl}/api/videos/{entity.Id}/manifest.m3u8"
            : null;
        var thumbnailUrl = (entity.Status == VideoProcessingStatus.Completed && !string.IsNullOrEmpty(entity.ThumbnailPath))
            ? $"{cleanBaseUrl}/api/videos/{entity.Id}/thumbnail"
            : null;

        return new VideoResponseDto
        {
            Id = entity.Id,
            OriginalFileName = entity.OriginalFileName,
            ContentType = entity.ContentType,
            FileSizeBytes = entity.FileSizeBytes,
            DurationSeconds = entity.DurationSeconds,
            Width = entity.Width,
            Height = entity.Height,
            Resolution = entity.Resolution,
            VideoCodec = entity.VideoCodec,
            AudioCodec = entity.AudioCodec,
            Bitrate = entity.Bitrate,
            FrameRate = entity.FrameRate,
            UploadedBy = entity.UploadedBy,
            Status = entity.Status,
            ErrorMessage = entity.ErrorMessage,
            ManifestUrl = manifestUrl,
            ThumbnailUrl = thumbnailUrl,
            CreatedAt = entity.CreatedAt,
            CompletedAt = entity.CompletedAt
        };
    }
}
