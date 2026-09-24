using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Volterp.Infrastructure.Data;

public class VolterpDbContextDesignTimeFactory : IDesignTimeDbContextFactory<VolterpDbContext>
{
    public VolterpDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("VOLTERP_CONNECTION_STRING")
            ?? "Host=localhost;Database=volterp;Username=volterp;Password=temppassword123;Port=5432";

        var options = new DbContextOptionsBuilder<VolterpDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new VolterpDbContext(options);
    }
}
