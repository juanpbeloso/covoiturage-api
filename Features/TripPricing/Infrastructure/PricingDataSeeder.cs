using Microsoft.EntityFrameworkCore;
using SubiteAPI.Data;
using SubiteAPI.Features.TripPricing.Domain.Models;

namespace SubiteAPI.Features.TripPricing.Infrastructure;

public static class PricingDataSeeder
{
    private static readonly Guid DefaultConfigId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public static async Task SeedAsync(AppDbContext db)
    {
        if (!await db.PricingConfigs.AnyAsync().ConfigureAwait(false))
        {
            var now = DateTime.UtcNow;
            db.PricingConfigs.Add(new PricingConfig
            {
                Id = DefaultConfigId,
                Name = "Argentina - Junio 2026",
                IsActive = true,
                FuelPricePerLiter = 1800,
                KmPerLiter = 11,
                WearCostPerKm = 0,
                MaxPriceRatioVsReference = 1.0m,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (!await db.ReferencePrices.AnyAsync().ConfigureAwait(false))
        {
            var now = DateTime.UtcNow;
            var validFrom = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

            db.ReferencePrices.AddRange(
                new ReferencePrice
                {
                    Id = Guid.NewGuid(),
                    OriginCity = "Junín",
                    DestinationCity = "Buenos Aires",
                    TransportMode = "bus_semi_cama",
                    Label = "Colectivo semicama Junín → Retiro",
                    Price = 30000,
                    ValidFrom = validFrom,
                    Source = "plataforma10.com.ar",
                    UpdatedAt = now
                },
                new ReferencePrice
                {
                    Id = Guid.NewGuid(),
                    OriginCity = "Junín",
                    DestinationCity = "Retiro",
                    TransportMode = "bus_semi_cama",
                    Label = "Colectivo semicama Junín → Retiro",
                    Price = 30000,
                    ValidFrom = validFrom,
                    Source = "plataforma10.com.ar",
                    UpdatedAt = now
                },
                new ReferencePrice
                {
                    Id = Guid.NewGuid(),
                    OriginCity = "Junín",
                    DestinationCity = "Vicente López",
                    TransportMode = "bus_semi_cama",
                    Label = "Colectivo semicama Junín → Vicente López (est.)",
                    Price = 32000,
                    ValidFrom = validFrom,
                    Source = "manual",
                    UpdatedAt = now
                },
                new ReferencePrice
                {
                    Id = Guid.NewGuid(),
                    OriginCity = "Junín",
                    DestinationCity = "Nuñez",
                    TransportMode = "bus_semi_cama",
                    Label = "Colectivo semicama Junín → Núñez (est.)",
                    Price = 31000,
                    ValidFrom = validFrom,
                    Source = "manual",
                    UpdatedAt = now
                }
            );
        }

        await db.SaveChangesAsync().ConfigureAwait(false);
    }
}
