namespace Volterp.ElectronicInvoicing.Dgii;

/// <summary>
/// The DGII web services an electronic issuer talks to.
/// </summary>
public interface IDgiiClient
{
    /// <summary>
    /// Gets the seed the DGII hands out. It has to be signed with the issuer's
    /// certificate before it is traded for a token.
    /// </summary>
    Task<string> GetSeedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Trades a signed seed for a token. The token authorizes every later call.
    /// </summary>
    Task<DgiiToken> RequestTokenAsync(string signedSeedXml, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a signed e-CF and gets back the track id that identifies it.
    /// </summary>
    Task<DgiiReceipt> SendElectronicInvoiceAsync(string signedEcfXml, string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks for the validation verdict of a received e-CF.
    /// </summary>
    Task<DgiiStatus> GetStatusAsync(string trackId, string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the DGII whether it received a document, by its issuer RNC and its
    /// e-NCF. It is how a lost answer is recovered without sending twice.
    /// </summary>
    Task<DgiiTrackLookup> GetTrackIdAsync(string issuerTaxId, string encf, string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Announces to the DGII the ranges of e-NCF that will not be used, with
    /// the signed document of the annulment.
    /// </summary>
    // The default implementation keeps the clients that only carry e-CF alive:
    // the ones that annul ranges of e-NCF implement it.
    Task<DgiiRangeAnnulmentAnswer> AnnulRangesAsync(string signedAnecfXml, string token, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This client cannot annul ranges of e-NCF.");
}
