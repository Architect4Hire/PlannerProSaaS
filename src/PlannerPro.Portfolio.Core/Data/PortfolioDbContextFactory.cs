using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Portfolio.Core.Data;

/// <summary>Design-time factory for <c>dotnet ef migrations add</c> / <c>database update</c> —
/// same shape as <c>AccessDbContextFactory</c>. Building the model never evaluates the tenant
/// filter's runtime value, so the connection string here is a placeholder.</summary>
public sealed class PortfolioDbContextFactory : IDesignTimeDbContextFactory<PortfolioDbContext>
{
    public PortfolioDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseSqlServer("Server=localhost;Database=portfoliodb;Trusted_Connection=True;TrustServerCertificate=True;");

        return new PortfolioDbContext(optionsBuilder.Options, new DesignTimeTenantContext());
    }
}
