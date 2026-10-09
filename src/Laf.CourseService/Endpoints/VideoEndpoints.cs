using Laf.CourseService.BackgroundJobs;
using Laf.CourseService.Data;
using Laf.CourseService.Models;
using Laf.CourseService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Laf.CourseService.Endpoints;

public static class VideoEndpoints
{
    public static RouteGroupBuilder MapVideoEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/videos")
            .WithTags("Videos");

        // 1. Upload Video
        group.MapPost("/upload", async (
            HttpRequest request,
            [FromForm] IFormFile? videoFile,
            [FromForm] string? uploadedBy,
            MediaDbContext dbContext,
            IVideoStorageService storageService,
            VideoProcessingQueue queue,
            IOptions<MediaStorageOptions> storageOptions,
            CancellationToken cancellationToken) =>
        {
            var options = storageOptions.Value;

            // Fallback checking if file is inside request.Form.Files
            if (videoFile == null && request.HasFormContentType && request.Form.Files.Count > 0)
            {
                videoFile = request.Form.Files["videoFile"] ?? request.Form.Files[0];
            }

            if (string.IsNullOrEmpty(uploadedBy) && request.HasFormContentType && request.Form.TryGetValue("uploadedBy", out var uploadedByVal))
            {
                uploadedBy = uploadedByVal.ToString();
            }

            if (videoFile == null || videoFile.Length == 0)
            {
                return Results.BadRequest(new { error = "A valid video file is required in 'videoFile' field." });
            }

            if (videoFile.Length > options.MaxFileSizeBytes)
            {
                return Results.BadRequest(new
                {
                    error = $"File size exceeds the maximum limit of {options.MaxFileSizeBytes / (1024 * 1024)} MB."
                });
            }

            var extension = Path.GetExtension(videoFile.FileName).ToLowerInvariant();
            if (!options.AllowedExtensions.Contains(extension))
            {
                return Results.BadRequest(new
                {
                    error = $"Unsupported file extension '{extension}'. Allowed extensions: {string.Join(", ", options.AllowedExtensions)}"
                });
            }

            var videoId = Guid.NewGuid();

            // Save raw upload to temp folder
            await storageService.SaveTemporaryUploadAsync(videoId, videoFile, cancellationToken);

            // Create initial DB entry with Pending status
            var videoMetadata = new VideoMetadata
            {
                Id = videoId,
                OriginalFileName = videoFile.FileName,
                ContentType = string.IsNullOrWhiteSpace(videoFile.ContentType) ? "video/mp4" : videoFile.ContentType,
                FileSizeBytes = videoFile.Length,
                UploadedBy = string.IsNullOrWhiteSpace(uploadedBy) ? "Anonymous" : uploadedBy.Trim(),
                Status = VideoProcessingStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Videos.Add(videoMetadata);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Enqueue background processing job
            await queue.QueueVideoAsync(videoId);

            var baseUrl = $"{request.Scheme}://{request.Host}";
            var statusUrl = $"{baseUrl}/api/videos/{videoId}";

            var response = new VideoUploadAcceptedResponse
            {
                VideoId = videoId,
                Message = "Video uploaded successfully and queued for HLS transcoding.",
                Status = VideoProcessingStatus.Pending,
                StatusUrl = statusUrl
            };

            return Results.Accepted(statusUrl, response);
        })
        .DisableAntiforgery()
        .WithName("UploadVideo")
        .WithSummary("Uploads a video file and starts asynchronous HLS transcoding.")
        .Produces<VideoUploadAcceptedResponse>(StatusCodes.Status202Accepted)
        .Produces(StatusCodes.Status400BadRequest);

        // 2. Get Video Status & Metadata by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            HttpRequest request,
            MediaDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var video = await dbContext.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
            if (video == null)
            {
                return Results.NotFound(new { error = $"Video with ID '{id}' was not found." });
            }

            var baseUrl = $"{request.Scheme}://{request.Host}";
            return Results.Ok(VideoResponseDto.FromEntity(video, baseUrl));
        })
        .WithName("GetVideoById")
        .WithSummary("Gets metadata and processing status for a video.")
        .Produces<VideoResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 3. List All Videos
        group.MapGet("/", async (
            HttpRequest request,
            MediaDbContext dbContext,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default) =>
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var totalCount = await dbContext.Videos.CountAsync(cancellationToken);
            var items = await dbContext.Videos
                .AsNoTracking()
                .OrderByDescending(v => v.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var baseUrl = $"{request.Scheme}://{request.Host}";
            var dtoList = items.Select(v => VideoResponseDto.FromEntity(v, baseUrl)).ToList();

            return Results.Ok(new
            {
                total = totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                data = dtoList
            });
        })
        .WithName("ListVideos")
        .WithSummary("Lists uploaded videos with pagination.")
        .Produces(StatusCodes.Status200OK);

        // 4. HLS Master Playlist Manifest (.m3u8)
        group.MapGet("/{id:guid}/manifest.m3u8", async (
            Guid id,
            HttpResponse response,
            MediaDbContext dbContext,
            IVideoStorageService storageService,
            CancellationToken cancellationToken) =>
        {
            var video = await dbContext.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
            if (video == null)
            {
                return Results.NotFound(new { error = $"Video with ID '{id}' was not found." });
            }

            if (video.Status != VideoProcessingStatus.Completed)
            {
                return Results.Problem(
                    detail: $"Video processing is currently '{video.Status}'. Master playlist is only available when status is Completed.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            var masterPath = storageService.GetMasterPlaylistPath(id);
            if (!File.Exists(masterPath))
            {
                return Results.NotFound(new { error = "HLS master playlist file not found on server." });
            }

            response.Headers.CacheControl = "public, max-age=60";
            return Results.File(masterPath, "application/vnd.apple.mpegurl", enableRangeProcessing: true);
        })
        .WithName("GetHlsManifest")
        .WithSummary("Serves HLS master playlist (.m3u8) for video streaming.")
        .Produces(StatusCodes.Status200OK, contentType: "application/vnd.apple.mpegurl")
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // 5. HLS Video Segment (.ts chunk) - supports both direct relative /{id}/{segmentName} and /{id}/segments/{segmentName}
        group.MapGet("/{id:guid}/{segmentName}", ServeSegment);
        group.MapGet("/{id:guid}/segments/{segmentName}", ServeSegment)
            .WithName("GetHlsSegment")
            .WithSummary("Serves an immutable HLS video chunk segment (.ts).")
            .Produces(StatusCodes.Status200OK, contentType: "video/mp2t")
            .Produces(StatusCodes.Status404NotFound);

        static IResult ServeSegment(
            Guid id,
            string segmentName,
            HttpResponse response,
            IVideoStorageService storageService)
        {
            if (string.IsNullOrWhiteSpace(segmentName) || !segmentName.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = "Invalid segment file name. Must be a .ts file." });
            }

            var segmentPath = storageService.GetSegmentPath(id, segmentName);
            if (!File.Exists(segmentPath))
            {
                return Results.NotFound(new { error = $"Segment '{segmentName}' not found for video '{id}'." });
            }

            // High-performance immutable caching for video chunks
            response.Headers.CacheControl = "public, max-age=31536000, immutable";
            return Results.File(segmentPath, "video/mp2t", enableRangeProcessing: true);
        }

        // 6. Poster Thumbnail
        group.MapGet("/{id:guid}/thumbnail", async (
            Guid id,
            HttpResponse response,
            MediaDbContext dbContext,
            IVideoStorageService storageService,
            CancellationToken cancellationToken) =>
        {
            var video = await dbContext.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
            if (video == null)
            {
                return Results.NotFound(new { error = $"Video with ID '{id}' was not found." });
            }

            var thumbnailPath = storageService.GetThumbnailPath(id);
            if (!File.Exists(thumbnailPath))
            {
                return Results.NotFound(new { error = "Thumbnail file not found." });
            }

            response.Headers.CacheControl = "public, max-age=86400";
            return Results.File(thumbnailPath, "image/jpeg");
        })
        .WithName("GetVideoThumbnail")
        .WithSummary("Serves the generated video thumbnail image.")
        .Produces(StatusCodes.Status200OK, contentType: "image/jpeg")
        .Produces(StatusCodes.Status404NotFound);

        // 7. Delete Video
        group.MapDelete("/{id:guid}", async (
            Guid id,
            MediaDbContext dbContext,
            IVideoStorageService storageService,
            CancellationToken cancellationToken) =>
        {
            var video = await dbContext.Videos.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
            if (video == null)
            {
                return Results.NotFound(new { error = $"Video with ID '{id}' was not found." });
            }

            storageService.CleanupTempFile(id);
            storageService.DeleteProcessedHls(id);

            dbContext.Videos.Remove(video);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        })
        .WithName("DeleteVideo")
        .WithSummary("Deletes video record and all associated processed HLS files.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        return group;
    }
}
