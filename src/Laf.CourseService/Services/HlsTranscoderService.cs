using System.Globalization;
using System.Text.Json;
using CliWrap;
using CliWrap.Buffered;
using Laf.CourseService.Models;
using Microsoft.Extensions.Options;

namespace Laf.CourseService.Services;

public class HlsTranscoderService : IHlsTranscoderService
{
    private readonly MediaStorageOptions _options;
    private readonly ILogger<HlsTranscoderService> _logger;

    public HlsTranscoderService(IOptions<MediaStorageOptions> options, ILogger<HlsTranscoderService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<VideoProbeResult> ProbeVideoAsync(string inputFilePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputFilePath))
            throw new FileNotFoundException($"Input file not found for probe: {inputFilePath}");

        var ffprobeExe = ResolveExecutable(_options.FfprobePath, "ffprobe");

        _logger.LogInformation("Probing video metadata for {File} using {Ffprobe}", inputFilePath, ffprobeExe);

        BufferedCommandResult result;
        try
        {
            result = await Cli.Wrap(ffprobeExe)
                .WithArguments([
                    "-v", "quiet",
                    "-print_format", "json",
                    "-show_format",
                    "-show_streams",
                    inputFilePath
                ])
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run ffprobe executable '{Ffprobe}'", ffprobeExe);
            throw new InvalidOperationException($"Failed to execute ffprobe ('{ffprobeExe}'). Ensure FFmpeg/FFprobe is installed and configured in appsettings.json or PATH.", ex);
        }

        if (result.ExitCode != 0)
        {
            var err = result.StandardError;
            _logger.LogError("ffprobe failed with exit code {ExitCode}. Error: {Error}", result.ExitCode, err);
            throw new InvalidOperationException($"FFprobe failed to analyze video. Exit code: {result.ExitCode}. Output: {err}");
        }

