using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Access.Core.Data;

/// <summary>
/// Design-time factory for <c>dotnet ef migrations add</c> / <c>database update</c> — fulfils the
/// "future <see cref="DesignTimeTenantContext"/> per service" anticipated by that type's own doc
/// comment. Building the model (what migrations generation needs) never evaluates the tenant filter's
/// runtime value, so the connection string here is a placeholder; it's never actually connected to for
/// `migrations add`, only for `database update`, which Aspire's real connection string (injected via
/// configuration when run through the host) supersedes in every other startup path.
/// </summary>
public sealed class AccessDbContextFactory : IDesignTimeDbContextFactory<AccessDbContext>
{
    public AccessDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AccessDbContext>()
            .UseSqlServer("Server=localhost;Database=accessdb;Trusted_Connection=True;TrustServerCertificate=True;");

        return new AccessDbContext(optionsBuilder.Options, new DesignTimeTenantContext());
    }
}
