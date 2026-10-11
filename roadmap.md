# Roadmap: High-Performance HLS Video Upload & Streaming API (.NET Minimal API + Nginx)

This roadmap defines the complete step-by-step implementation blueprint for building a lightweight, ultra-high-performance **Video Management & HLS Streaming API** using **.NET 10 Minimal APIs (C#)**, **EF Core (SQLite)**, **FFmpeg**, and **Nginx Reverse Proxy Caching**.

---

## 🏗️ Architectural Overview & Principles

1. **Minimal API Architecture**: High-performance, controller-less endpoint routing using `app.MapGroup("/api/videos")` with clean folder division (`Endpoints`, `Services`, `Data`, `Models`, `BackgroundJobs`).
2. **Asynchronous HLS Processing**: Video upload saves the raw file and returns a `GUID` immediately (`202 Accepted`). Transcoding to HLS (`.m3u8` playlist & `.ts` chunks) runs in an in-memory background worker (`Channel<Guid>`).
3. **Rich Metadata Extraction**: FFprobe automatically extracts duration, resolution, codecs, bitrate, frame rate, file size, and generates a poster thumbnail.
4. **Nginx HLS Chunk Caching**: Serves `.ts` chunks with `max-age=31536000, immutable` headers for 100% cache hit rates on repeat views, drastically reducing bandwidth and CPU usage.

```
[LAF Client] ──(POST Video Upload)──> [.NET Minimal API] ──(Instant GUID)──> [DB: Pending Status]
                                               │
                                    (Background Channel Queue)
                                               │
                                               ▼
                                    [FFmpeg HLS Transcoder]
                                     ├─ Extract Metadata & Thumbnail
                                     ├─ Convert to .m3u8 + .ts Chunks
                                     └─ Update DB Status -> Completed
                                               │
[HLS Player / LAF] <──(Stream .m3u8/.ts)── [Nginx Cache] <── (Static Files)
```

---

## 📁 Project Structure

```text
src/Laf.CourseService/
├── BackgroundJobs/
│   ├── VideoProcessingQueue.cs     # Channel-based async queue
│   └── VideoProcessingWorker.cs    # Background HostedService worker
├── Data/
│   └── MediaDbContext.cs           # EF Core DbContext (SQLite)
├── Endpoints/
│   └── VideoEndpoints.cs           # Minimal API endpoints (Upload, Manifest, Segment, Thumbnail, Metadata)
├── Models/
│   ├── VideoMetadata.cs            # EF Core Entity for Video Metadata & Status
│   ├── UploadVideoRequest.cs       # DTO for incoming upload
│   ├── VideoResponseDto.cs         # JSON DTO for LAF integration
│   └── VideoProbeResult.cs         # Parsed FFprobe metadata model
├── Services/
│   ├── IVideoStorageService.cs     # File I/O & directory management
│   ├── VideoStorageService.cs
│   ├── IHlsTranscoderService.cs    # FFmpeg wrapper (HLS conversion & metadata extraction)
│   ├── HlsTranscoderService.cs
│   └── MediaStorageOptions.cs      # Storage configuration options
├── Storage/                        # Local Storage
│   ├── TempUploads/                # Temporary raw uploaded MP4s
│   └── ProcessedHls/               # Extracted {Guid}/master.m3u8, {Guid}/segment_000.ts, thumbnail.jpg
├── appsettings.json
├── nginx.conf                      # Production-ready Nginx configuration
└── Program.cs                      # Minimal APIs / Service registrations
```

---

## 📋 Execution Phases & Action Items

### Phase 1: Database & Entity Framework Core Setup

- [x] **Data Model (`Models/VideoMetadata.cs`)**:
  Entity containing full video details & `VideoProcessingStatus` enum.
- [x] **EF Core Context (`Data/MediaDbContext.cs`)**:
  Configured `DbSet<VideoMetadata>` with SQLite provider and auto-initialization.

---

### Phase 2: Asynchronous Background Processing Pipeline

- [x] **Processing Channel (`BackgroundJobs/VideoProcessingQueue.cs`)**:
  Implemented lightweight thread-safe queue using `System.Threading.Channels`.
- [x] **Background Worker (`BackgroundJobs/VideoProcessingWorker.cs`)**:
  Implemented `BackgroundService` to process queued videos sequentially without blocking Web API requests.

---

### Phase 3: FFmpeg HLS Transcoding & Metadata Service

- [x] **FFmpeg Integration (`Services/HlsTranscoderService.cs`)**:
  Implemented FFmpeg / FFprobe commands using `CliWrap`:
  1. **Metadata Probe Command (FFprobe)**:
     `ffprobe -v quiet -print_format json -show_format -show_streams input.mp4`
  2. **Poster Thumbnail Extraction**:
     `ffmpeg -ss 00:00:01 -i input.mp4 -vframes 1 -q:v 2 thumbnail.jpg`
  3. **HLS Transcoding (MP4 -> .m3u8 + .ts Chunks)**:
     `ffmpeg -i input.mp4 -codec:v libx264 -crf 22 -preset fast -codec:a aac -b:a 128k -hls_time 6 -hls_playlist_type vod -hls_segment_filename "segment_%03d.ts" master.m3u8`

---

### Phase 4: Minimal API Endpoints Implementation (`Endpoints/VideoEndpoints.cs`)

- [x] **Upload Endpoint (`POST /api/videos/upload`)**:
  - Accepts `IFormFile videoFile` and optional `uploadedBy` parameter.
  - Validates extension (`.mp4`, `.mov`, `.avi`, `.mkv`, `.webm`) and max size.
  - Generates new `Guid videoId`.
  - Saves file to `Storage/TempUploads/{videoId}.mp4`.
  - Creates DB entry with `Status = VideoProcessingStatus.Pending`.
  - Triggers `_processingQueue.QueueVideoAsync(videoId)`.
  - Returns HTTP 202 Accepted with JSON containing `videoId` and status endpoint URL.

- [x] **Status / Metadata Endpoint (`GET /api/videos/{id}`)**:
  - Returns full metadata JSON (used by LAF or Web Clients to check if video processing is `Completed`).

- [x] **List Videos Endpoint (`GET /api/videos`)**:
  - Paginated list of videos with metadata and status.

- [x] **HLS Playlist Endpoint (`GET /api/videos/{id}/manifest.m3u8`)**:
  - Serves `master.m3u8` file with `Cache-Control: public, max-age=60` and `Content-Type: application/vnd.apple.mpegurl`.

- [x] **HLS Segment Endpoint (`GET /api/videos/{id}/segments/{segmentName}`)**:
  - Serves `.ts` video chunk file with `Cache-Control: public, max-age=31536000, immutable` and `Content-Type: video/mp2t`.

- [x] **Thumbnail Endpoint (`GET /api/videos/{id}/thumbnail`)**:
  - Serves `thumbnail.jpg` image (`Content-Type: image/jpeg`).

- [x] **Delete Video Endpoint (`DELETE /api/videos/{id}`)**:
  - Deletes video DB record and cleans up associated files.

---

### Phase 5: Nginx Reverse Proxy & HLS Caching Setup (`nginx.conf`)

- [x] **Nginx Config Creation (`nginx.conf`)**:
  Configured Nginx to cache `.ts` segments with immutable headers and zero-copy TCP streaming.

---

### Phase 6: LED Application Framework (LAF) Integration Guide

- [x] **LAF C# Upload Snippet**:
  Documented in `README.md` and `roadmap.md` with C# `HttpClient` / `MultipartFormDataContent` example.
