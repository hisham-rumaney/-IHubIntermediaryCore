using IHub.Application.Interfaces.Persistence;
using IHub.Application.Interfaces.Repositories;
using IHub.Data.Persistence;
using IHub.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IHub.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataLayer(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseProvider = configuration["DatabaseProvider"] ?? "Npgsql";

        services.AddDbContext<IHubDbContext>(options =>
        {
            if (string.Equals(databaseProvider, "InMemory", StringComparison.OrdinalIgnoreCase))
            {
                var databaseName = configuration["InMemoryDatabaseName"] ?? "ihub-tests";
                options.UseInMemoryDatabase(databaseName);
                return;
            }

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Port=5432;Database=ihub";

            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IIntermediaryRepository, IntermediaryRepository>();
        services.AddScoped<IStatementRepository, StatementRepository>();
        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IHubDbContext>();

        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync();
            return;
        }

        await dbContext.Database.EnsureCreatedAsync();
    }
}
