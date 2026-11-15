using Volterp.DgiiSimulator;
using Xunit;

namespace Volterp.DgiiSimulator.Tests;

public sealed class DgiiSimulatorFixture : IAsyncLifetime
{
    private DgiiSimulatorApplication? _simulator;

    public HttpClient Client { get; private set; } = null!;

    public DgiiSimulatorApplication Simulator => _simulator!;

    public async Task InitializeAsync()
    {
        _simulator = await DgiiSimulatorApplication.StartAsync(new DgiiSimulatorOptions
        {
            BaseUrl = "http://127.0.0.1:0",
            Environment = "testecf"
        });

        Client = new HttpClient { BaseAddress = Simulator.BaseAddress };
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();

        if (_simulator is not null)
            await _simulator.DisposeAsync();
    }
}
