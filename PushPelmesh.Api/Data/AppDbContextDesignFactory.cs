using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PushPelmesh.Api.Data;

// EF tooling must not run Program.cs: startup applies migrations and starts background jobs.
public class AppDbContextDesignFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=pushpelmesh_design_only")
            .Options;
        return new AppDbContext(options);
    }
}
