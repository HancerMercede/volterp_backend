using Volterp.DgiiSimulator;

var builder = WebApplication.CreateBuilder(args);
var options = builder.Configuration.GetSection(DgiiSimulatorOptions.SectionName).Get<DgiiSimulatorOptions>()
              ?? new DgiiSimulatorOptions();

await using var simulator = await DgiiSimulatorApplication.StartAsync(options);

Console.WriteLine($"DGII simulator listening on {simulator.BaseAddress.ToString().TrimEnd('/')}/{options.Environment}");

await simulator.WaitForShutdownAsync();
