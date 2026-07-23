using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlannerPro.Access.Core.Data;
using PlannerPro.Access.Core.Managers.Models.Domain;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Access.Tests.TestSupport;

/// <summary>
/// Wires the same <c>UserOnlyStore&lt;ApplicationUser, AccessDbContext, Guid&gt;</c> stack
/// <c>AddAccessCore</c> registers in production, against a Sqlite-backed <see cref="AccessDbContext"/>
/// instead of the real <c>accessdb</c> — enough to exercise <c>UserManager</c>/<c>SignInManager</c>
/// for real without a live SQL Server.
/// </summary>
internal sealed class IdentityTestHarness : IDisposable
{
    private readonly SqliteAccessDbContextFactory _dbFactory = new();
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    public IdentityTestHarness()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddAuthentication();

        services.AddSingleton<ITenantContext>(StaticTenantContext.Bypass);

        // No .AddInterceptors(...) here — AccessDbContext's base (SharedDbContext) wires
        // TenantSaveChangesInterceptor itself from the ITenantContext registered just above.
        services.AddDbContext<AccessDbContext>(options => options.UseSqlite(_dbFactory.Connection));

        services.AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = true)
            .AddEntityFrameworkStores<AccessDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _scope.ServiceProvider.GetRequiredService<AccessDbContext>().Database.EnsureCreated();
    }

    public UserManager<ApplicationUser> UserManager => _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    public SignInManager<ApplicationUser> SignInManager => _scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _dbFactory.Dispose();
    }
}
