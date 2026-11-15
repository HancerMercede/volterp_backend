using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Volterp.Application.Services;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Services;
using Volterp.Infrastructure.UnitOfWork;
using Volterp.Tools.Onboarding;
using Volterp.Tools.Seeding;

namespace Volterp.Tools;

public static class Program
{
    private const string ConnectionStringKey = "ConnectionStrings:DefaultConnection";

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is not ("create-tenant" or "seed-demo-invoices" or "--help" or "-h"))
        {
            Usage();
            return 2;
        }

        if (args[0] is "--help" or "-h")
        {
            Usage();
            return 0;
        }

        var options = Options.Parse(args[1..]);

        if (args[0] == "seed-demo-invoices")
            return await SeedDemoInvoices(options);

        if (!options.TryGetValue("rnc", out var rnc) ||
            !options.TryGetValue("name", out var name) ||
            !options.TryGetValue("legal-name", out var legalName) ||
            !options.TryGetValue("address", out var address) ||
            !options.TryGetValue("phone", out var phone) ||
            !options.TryGetValue("email", out var email))
        {
            Console.Error.WriteLine("The rnc, the name, the legal-name, the address, the phone and the email are required.");
            Usage();
            return 2;
        }

        var request = new TenantOnboardingRequest(
            name,
            rnc,
            legalName,
            address,
            phone,
            email,
            options.GetValueOrDefault("admin-username", "admin"),
            options.GetValueOrDefault("admin-email", email),
            options.GetValueOrDefault("admin-name", "Administrador"));

        var connectionString = ReadConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine(
                $"There is no connection string. Set {ConnectionStringKey} in appsettings.json or in the environment.");
            return 2;
        }

        var dbOptions = new DbContextOptionsBuilder<VolterpDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        Console.WriteLine($"The tenant will be created in {Target.Describe(connectionString)}.");
        Console.WriteLine($"Company: {request.Name} (RNC {request.TaxId}).");
        Console.WriteLine();

        try
        {
            await using var context = new VolterpDbContext(dbOptions);
            var unitOfWork = new UnitOfWork(context);

            var onboarding = new TenantOnboardingService(
                unitOfWork,
                new CompanyService(unitOfWork),
                new UserService(unitOfWork, new PasswordHasher(), new DeploymentCurrentUserService()),
                new PasswordGenerator());

            var result = await onboarding.RunAsync(request);

            return result.Match(
                error =>
                {
                    Console.Error.WriteLine($"The tenant was not created: {error.Message}");
                    return 1;
                },
                outcome =>
                {
                    Report(outcome);
                    return 0;
                });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"The tenant was not created: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> SeedDemoInvoices(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("rnc", out var taxId) || string.IsNullOrWhiteSpace(taxId))
        {
            Console.Error.WriteLine("The rnc is required.");
            Usage();
            return 2;
        }

        var connectionString = ReadConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine(
                $"There is no connection string. Set {ConnectionStringKey} in appsettings.json or in the environment.");
            return 2;
        }

        var dbOptions = new DbContextOptionsBuilder<VolterpDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        Console.WriteLine($"The demo invoices will be created in {Target.Describe(connectionString)}.");
        Console.WriteLine();

        try
        {
            await using var context = new VolterpDbContext(dbOptions);
            var seeder = new DemoInvoiceSeeder(new UnitOfWork(context), context);

            var result = await seeder.SeedAsync(taxId);

            return result.Match(
                error =>
                {
                    Console.Error.WriteLine($"The demo invoices were not seeded: {error.Message}");
                    return 1;
                },
                outcome =>
                {
                    ReportDemoInvoices(outcome);
                    return 0;
                });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"The demo invoices were not seeded: {ex.Message}");
            return 1;
        }
    }

    private static string? ReadConnectionString()
        => new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build()[ConnectionStringKey];

    private static void ReportDemoInvoices(DemoInvoiceSeedOutcome outcome)
    {
        Console.WriteLine(
            $"Demo invoices created: {outcome.CreatedCount}; already there: {outcome.ExistingCount}.");
        Console.WriteLine(
            $"Demo invoices by state: pending emission={outcome.PendingEmissionCount}, in process={outcome.InProcessCount}, rejected={outcome.RejectedCount}, requires review={outcome.RequiresReviewCount}.");
    }

    private static void Report(TenantOnboardingOutcome outcome)
    {
        if (outcome.CompanyAlreadyExisted && outcome.AdminPassword is null)
        {
            Console.WriteLine(
                $"The company {outcome.CompanyName} (RNC {outcome.TaxId}) already exists and it has its administrator: nothing was created.");
            return;
        }

        if (outcome.CompanyAlreadyExisted)
        {
            Console.WriteLine(
                $"The company {outcome.CompanyName} (RNC {outcome.TaxId}) already existed and it had no administrator: its first administrator was created.");
        }
        else
        {
            Console.WriteLine($"The company {outcome.CompanyName} (RNC {outcome.TaxId}) was created.");
        }

        Console.WriteLine($"The administrator {outcome.AdminUsername} was created.");
        Console.WriteLine();
        Console.WriteLine($"Password: {outcome.AdminPassword}");
        Console.WriteLine("It is shown once and it is not stored anywhere else: write it down now.");
    }

    private static void Usage()
    {
        Console.WriteLine("Volterp.Tools - the deployment commands of the ERP.");
        Console.WriteLine();
        Console.WriteLine("  create-tenant --rnc <rnc> --name <name> --legal-name <legal name> --address <address>");
        Console.WriteLine("                --phone <phone> --email <email>");
        Console.WriteLine("                [--admin-username <username>] [--admin-email <email>] [--admin-name <name>]");
        Console.WriteLine();
        Console.WriteLine("  seed-demo-invoices --rnc <rnc>");
        Console.WriteLine();
        Console.WriteLine("The connection string comes from ConnectionStrings:DefaultConnection, in appsettings.json");
        Console.WriteLine("or in the environment (ConnectionStrings__DefaultConnection).");
    }
}
