using Xunit;

namespace Volterp.Tests.Integration;

// The tests that raise the API share a host, and two hosts at the same time
// take the harness down: a run with the default parallelism aborted with a
// stack overflow when the extra ApiFactory appeared.
//
// The collection does not run in parallel with any other one, so the tests
// that use the ApiFactory are the only ones alive while they run.
[CollectionDefinition("VolterpApiFactory", DisableParallelization = true)]
public sealed class VolterpApiFactoryCollection;
