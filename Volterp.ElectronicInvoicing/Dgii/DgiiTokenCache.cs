using System.Collections.Concurrent;

namespace Volterp.ElectronicInvoicing.Dgii;

/// <summary>
/// The token of every issuer we have authenticated, kept until the DGII says
/// it dies.
/// </summary>
/// <remarks>
/// The token belongs to the issuer whose certificate signed the seed, so the
/// cache is keyed by the issuer RNC: two issuers never share a token. The DGII
/// says how long the token lives and until then the round trip is not repeated
/// for every emission.
/// <para>
/// This is the only state of the authentication that has to outlive a request,
/// and the reason the authenticator can stay transient: the behaviour (seed,
/// sign, token) is cheap to rebuild and must not capture a client.
/// </para>
/// </remarks>
public sealed class DgiiTokenCache(TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>
    /// What we hold for every issuer we have authenticated: its token and the
    /// gate that keeps a single renewal in flight for that issuer.
    /// </summary>
    private readonly ConcurrentDictionary<string, IssuerTokens> _tokensByIssuer = new(StringComparer.Ordinal);

    /// <summary>
    /// Returns the live token of the issuer, trading a new one for it when
    /// there is none.
    /// </summary>
    /// <param name="issuerRnc">
    /// The RNC of the issuer the token belongs to. The cache takes the
    /// caller's word for it: it does not check the RNC against the
    /// certificate, and nobody should read this class as if it did.
    /// </param>
    /// <param name="renew">
    /// The round trip that asks the DGII for a new token. The cache calls it
    /// only when it has no live token, and only once per issuer at a time.
    /// </param>
    /// <param name="cancellationToken">Cancels the round trip.</param>
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

    /// <summary>
    /// A token is usable only when the DGII said when it dies and that moment
    /// has not passed: a token we cannot date is a token we cannot trust.
    /// </summary>
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
