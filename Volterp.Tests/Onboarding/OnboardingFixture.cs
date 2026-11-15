using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Services;
using Volterp.Infrastructure.UnitOfWork;
using Volterp.Tools.Onboarding;

namespace Volterp.Tests.Onboarding;

// The command of the deployment runs against the same services the API uses:
// the company and its administrator are created by the services of the
// application, not by a second implementation of them.
internal sealed class OnboardingFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VolterpDbContext _context;

    public OnboardingFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<VolterpDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new VolterpDbContext(options);

        UnitOfWork = new UnitOfWork(_context);
        CompanyService = new CompanyService(UnitOfWork);
        Hasher = new PasswordHasher();
        UserService = new UserService(UnitOfWork, Hasher, new DeploymentCurrentUserService());
        Onboarding = TenantSeeder.OnboardingOver(UnitOfWork);
    }

    public VolterpDbContext Context => _context;
    public IUnitOfWork UnitOfWork { get; }
    public ICompanyService CompanyService { get; }
    public IUserService UserService { get; }
    public IPasswordHasher Hasher { get; }
    public ITenantOnboarding Onboarding { get; }

    public async Task InitializeAsync() => await _context.Database.EnsureCreatedAsync();

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
