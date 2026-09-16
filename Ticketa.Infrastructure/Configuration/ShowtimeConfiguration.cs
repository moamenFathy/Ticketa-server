using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketa.Core.Entities;

namespace Ticketa.Infrastructure.Configuration
{
  public class ShowtimeConfiguration : IEntityTypeConfiguration<Showtime>
  {
    public void Configure(EntityTypeBuilder<Showtime> builder)
    {
      builder.Property(s => s.Price)
             .HasColumnType("decimal(18,2)")
             .IsRequired();

      builder.Property(s => s.Status)
             .HasConversion<int>();

      builder.HasOne(m => m.Movie)
             .WithMany()
             .HasForeignKey(s => s.MovieId)
             .OnDelete(DeleteBehavior.Restrict);

      builder.HasOne(h => h.Hall)
             .WithMany(s => s.Showtimes)
             .HasForeignKey(s => s.HallId)
             .OnDelete(DeleteBehavior.Restrict);

      // Filtered index: covers "all active" count query
      builder.HasIndex(s => s.IsArchived).HasFilter("[IsArchived] = 0");

      // Composite index: covers filtered DataTable queries (status + movie grouping)
      builder.HasIndex(s => new { s.IsArchived, s.Status, s.MovieId })
          .HasFilter("[IsArchived] = 0");

      // Covering index: IN/MovieId lookups (paged data load)
      builder.HasIndex(s => s.MovieId);

      // Covering index: conflict checks and timeline queries
      builder.HasIndex(s => new { s.HallId, s.StartTime });
    }
  }
}
