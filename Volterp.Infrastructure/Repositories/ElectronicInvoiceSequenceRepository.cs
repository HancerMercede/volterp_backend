using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Xml;

namespace Volterp.Infrastructure.Repositories;

public class ElectronicInvoiceSequenceRepository(VolterpDbContext context)
    : RepositoryBase<ElectronicInvoiceSequence>(context), IElectronicInvoiceSequenceRepository
{
    // The row lock and the increment are one statement: concurrent emissions
    // cannot take the same number, and the returned number is checked against
    // the range before it leaves the statement.
    private const string PostgresNextSequenceSql = """
        WITH "candidate" AS (
            SELECT authorized."Id"
            FROM "ElectronicInvoiceSequences" AS authorized
            WHERE authorized."CompanyId" = @companyId
              AND authorized."DocumentType" = @documentType
              AND authorized."IssuerTaxId" = @issuerTaxId
              AND authorized."From" <= authorized."To"
              AND authorized."CurrentSequence" < authorized."To"
              AND (@expiryApplies = 0 OR authorized."ExpiresAt" >= @expiryCutoff)
            ORDER BY authorized."From", authorized."Id"
            LIMIT 1
            FOR UPDATE
        )
        UPDATE "ElectronicInvoiceSequences" AS sequence
        SET "CurrentSequence" = CASE
                WHEN sequence."CurrentSequence" + 1 < sequence."From" THEN sequence."From"
                ELSE sequence."CurrentSequence" + 1
            END,
            "UpdatedAt" = @updatedAt
        FROM "candidate"
        WHERE sequence."Id" = candidate."Id"
          AND sequence."From" <= sequence."To"
          AND sequence."CurrentSequence" < sequence."To"
          AND (@expiryApplies = 0 OR sequence."ExpiresAt" >= @expiryCutoff)
        RETURNING sequence."CurrentSequence";
        """;

    private const string SqliteNextSequenceSql = """
        UPDATE "ElectronicInvoiceSequences"
        SET "CurrentSequence" = CASE
                WHEN "CurrentSequence" + 1 < "From" THEN "From"
                ELSE "CurrentSequence" + 1
            END,
            "UpdatedAt" = @updatedAt
        WHERE "Id" = (
            SELECT authorized."Id"
            FROM "ElectronicInvoiceSequences" AS authorized
            WHERE authorized."CompanyId" = @companyId
              AND authorized."DocumentType" = @documentType
              AND authorized."IssuerTaxId" = @issuerTaxId
              AND authorized."From" <= authorized."To"
              AND authorized."CurrentSequence" < authorized."To"
              AND (@expiryApplies = 0 OR authorized."ExpiresAt" >= @expiryCutoff)
            ORDER BY authorized."From", authorized."Id"
            LIMIT 1
        )
          AND "From" <= "To"
          AND "CurrentSequence" < "To"
          AND (@expiryApplies = 0 OR "ExpiresAt" >= @expiryCutoff)
        RETURNING "CurrentSequence";
        """;

    private const string RangesSql = """
        SELECT authorized."Id", authorized."From", authorized."To", authorized."ExpiresAt", authorized."CurrentSequence", authorized."IssuerTaxId"
        FROM "ElectronicInvoiceSequences" AS authorized
        WHERE authorized."CompanyId" = @companyId
          AND authorized."DocumentType" = @documentType
          AND authorized."IssuerTaxId" = @issuerTaxId
        ORDER BY authorized."From", authorized."Id";
        """;

    public async Task<int> NextSequenceAsync(Guid companyId, EcfDocumentType documentType, CancellationToken ct = default)
    {
        var issuerTaxId = await context.Set<Company>()
            .AsNoTracking()
            .Where(company => company.Id == companyId)
            .Select(company => company.TaxId)
            .SingleOrDefaultAsync(ct);

        if (issuerTaxId is null)
            throw new InvalidOperationException(
                $"The issuer has no authorized range of e-NCF for the type {(int)documentType}.");

        return await AllocateAsync(companyId, issuerTaxId, documentType, ct);
    }

    public Task<int> NextSequenceAsync(Guid companyId, string issuerTaxId, EcfDocumentType documentType, CancellationToken ct = default)
        => AllocateAsync(companyId, issuerTaxId, documentType, ct);

    public async Task<IReadOnlyList<ElectronicInvoiceSequence>> GetRangesAsync(
        Guid companyId, string issuerTaxId, CancellationToken ct = default)
        => await Set()
            .AsNoTracking()
            .Where(range => range.CompanyId == companyId && range.IssuerTaxId == issuerTaxId)
            .OrderBy(range => range.From)
            .ThenBy(range => range.Id)
            .ToListAsync(ct);

    private async Task<int> AllocateAsync(
        Guid companyId,
        string issuerTaxId,
        EcfDocumentType documentType,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var expiryApplies = EcfTypeProfiles.For(documentType).RequiresSequenceExpiry;
        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
            ? PostgresNextSequenceSql
            : SqliteNextSequenceSql;

        AddParameter(command, "companyId", companyId);
        AddParameter(command, "issuerTaxId", issuerTaxId, DbType.String);
        AddParameter(command, "documentType", (int)documentType);
        AddParameter(command, "expiryApplies", expiryApplies ? 1 : 0);
        AddParameter(command, "expiryCutoff", now.Date);
        AddParameter(command, "updatedAt", now);

        var allocated = await command.ExecuteScalarAsync(ct);

        if (allocated is not null and not DBNull)
            return Convert.ToInt32(allocated);

        throw await NoRangeAsync(companyId, issuerTaxId, documentType, now, expiryApplies, ct);
    }

    private async Task<InvalidOperationException> NoRangeAsync(
        Guid companyId,
        string issuerTaxId,
        EcfDocumentType documentType,
        DateTime now,
        bool expiryApplies,
        CancellationToken ct)
    {
        var ranges = await RangesAsync(companyId, issuerTaxId, documentType, ct);

        if (ranges.Count == 0)
            return new InvalidOperationException(
                $"The issuer has no authorized range of e-NCF for the type {(int)documentType}.");

        var expired = ranges.Where(range => range.IsExpired(now)).ToList();

        if (expired.Count == ranges.Count)
            return new InvalidOperationException(
                $"The authorized range of e-NCF for the type {(int)documentType} expired on {expired.Max(range => range.ExpiresAt!.Value):yyyy-MM-dd}.");

        var usable = ranges
            .Where(range => range.HasRoom
                && !range.IsExpired(now)
                && (!expiryApplies || range.ExpiresAt is not null))
            .ToList();

        if (usable.Count == 0)
        {
            var missingExpiry = ranges.Any(range =>
                range.HasRoom
                && !range.IsExpired(now)
                && expiryApplies
                && range.ExpiresAt is null);

            if (missingExpiry)
                return new InvalidOperationException(
                    $"The authorized range of e-NCF for the type {(int)documentType} has no expiry date.");

            return new InvalidOperationException(
                $"The authorized range of e-NCF for the type {(int)documentType} is exhausted.");
        }

        return new InvalidOperationException(
            $"The authorized range of e-NCF for the type {(int)documentType} is exhausted.");
    }

    private async Task<List<ElectronicInvoiceSequence>> RangesAsync(
        Guid companyId,
        string issuerTaxId,
        EcfDocumentType documentType,
        CancellationToken ct)
    {
        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = RangesSql;

        AddParameter(command, "companyId", companyId);
        AddParameter(command, "issuerTaxId", issuerTaxId, DbType.String);
        AddParameter(command, "documentType", (int)documentType);

        var ranges = new List<ElectronicInvoiceSequence>();

        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            ranges.Add(new ElectronicInvoiceSequence
            {
                Id = reader.GetGuid(0),
                CompanyId = companyId,
                IssuerTaxId = reader.GetString(5),
                DocumentType = documentType,
                From = reader.GetInt32(1),
                To = reader.GetInt32(2),
                ExpiresAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                CurrentSequence = reader.GetInt32(4)
            });
        }

        return ranges;
    }

    private static void AddParameter(DbCommand command, string name, object value, DbType? dbType = null)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;

        if (dbType is not null)
            parameter.DbType = dbType.Value;

        command.Parameters.Add(parameter);
    }
}
