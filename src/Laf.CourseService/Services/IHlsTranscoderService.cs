using Laf.CourseService.Models;

namespace Laf.CourseService.Services;

public interface IHlsTranscoderService
{
    Task<VideoProbeResult> ProbeVideoAsync(string inputFilePath, CancellationToken cancellationToken = default);
    Task<string> GenerateThumbnailAsync(string inputFilePath, string outputDirectory, CancellationToken cancellationToken = default);
    Task<string> TranscodeToHlsAsync(string inputFilePath, string outputDirectory, CancellationToken cancellationToken = default);
}
