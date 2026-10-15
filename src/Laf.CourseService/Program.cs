using Laf.CourseService.BackgroundJobs;
using Laf.CourseService.Data;
using Laf.CourseService.Endpoints;
using Laf.CourseService.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure Media Storage Options
builder.Services.Configure<MediaStorageOptions>(builder.Configuration.GetSection(MediaStorageOptions.SectionName));

// Configure EF Core SQLite Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=media_stream.db";
builder.Services.AddDbContext<MediaDbContext>(options =>
{
    options.UseSqlite(connectionString);
});

// Configure Queue, Services and Background Worker
builder.Services.AddSingleton<VideoProcessingQueue>();
builder.Services.AddSingleton<IVideoStorageService, VideoStorageService>();
builder.Services.AddScoped<IHlsTranscoderService, HlsTranscoderService>();
builder.Services.AddHostedService<VideoProcessingWorker>();

// Configure Large File Upload limits (500MB)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524_288_000;
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 524_288_000;
});

// Configure IIS In-Process large file upload limits
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 524_288_000;
});

// Configure In-Memory Cache Tracker for HIT/MISS status
builder.Services.AddMemoryCache();

// Configure CORS for web video players (Video.js, HLS.js, Plyr, etc.)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("X-Cache-Status", "Content-Range", "Accept-Ranges");
    });
});

// OpenAPI Documentation
builder.Services.AddOpenApi();

var app = builder.Build();

// Ensure SQLite database & tables exist on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors();

app.MapGet("/", () => Results.Ok(new
{
    service = "LAF Course Video & HLS Streaming API",
    status = "Online",
    version = "1.0.0",
    test_dashboard = "/test",
    docs = "/openapi/v1.json"
}))
.WithName("RootInfo")
.ExcludeFromDescription();

// Interactive HTML5 & HLS Test / Player Dashboard
app.MapGet("/test", () => Results.Content(TestPlayerHtml.Content, "text/html"))
    .WithName("TestPlayer")
    .ExcludeFromDescription();

// Map Minimal API Endpoints
app.MapVideoEndpoints();

app.Run();
