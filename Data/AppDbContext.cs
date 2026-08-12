using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SubiteAPI.Features.TripPricing.Domain.Models;
using SubiteAPI.Models;

namespace SubiteAPI.Data;

public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Ride> Rides => Set<Ride>();
    public DbSet<RideStop> RideStops => Set<RideStop>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Verification> Verifications => Set<Verification>();
    public DbSet<DiditSession> DiditSessions => Set<DiditSession>();
    public DbSet<AppNotification> AppNotifications => Set<AppNotification>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PricingConfig> PricingConfigs => Set<PricingConfig>();
    public DbSet<ReferencePrice> ReferencePrices => Set<ReferencePrice>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // User
        builder.Entity<User>(e =>
        {
            e.Property(u => u.Rating).HasPrecision(2, 1);
        });

        // Vehicle
        builder.Entity<Vehicle>(e =>
        {
            e.HasOne(v => v.User)
                .WithOne(u => u.Vehicle)
                .HasForeignKey<Vehicle>(v => v.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Ride
        builder.Entity<Ride>(e =>
        {
            e.Property(r => r.PricePerSeat).HasPrecision(10, 2);

            e.HasOne(r => r.Driver)
                .WithMany(u => u.RidesAsDriver)
                .HasForeignKey(r => r.DriverId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.Vehicle)
                .WithMany(v => v.Rides)
                .HasForeignKey(r => r.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(r => r.Stops)
                .WithOne(s => s.Ride)
                .HasForeignKey(s => s.RideId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RideStop>(e =>
        {
            e.HasIndex(s => new { s.RideId, s.Sequence }).IsUnique();
            e.Property(s => s.City).HasMaxLength(120);
            e.Property(s => s.Address).HasMaxLength(250);
        });

        // Reservation
        builder.Entity<Reservation>(e =>
        {
            e.Property(r => r.TotalPrice).HasPrecision(10, 2);

            e.HasOne(r => r.Ride)
                .WithMany(ride => ride.Reservations)
                .HasForeignKey(r => r.RideId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.Passenger)
                .WithMany(u => u.Reservations)
                .HasForeignKey(r => r.PassengerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Payment
        builder.Entity<Payment>(e =>
        {
            e.Property(p => p.Amount).HasPrecision(10, 2);

            e.HasOne(p => p.Reservation)
                .WithOne(r => r.Payment)
                .HasForeignKey<Payment>(p => p.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Message
        builder.Entity<Message>(e =>
        {
            e.HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(m => m.Receiver)
                .WithMany()
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(m => m.Ride)
                .WithMany(r => r.Messages)
                .HasForeignKey(m => m.RideId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Verification
        builder.Entity<Verification>(e =>
        {
            e.HasOne(v => v.User)
                .WithMany(u => u.Verifications)
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DiditSession>(e =>
        {
            e.HasIndex(s => s.SessionId).IsUnique();
            e.HasIndex(s => s.UserId);
            e.Property(s => s.SessionId).HasMaxLength(64);
            e.Property(s => s.Status).HasMaxLength(64);
            e.HasOne(s => s.User)
                .WithMany(u => u.DiditSessions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AppNotification>(e =>
        {
            e.HasIndex(n => new { n.UserId, n.CreatedAt });
            e.HasIndex(n => new { n.UserId, n.IsRead });
            e.Property(n => n.Type).HasMaxLength(32);
            e.Property(n => n.Title).HasMaxLength(200);
            e.Property(n => n.Body).HasMaxLength(1000);
            e.Property(n => n.ActionUrl).HasMaxLength(500);
            e.HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // RefreshToken
        builder.Entity<RefreshToken>(e =>
        {
            e.HasIndex(rt => rt.Token).IsUnique();
            e.Property(rt => rt.Token).HasMaxLength(512);

            e.HasOne(rt => rt.User)
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Review
        builder.Entity<Review>(e =>
        {
            e.HasOne(r => r.Ride)
                .WithMany(ride => ride.Reviews)
                .HasForeignKey(r => r.RideId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(r => r.Reviewer)
                .WithMany(u => u.ReviewsGiven)
                .HasForeignKey(r => r.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.ReviewedUser)
                .WithMany(u => u.ReviewsReceived)
                .HasForeignKey(r => r.ReviewedUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Trip Pricing
        builder.Entity<PricingConfig>(e =>
        {
            e.Property(c => c.FuelPricePerLiter).HasPrecision(10, 2);
            e.Property(c => c.WearCostPerKm).HasPrecision(10, 4);
            e.Property(c => c.MaxPriceRatioVsReference).HasPrecision(5, 2);
            e.HasIndex(c => c.IsActive);
        });

        builder.Entity<ReferencePrice>(e =>
        {
            e.Property(r => r.Price).HasPrecision(10, 2);
            e.HasIndex(r => new { r.OriginCity, r.DestinationCity, r.TransportMode });
        });
    }
}
