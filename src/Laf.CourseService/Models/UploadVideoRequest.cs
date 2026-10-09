namespace Laf.CourseService.Models;

public class UploadVideoRequest
{
    public IFormFile VideoFile { get; set; } = null!;
    public string? UploadedBy { get; set; }
}

public class VideoUploadAcceptedResponse
{
    public Guid VideoId { get; set; }
    public string Message { get; set; } = "Video uploaded and queued for HLS transcoding.";
    public VideoProcessingStatus Status { get; set; } = VideoProcessingStatus.Pending;
    public string StatusUrl { get; set; } = string.Empty;
}
