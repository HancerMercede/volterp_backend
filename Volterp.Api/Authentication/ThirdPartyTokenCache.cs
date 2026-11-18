using System.Collections.Concurrent;
using Volterp.Application.DTOs.SeedDtos;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Authentication;

// The token of every third party that validated a seed. It is keyed by the
// subject of the certificate that signed the seed: the token belongs to the
// signer and not to a tenant of ours.
//
// It is the session of the third party and it is kept until it expires, so a
// third party that validates several seeds does not collect a token for each
// of them. Only one issue travels at a time for each signer: two validations
// that overlap share the token instead of making two of them.
public sealed class ThirdPartyTokenCache(TimeProvider? clock = null) : ISeedTokenCache
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    private readonly ConcurrentDictionary<string, SignerSession> _sessionsBySigner = new(StringComparer.Ordinal);

    public async Task<SeedSession> GetOrIssueAsync(
        string subjectName,
        Func<SeedSession> issue,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectName);
        ArgumentNullException.ThrowIfNull(issue);

        var signer = _sessionsBySigner.GetOrAdd(subjectName.Trim(), static _ => new SignerSession());

        // The fast path: the token we already hold is still alive.
        var cached = signer.Current;
        if (IsUsable(cached))
            return cached!;

        // The slow path: one issue at a time per signer.
        await signer.Gate.WaitAsync(ct);
        try
        {
            // The validation that held the gate may have issued the token
            // while we were waiting for it.
            cached = signer.Current;
            if (IsUsable(cached))
                return cached!;

            var session = issue();
            signer.Current = session;

            return session;
        }
        finally
        {
            signer.Gate.Release();
        }
    }

    // A token we cannot date is a token we cannot trust.
    private bool IsUsable(SeedSession? session)
        => session is not null && session.ExpiresAt > _clock.GetUtcNow();

    // The session of one signer and the gate that keeps a single issue in
    // flight for it.
    private sealed class SignerSession
    {
        /// <summary>
        /// Written under the gate and read without it, so it is volatile: the
        /// fast path must never read a reference the issue has not finished
        /// writing.
        /// </summary>
        public volatile SeedSession? Current;

        public SemaphoreSlim Gate { get; } = new(1, 1);
    }
}
