namespace Laf.CourseService.Services;

public class MediaStorageOptions
{
    public const string SectionName = "MediaStorage";

    public string BasePath { get; set; } = "Storage";
    public string TempUploadsFolder { get; set; } = "TempUploads";
    public string ProcessedHlsFolder { get; set; } = "ProcessedHls";
    public long MaxFileSizeBytes { get; set; } = 500 * 1024 * 1024; // 500 MB
    public string FfmpegPath { get; set; } = "ffmpeg";
    public string FfprobePath { get; set; } = "ffprobe";
    public string[] AllowedExtensions { get; set; } = [".mp4", ".mov", ".avi", ".mkv", ".webm"];
}
