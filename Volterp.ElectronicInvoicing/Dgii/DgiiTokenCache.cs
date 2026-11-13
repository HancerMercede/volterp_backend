using System.Collections.Concurrent;

namespace Volterp.ElectronicInvoicing.Dgii;

// The token of every issuer we authenticated, kept until the DGII says it
// dies. Keyed by issuer RNC: the token belongs to the issuer that signed the
// seed, so two issuers never share one.
//
// This is the only state that has to outlive a request, which is why the
// authenticator can stay transient and never capture a client.
public sealed class DgiiTokenCache(TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    // The token of one issuer and the gate that keeps a single renewal in
    // flight for it.
    private readonly ConcurrentDictionary<string, IssuerTokens> _tokensByIssuer = new(StringComparer.Ordinal);

    // The RNC is the caller's word: the cache does not check it against the
    // certificate, and nobody should read this class as if it did.
    public async Task<DgiiToken> GetOrRenewAsync(
        string issuerRnc,
        Func<CancellationToken, Task<DgiiToken>> renew,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuerRnc);
        ArgumentNullException.ThrowIfNull(renew);

        var issuer = _tokensByIssuer.GetOrAdd(issuerRnc.Trim(), static _ => new IssuerTokens());

        // The fast path: the token we already hold is still alive.
        var cached = issuer.Current;
        if (IsUsable(cached))
            return cached!;

        // The slow path: one renewal at a time per issuer, so two emissions
        // that overlap share the trip instead of making two of them.
        await issuer.Gate.WaitAsync(cancellationToken);
        try
        {
            // The emission that held the gate may have renewed the token while
            // we were waiting for it.
            cached = issuer.Current;
            if (IsUsable(cached))
                return cached!;

            var token = await renew(cancellationToken);
            issuer.Current = token;

            return token;
        }
        finally
        {
            issuer.Gate.Release();
        }
    }

    // A token we cannot date is a token we cannot trust.
    private bool IsUsable(DgiiToken? token)
        => token is not null && token.ExpiresAt is not null && !token.IsExpiredAt(_clock.GetUtcNow());

    /// <summary>
    /// The token of one issuer and the gate that keeps a single renewal in
    /// flight for it.
    /// </summary>
    private sealed class IssuerTokens
    {
        /// <summary>
        /// Written under the gate and read without it, so it is volatile: the
        /// fast path must never read a reference the renewal has not finished
        /// writing.
        /// </summary>
        public volatile DgiiToken? Current;

        public SemaphoreSlim Gate { get; } = new(1, 1);
    }
}
