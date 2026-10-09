using Microsoft.EntityFrameworkCore;
using Laf.CourseService.Models;

namespace Laf.CourseService.Data;

public class MediaDbContext : DbContext
{
    public MediaDbContext(DbContextOptions<MediaDbContext> options) : base(options)
    {
    }

    public DbSet<VideoMetadata> Videos => Set<VideoMetadata>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VideoMetadata>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OriginalFileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.Property(e => e.Resolution).HasMaxLength(50);
            entity.Property(e => e.VideoCodec).HasMaxLength(50);
            entity.Property(e => e.AudioCodec).HasMaxLength(50);
            entity.Property(e => e.UploadedBy).HasMaxLength(100);
            entity.Property(e => e.HlsMasterPlaylistPath).HasMaxLength(500);
            entity.Property(e => e.ThumbnailPath).HasMaxLength(500);

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}
