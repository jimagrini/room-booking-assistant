using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomBooking.Infrastructure.Persistence;

namespace RoomBooking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<RoomBookingDbContext>(
            options => options.UseNpgsql(connectionString));

        return services;
    }
}
