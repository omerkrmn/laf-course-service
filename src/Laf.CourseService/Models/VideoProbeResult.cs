namespace Laf.CourseService.Models;

public class VideoProbeResult
{
    public double DurationSeconds { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Resolution { get; set; } = string.Empty;
    public string VideoCodec { get; set; } = string.Empty;
    public string AudioCodec { get; set; } = string.Empty;
    public long Bitrate { get; set; }
    public double FrameRate { get; set; }
    public long FileSizeBytes { get; set; }
}
