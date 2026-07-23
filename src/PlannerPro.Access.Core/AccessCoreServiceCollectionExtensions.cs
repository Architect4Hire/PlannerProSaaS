using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlannerPro.Access.Core.Business;
using PlannerPro.Access.Core.Data;
using PlannerPro.Access.Core.Facade;
using PlannerPro.Access.Core.Managers.Models.Domain;
using PlannerPro.Access.Core.Managers.Models.ViewModels;
using PlannerPro.Access.Core.Managers.Validators;
using PlannerPro.Shared;

namespace PlannerPro.Access.Core;

/// <summary>
/// Registration lives in <c>.Core</c>, not the host (`.claude/rules/backend.md`) — this is the single
/// call the thin <c>PlannerPro.Access</c> host makes to get every layer wired: tenancy + persistence
/// mechanism from <c>Shared</c>, the <c>AccessDbContext</c> itself, ASP.NET Core Identity for the
/// global <see cref="ApplicationUser"/> slice, and every facade/business/data-layer/repository pair
/// this service currently has.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately NOT registered via the <c>Aspire.Microsoft.EntityFrameworkCore.SqlServer</c>
/// package's <c>builder.AddSqlServerDbContext&lt;TContext&gt;(...)</c></b>, even though that's what
/// `.claude/rules/backend.md` names as the sanctioned pattern. That integration pools DbContext
/// instances **unconditionally**, with no supported way to opt out (confirmed against
/// <c>dotnet/aspire</c> issue #7023, requesting exactly this, closed as "not planned"). Pooling is
/// flatly incompatible with this system's tenancy mechanism: a pooled context is constructed once
/// against the ROOT provider and reused across scopes, so it can never see the per-request scoped
/// <c>ITenantContext</c> the automatic query filter and <c>TenantSaveChangesInterceptor</c> both
/// depend on — exactly what <see cref="SharedServiceCollectionExtensions.AddSharedTenancy"/>'s own
/// remarks already warned against ("never <c>AddDbContextPool</c>"), now confirmed as a hard
/// constraint rather than a style preference.
/// </para>
/// <para>
/// The connection string is still Aspire-injected, not hardcoded: the AppHost's
/// <c>sql.AddDatabase("accessdb")</c> plus the project's <c>WithReference(accessDb)</c> populate
/// <c>ConnectionStrings:accessdb</c> in configuration exactly as the Aspire integration would have
/// read it internally — this reads that same configuration value directly via plain,
/// non-pooled <c>AddDbContext</c> instead. <see cref="Persistence.SharedDbContext.OnConfiguring"/>
/// wires <c>TenantSaveChangesInterceptor</c> itself from each context instance's own injected
/// <c>ITenantContext</c>, so nothing needs adding here beyond the connection.
/// </para>
/// </remarks>
public static class AccessCoreServiceCollectionExtensions
{
    public static IServiceCollection AddAccessCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSharedTenancy();
        services.AddSharedPersistence<AccessDbContext>();

        services.AddDbContext<AccessDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("accessdb")));

        services.AddHttpContextAccessor();
        services.AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = true)
            .AddEntityFrameworkStores<AccessDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddScoped<IValidator<LoginViewModel>, LoginViewModelValidator>();

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantMembershipRepository, TenantMembershipRepository>();
        services.AddScoped<ITenantResolutionDataLayer, TenantResolutionDataLayer>();
        services.AddScoped<ITenantResolutionBusiness, TenantResolutionBusiness>();
        services.AddScoped<ITenantResolutionFacade, TenantResolutionFacade>();

        services.AddScoped<IAuthBusiness, AuthBusiness>();
        services.AddScoped<IAuthFacade, AuthFacade>();

        return services;
    }
}
