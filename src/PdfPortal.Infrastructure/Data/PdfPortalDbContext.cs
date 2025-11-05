using Microsoft.EntityFrameworkCore;
using PdfPortal.Domain.Entities;

namespace PdfPortal.Infrastructure.Data;

public class PdfPortalDbContext : DbContext
{
    public PdfPortalDbContext(DbContextOptions<PdfPortalDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<TemplateRuleSet> TemplateRuleSets { get; set; }
    public DbSet<DocumentOriginal> DocumentOriginals { get; set; }
    public DbSet<DocumentProcessed> DocumentProcessed { get; set; }
    public DbSet<Notification> Notifications { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Role).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // TemplateRuleSet configuration
        modelBuilder.Entity<TemplateRuleSet>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.JsonDefinition).IsRequired();
            entity.HasOne(e => e.CreatedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // DocumentOriginal configuration
        modelBuilder.Entity<DocumentOriginal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(255);
            entity.HasOne(e => e.UploaderUser)
                  .WithMany()
                  .HasForeignKey(e => e.UploaderUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // DocumentProcessed configuration
        modelBuilder.Entity<DocumentProcessed>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FilePathFinalPdf).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ExtractedJsonData).IsRequired();
            entity.HasOne(e => e.SourceDocument)
                  .WithMany()
                  .HasForeignKey(e => e.SourceDocumentId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.TemplateRuleSet)
                  .WithMany()
                  .HasForeignKey(e => e.TemplateRuleSetId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
