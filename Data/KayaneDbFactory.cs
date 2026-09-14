using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Kayane.Data;

public class KayaneDbFactory : IDesignTimeDbContextFactory<KayaneDb>
{
    public KayaneDb CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=kayane_db;Username=postgres;Password=postgres";

        var builder = new DbContextOptionsBuilder<KayaneDb>();
        builder.UseNpgsql(connectionString);

        return new KayaneDb(builder.Options);
    }
}