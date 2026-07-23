using Microsoft.EntityFrameworkCore;
using PlannerPro.Shared.Persistence;

namespace PlannerPro.Shared.Tests.TestSupport;

internal sealed class TestDbContext(DbContextOptions<TestDbContext> options) : SharedDbContext(options);
