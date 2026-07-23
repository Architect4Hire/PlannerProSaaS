using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlannerPro.Portfolio.Core.Business;
using PlannerPro.Portfolio.Core.Data;
using PlannerPro.Portfolio.Core.Facade;
using PlannerPro.Shared;

namespace PlannerPro.Portfolio.Core;

/// <summary>
/// Registration lives in <c>.Core</c>, not the host (`.claude/rules/backend.md`) — mirrors
/// <c>AccessCoreServiceCollectionExtensions.AddAccessCore</c> exactly, including the same
/// deliberately-plain <c>AddDbContext</c> (never the pooled Aspire SQL Server integration — see that
/// type's remarks for why pooling is incompatible with this system's per-request/per-consumer-scope
/// <c>ITenantContext</c> design).
/// </summary>
public static class PortfolioCoreServiceCollectionExtensions
{
    public static IServiceCollection AddPortfolioCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSharedTenancy();
        services.AddSharedPersistence<PortfolioDbContext>();

        services.AddDbContext<PortfolioDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("portfoliodb")));

        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<ITenantProvisionedDataLayer, TenantProvisionedDataLayer>();
        services.AddScoped<ITenantProvisionedBusiness, TenantProvisionedBusiness>();
        services.AddScoped<IClientFacade, ClientFacade>();

        return services;
    }
}