        return ParseProbeJson(result.StandardOutput, inputFilePath);
    }

    public async Task<string> GenerateThumbnailAsync(string inputFilePath, string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var thumbnailPath = Path.Combine(outputDirectory, "thumbnail.jpg");
        var ffmpegExe = ResolveExecutable(_options.FfmpegPath, "ffmpeg");

        _logger.LogInformation("Generating thumbnail for {File} to {ThumbnailPath}", inputFilePath, thumbnailPath);

        BufferedCommandResult result;
        try
        {
            result = await Cli.Wrap(ffmpegExe)
                .WithArguments([
                    "-y",
                    "-ss", "00:00:01",
                    "-i", inputFilePath,
                    "-vframes", "1",
                    "-q:v", "2",
                    thumbnailPath
                ])
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run ffmpeg executable '{Ffmpeg}' for thumbnail extraction", ffmpegExe);
            throw new InvalidOperationException($"Failed to execute ffmpeg ('{ffmpegExe}'). Ensure FFmpeg is installed and configured in appsettings.json or PATH.", ex);
        }

        if (result.ExitCode != 0)
        {
            _logger.LogWarning("Thumbnail extraction at 1s failed, retrying at 0s. Exit code: {ExitCode}", result.ExitCode);
            result = await Cli.Wrap(ffmpegExe)
                .WithArguments([
                    "-y",
                    "-ss", "00:00:00",
                    "-i", inputFilePath,
                    "-vframes", "1",
                    "-q:v", "2",
                    thumbnailPath
                ])
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken);

            if (result.ExitCode != 0)
            {
                _logger.LogError("Thumbnail extraction failed completely: {Error}", result.StandardError);
                throw new InvalidOperationException($"FFmpeg failed to extract thumbnail: {result.StandardError}");
            }
        }

        return thumbnailPath;
    }

    public async Task<string> TranscodeToHlsAsync(string inputFilePath, string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var masterPlaylistPath = Path.Combine(outputDirectory, "master.m3u8");
        var segmentPattern = Path.Combine(outputDirectory, "segment_%03d.ts");
        var ffmpegExe = ResolveExecutable(_options.FfmpegPath, "ffmpeg");

        _logger.LogInformation("Transcoding {InputFile} to HLS in {OutputDir}", inputFilePath, outputDirectory);

        BufferedCommandResult result;
        try
        {
            result = await Cli.Wrap(ffmpegExe)
                .WithArguments([
                    "-y",
                    "-i", inputFilePath,
                    "-codec:v", "libx264",
                    "-crf", "22",
                    "-preset", "fast",
                    "-codec:a", "aac",
                    "-b:a", "128k",
                    "-hls_time", "6",
                    "-hls_playlist_type", "vod",
                    "-hls_segment_filename", segmentPattern,
                    masterPlaylistPath
                ])
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run ffmpeg executable '{Ffmpeg}' for HLS transcoding", ffmpegExe);
            throw new InvalidOperationException($"Failed to execute ffmpeg ('{ffmpegExe}'). Ensure FFmpeg is installed and configured in appsettings.json or PATH.", ex);
        }

        if (result.ExitCode != 0)
        {
            _logger.LogError("HLS transcoding failed with exit code {ExitCode}. Error: {Error}", result.ExitCode, result.StandardError);
            throw new InvalidOperationException($"FFmpeg failed HLS transcoding: {result.StandardError}");
        }

        return masterPlaylistPath;
    }

    private static string ResolveExecutable(string configuredPath, string binaryName)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && (Path.IsPathRooted(configuredPath) || configuredPath.Contains('/') || configuredPath.Contains('\\')) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        // Check if executable exists directly in PATH or current environment
        var isWindows = OperatingSystem.IsWindows();
        var exeName = isWindows && !binaryName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? $"{binaryName}.exe" : binaryName;

        // Check WinGet installation directory on Windows
        if (isWindows)
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var wingetDir = Path.Combine(userProfile, @"AppData\Local\Microsoft\WinGet\Packages");
            if (Directory.Exists(wingetDir))
            {
                var match = Directory.GetFiles(wingetDir, exeName, SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrEmpty(match) && File.Exists(match))
                {
                    return match;
                }
            }

            // Check Chocolatey
            var chocoDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"chocolatey\bin", exeName);
            if (File.Exists(chocoDir))
            {
                return chocoDir;
            }

            // Check Program Files
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pfMatch = Path.Combine(programFiles, "ffmpeg", "bin", exeName);
            if (File.Exists(pfMatch))
            {
                return pfMatch;
            }
        }

        return !string.IsNullOrWhiteSpace(configuredPath) ? configuredPath : binaryName;
    }

    private VideoProbeResult ParseProbeJson(string json, string filePath)
    {
        var probe = new VideoProbeResult();
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Exists)
        {
            probe.FileSizeBytes = fileInfo.Length;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Parse format
        if (root.TryGetProperty("format", out var formatElem))
        {
            if (formatElem.TryGetProperty("duration", out var durElem) &&
                double.TryParse(durElem.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var duration))
            {
                probe.DurationSeconds = duration;
            }

            if (formatElem.TryGetProperty("bit_rate", out var bitElem) &&
                long.TryParse(bitElem.GetString(), out var bitrate))
            {
                probe.Bitrate = bitrate;
            }

            if (probe.FileSizeBytes == 0 && formatElem.TryGetProperty("size", out var sizeElem) &&
                long.TryParse(sizeElem.GetString(), out var size))
            {
                probe.FileSizeBytes = size;
            }
        }

        // Parse streams
        if (root.TryGetProperty("streams", out var streamsElem) && streamsElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var stream in streamsElem.EnumerateArray())
            {
                var codecType = stream.TryGetProperty("codec_type", out var ct) ? ct.GetString() : null;

                if (codecType == "video" && string.IsNullOrEmpty(probe.VideoCodec))
                {
                    if (stream.TryGetProperty("codec_name", out var vc))
                        probe.VideoCodec = vc.GetString() ?? "";

                    if (stream.TryGetProperty("width", out var w))
                        probe.Width = w.GetInt32();

                    if (stream.TryGetProperty("height", out var h))
                        probe.Height = h.GetInt32();

                    if (stream.TryGetProperty("r_frame_rate", out var frElem))
                    {
                        probe.FrameRate = ParseFrameRate(frElem.GetString());
                    }
                    else if (stream.TryGetProperty("avg_frame_rate", out var afrElem))
                    {
                        probe.FrameRate = ParseFrameRate(afrElem.GetString());
                    }

                    if (probe.Bitrate == 0 && stream.TryGetProperty("bit_rate", out var vBit) &&
                        long.TryParse(vBit.GetString(), out var streamBitrate))
                    {
                        probe.Bitrate = streamBitrate;
                    }

                    if (probe.DurationSeconds == 0 && stream.TryGetProperty("duration", out var vDur) &&
                        double.TryParse(vDur.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var streamDur))
                    {
                        probe.DurationSeconds = streamDur;
                    }
                }
                else if (codecType == "audio" && string.IsNullOrEmpty(probe.AudioCodec))
                {
                    if (stream.TryGetProperty("codec_name", out var ac))
                        probe.AudioCodec = ac.GetString() ?? "";
                }
            }
        }

        probe.Resolution = GetResolutionLabel(probe.Width, probe.Height);
        return probe;
    }

    private static double ParseFrameRate(string? frameRateStr)
    {
        if (string.IsNullOrWhiteSpace(frameRateStr)) return 0;

        var parts = frameRateStr.Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var num) &&
            double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var den) &&
            den > 0)
        {
            return Math.Round(num / den, 2);
        }

        if (double.TryParse(frameRateStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var direct))
        {
            return Math.Round(direct, 2);
        }

        return 0;
    }

    private static string GetResolutionLabel(int width, int height)
    {
        if (width <= 0 || height <= 0) return string.Empty;

        var tag = height switch
        {
            >= 2160 => "4K",
            >= 1440 => "1440p",
            >= 1080 => "1080p",
            >= 720 => "720p",
            >= 480 => "480p",
            >= 360 => "360p",
            _ => $"{height}p"
        };

        return $"{width}x{height} ({tag})";
    }
}
