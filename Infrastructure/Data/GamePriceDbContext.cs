using GamePrice.Api.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace GamePrice.Api.Infrastructure.Data
{
    public class GamePriceDbContext : DbContext
    {
        public GamePriceDbContext(DbContextOptions<GamePriceDbContext> options)
            : base(options)
        {
        }

        public DbSet<UserModel> Users => Set<UserModel>();
        public DbSet<GameModel> Games => Set<GameModel>();
        public DbSet<StoreModel> Stores => Set<StoreModel>();
        public DbSet<OfferModel> Offers => Set<OfferModel>();
        public DbSet<PriceSnapshotModel> PriceSnapshots => Set<PriceSnapshotModel>();
        public DbSet<WishlistAlertModel> WishlistAlerts => Set<WishlistAlertModel>();
        public DbSet<LoginAuditModel> LoginAudits => Set<LoginAuditModel>();
        public DbSet<SearchHistoryModel> SearchHistory => Set<SearchHistoryModel>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserModel>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
                entity.Property(item => item.Email).HasMaxLength(254).IsRequired();
                entity.Property(item => item.NormalizedEmail).HasMaxLength(254).IsRequired();
                entity.Property(item => item.PasswordHash).HasMaxLength(256).IsRequired();
                entity.HasIndex(item => item.NormalizedEmail).IsUnique();
            });

            modelBuilder.Entity<GameModel>(entity =>
            {
                entity.ToTable("games");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Title).HasMaxLength(300).IsRequired();
                entity.Property(item => item.NormalizedTitle).HasMaxLength(300).IsRequired();
                entity.Property(item => item.ImageUrl).HasMaxLength(2048);
                entity.HasIndex(item => item.NormalizedTitle).IsUnique();
                entity.HasIndex(item => item.UpdatedAt);
            });

            modelBuilder.Entity<StoreModel>(entity =>
            {
                entity.ToTable("stores");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
                entity.Property(item => item.Slug).HasMaxLength(100).IsRequired();
                entity.Property(item => item.WebsiteUrl).HasMaxLength(2048);
                entity.HasIndex(item => item.Slug).IsUnique();
            });

            modelBuilder.Entity<OfferModel>(entity =>
            {
                entity.ToTable("offers");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Currency).HasMaxLength(3).IsRequired();
                entity.Property(item => item.Platform).HasMaxLength(50).IsRequired();
                entity.Property(item => item.RedirectUrl).HasMaxLength(2048);
                entity.Property(item => item.ImageUrl).HasMaxLength(2048);
                entity.Property(item => item.Source).HasMaxLength(80).IsRequired();
                entity.HasIndex(item => new { item.GameId, item.StoreId, item.Platform }).IsUnique();
                entity.HasIndex(item => item.ObservedAt);
                entity.HasOne(item => item.Game)
                    .WithMany(game => game.Offers)
                    .HasForeignKey(item => item.GameId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(item => item.Store)
                    .WithMany(store => store.Offers)
                    .HasForeignKey(item => item.StoreId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PriceSnapshotModel>(entity =>
            {
                entity.ToTable("price_snapshots");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Id).ValueGeneratedOnAdd();
                entity.Property(item => item.Currency).HasMaxLength(3).IsRequired();
                entity.HasIndex(item => new { item.OfferId, item.ObservedAt });
                entity.HasOne(item => item.Offer)
                    .WithMany(offer => offer.PriceHistory)
                    .HasForeignKey(item => item.OfferId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<WishlistAlertModel>(entity =>
            {
                entity.ToTable("wishlist_alerts");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Currency).HasMaxLength(3).IsRequired();
                entity.HasIndex(item => new { item.UserId, item.GameId }).IsUnique();
                entity.HasOne(item => item.User)
                    .WithMany(user => user.WishlistAlerts)
                    .HasForeignKey(item => item.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(item => item.Game)
                    .WithMany(game => game.WishlistAlerts)
                    .HasForeignKey(item => item.GameId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<LoginAuditModel>(entity =>
            {
                entity.ToTable("login_audits");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Id).ValueGeneratedOnAdd();
                entity.Property(item => item.Email).HasMaxLength(254).IsRequired();
                entity.Property(item => item.FailureReason).HasMaxLength(80);
                entity.Property(item => item.IpAddressHash).HasMaxLength(64);
                entity.Property(item => item.UserAgent).HasMaxLength(512);
                entity.HasIndex(item => item.OccurredAt);
                entity.HasIndex(item => item.Email);
                entity.HasOne(item => item.User)
                    .WithMany(user => user.LoginAudits)
                    .HasForeignKey(item => item.UserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<SearchHistoryModel>(entity =>
            {
                entity.ToTable("search_history");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Id).ValueGeneratedOnAdd();
                entity.Property(item => item.Query).HasMaxLength(300).IsRequired();
                entity.HasIndex(item => item.SearchedAt);
                entity.HasIndex(item => item.Query);
                entity.HasOne(item => item.User)
                    .WithMany(user => user.Searches)
                    .HasForeignKey(item => item.UserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
