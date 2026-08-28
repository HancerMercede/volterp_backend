using System.Threading.RateLimiting;
using FluentAssertions;
using Xunit;

namespace Volterp.Tests.RateLimiting;

public class RateLimiterBehaviorTests
{
    [Fact]
    public async Task FixedWindowLimiter_RejectsAfterPermitLimit()
    {
        using var limiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = 2,
            Window = TimeSpan.FromSeconds(60),
            QueueLimit = 0,
            AutoReplenishment = false
        });

        (await limiter.AcquireAsync()).IsAcquired.Should().BeTrue();
        (await limiter.AcquireAsync()).IsAcquired.Should().BeTrue();
        (await limiter.AcquireAsync()).IsAcquired.Should().BeFalse();
    }

    [Fact]
    public async Task FixedWindowLimiter_RejectedLease_ExposesRetryAfterMetadata()
    {
        using var limiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = 1,
            Window = TimeSpan.FromSeconds(60),
            QueueLimit = 0,
            AutoReplenishment = false
        });

        (await limiter.AcquireAsync()).IsAcquired.Should().BeTrue();
        var rejected = await limiter.AcquireAsync();

        rejected.IsAcquired.Should().BeFalse();
        rejected.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter).Should().BeTrue();
        retryAfter.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task SlidingWindowLimiter_RejectsAfterPermitLimit()
    {
        using var limiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 2,
            Window = TimeSpan.FromSeconds(60),
            SegmentsPerWindow = 6,
            QueueLimit = 0,
            AutoReplenishment = false
        });

        (await limiter.AcquireAsync()).IsAcquired.Should().BeTrue();
        (await limiter.AcquireAsync()).IsAcquired.Should().BeTrue();
        (await limiter.AcquireAsync()).IsAcquired.Should().BeFalse();
    }
}