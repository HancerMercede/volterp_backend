using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using EitherWay;
using Volterp.Application.DTOs.SeedDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing.Abstractions;

namespace Volterp.Application.Services;

// The seed of the family "Comunicación Emisor-Receptor" of the DGII: the
// session a third party opens against this platform. The seed is issued and
// kept, and when it comes back signed it is checked to be one of ours, alive
// and unused before a token is handed out.
//
// The token says that the seed was ours. It is not a credential of ours: the
// reception of an e-CF keeps demanding the certificate of the client.
public class SeedAuthenticationService(
    IIssuedSeedRepository seeds,
    ISeedTokenCache tokenCache,
    IEcfSigner signer,
    TimeProvider clock) : ISeedAuthenticationService
{
    // The seed is signed and returned right away, so it lives minutes and
    // not days.
    private static readonly TimeSpan SeedLifetime = TimeSpan.FromMinutes(5);

    // The token lives an hour, which is what the DGII grants (p. 9 of the
    // "Descripción Técnica Servicios DGII").
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    public async Task<Either<Error, SeedDto>> IssueSeedAsync(CancellationToken ct = default)
    {
        var issuedAt = clock.GetUtcNow();

        var seed = new IssuedSeed
        {
            Id = Guid.CreateVersion7(),
            Value = Guid.NewGuid().ToString(),
            IssuedAt = issuedAt.UtcDateTime,
            ExpiresAt = issuedAt.Add(SeedLifetime).UtcDateTime
        };

        return await EitherAsync<Error, IssuedSeed>
            .FromRight(seed)
            .FlatMap(
                async issued =>
                {
                    await seeds.AddAsync(issued, ct);
                    await seeds.CommitAsync(ct);
                    return issued;
                },
                exception => new Error(exception.Message))
            .Map(issued => new SeedDto(issued.Value, issuedAt))
            .Run();
    }

    public async Task<Either<Error, SeedValidationDto>> ValidateAsync(
        string xml,
        CancellationToken ct = default)
    {
        return await EitherAsync<Error, string>
            .FromRight(xml)
            .Ensure(value => !string.IsNullOrWhiteSpace(value), new Error("The XML document is required."))
            .FlatMap(
                async value => await ValidateTheSeedAsync(value, ct),
                exception => new Error(exception.Message))
            .Map(session => new SeedValidationDto(
                session.Token,
                Format(session.ExpiresAt),
                Format(session.IssuedAt)))
            .Run();
    }

    private async Task<SeedSession> ValidateTheSeedAsync(string xml, CancellationToken ct)
    {
        var document = LoadSecureXml(xml);

        // The signature is checked against the certificate that travels
        // inside the document: that certificate is the one of the signer, and
        // nobody else could have produced the signature.
        var certificate = EmbeddedCertificate(document);
        if (certificate is null)
            throw new InvalidOperationException("The document does not carry the certificate of the signer.");

        if (!signer.Verify(xml, certificate))
            throw new InvalidOperationException("The signature of the document is not valid.");

        var value = SeedValue(document);
        if (value is null)
            throw new InvalidOperationException("The document does not carry the seed.");

        var seed = await seeds.FindByValueAsync(value, ct);
        if (seed is null)
            throw new InvalidOperationException("The seed was not issued by this platform.");

        var now = clock.GetUtcNow().UtcDateTime;

        if (seed.IsExpired(now))
            throw new InvalidOperationException("The seed expired.");

        if (!await seeds.TryConsumeAsync(seed.Id, now, ct))
            throw new InvalidOperationException("The seed was already used.");

        var subject = SubjectOf(certificate);

        return await tokenCache.GetOrIssueAsync(subject, () => NewSession(subject), ct);
    }

    private SeedSession NewSession(string subjectName)
    {
        var issuedAt = clock.GetUtcNow();

        return new SeedSession(
            subjectName,
            NewToken(),
            issuedAt,
            issuedAt.Add(TokenLifetime));
    }

    private static string NewToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);

        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static XmlDocument LoadSecureXml(string xml)
    {
        try
        {
            using var reader = XmlReader.Create(
                new StringReader(xml),
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersFromEntities = 0
                });

            var document = new XmlDocument { XmlResolver = null, PreserveWhitespace = false };
            document.Load(reader);

            return document;
        }
        catch (XmlException exception)
        {
            throw new InvalidOperationException("The XML document is malformed.", exception);
        }
    }

    private static X509Certificate2? EmbeddedCertificate(XmlDocument document)
    {
        var signature = document.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl);
        if (signature.Count != 1)
            return null;

        var signedXml = new SignedXml(document);

        try
        {
            signedXml.LoadXml((XmlElement)signature[0]!);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }

        return signedXml.KeyInfo?
            .OfType<KeyInfoX509Data>()
            .SelectMany(data => data.Certificates?.OfType<X509Certificate2>() ?? [])
            .FirstOrDefault();
    }

    private static string? SeedValue(XmlDocument document)
    {
        var node = document.SelectSingleNode("//*[local-name()='valor']");
        var value = node?.InnerText.Trim();

        return string.IsNullOrEmpty(value) ? null : value;
    }

    // The certificates the DGII mandates belong to the person that signs for
    // the taxpayer, and the only thing the sources say about their content is
    // that the SN of the subject is the id of the owner (p. 13 of the
    // "Descripción Técnica Emisores Electrónicos"). The delegation of that
    // person to a company lives in the registry of the DGII, which is not
    // ours, so the token is bound to the signer and to nothing else.
    private static string SubjectOf(X509Certificate2 certificate)
    {
        foreach (var part in certificate.Subject.Split(','))
        {
            var trimmed = part.Trim();

            if (!trimmed.StartsWith("SN=", StringComparison.OrdinalIgnoreCase))
                continue;

            var subjectName = trimmed[3..];
            if (!string.IsNullOrWhiteSpace(subjectName))
                return subjectName;
        }

        return certificate.Subject;
    }

    private static string Format(DateTimeOffset value)
        => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
