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

// Configure CORS for web video players (Video.js, HLS.js, Plyr, etc.)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// OpenAPI Documentation
builder.Services.AddOpenApi();

var app = builder.Build();

// Ensure SQLite Database is initialized
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
    dbContext.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

// Root API Health & Info
app.MapGet("/", () => Results.Ok(new
{
    service = "LAF Course Video & HLS Streaming API",
    status = "Online",
    version = "1.0.0",
    docs = "/openapi/v1.json"
}))
.WithName("RootInfo")
.ExcludeFromDescription();

// Map Minimal API Endpoints
app.MapVideoEndpoints();

app.Run();
