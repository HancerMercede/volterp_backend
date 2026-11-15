using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class NormalFiscalSequenceRepository(VolterpDbContext context)
    : RepositoryBase<NormalFiscalSequence>(context), INormalFiscalSequenceRepository
{
    // The row lock and the increment are one statement: two paper documents
    // of the same issuer cannot take the same number, and the number that
    // comes back is checked against the range before it leaves the statement.
    private const string PostgresNextSequenceSql = """
        WITH "candidate" AS (
            SELECT authorized."Id"
            FROM "NormalFiscalSequences" AS authorized
            WHERE authorized."CompanyId" = @companyId
              AND authorized."DocumentType" = @documentType
              AND authorized."IssuerTaxId" = @issuerTaxId
              AND authorized."From" <= authorized."To"
              AND authorized."From" >= 1
              AND authorized."To" <= @maxSequence
              AND authorized."CurrentSequence" < authorized."To"
              AND (@expiryApplies = 0 OR authorized."ExpiresAt" >= @expiryCutoff)
            ORDER BY authorized."From", authorized."Id"
            LIMIT 1
            FOR UPDATE
        )
        UPDATE "NormalFiscalSequences" AS sequence
        SET "CurrentSequence" = CASE
                WHEN sequence."CurrentSequence" + 1 < sequence."From" THEN sequence."From"
                ELSE sequence."CurrentSequence" + 1
            END,
            "UpdatedAt" = @updatedAt
        FROM "candidate"
        WHERE sequence."Id" = candidate."Id"
          AND sequence."From" <= sequence."To"
          AND sequence."From" >= 1
          AND sequence."To" <= @maxSequence
          AND sequence."CurrentSequence" < sequence."To"
          AND (@expiryApplies = 0 OR sequence."ExpiresAt" >= @expiryCutoff)
        RETURNING sequence."CurrentSequence";
        """;

    private const string SqliteNextSequenceSql = """
        UPDATE "NormalFiscalSequences"
        SET "CurrentSequence" = CASE
                WHEN "CurrentSequence" + 1 < "From" THEN "From"
                ELSE "CurrentSequence" + 1
            END,
            "UpdatedAt" = @updatedAt
        WHERE "Id" = (
            SELECT authorized."Id"
            FROM "NormalFiscalSequences" AS authorized
            WHERE authorized."CompanyId" = @companyId
              AND authorized."DocumentType" = @documentType
              AND authorized."IssuerTaxId" = @issuerTaxId
              AND authorized."From" <= authorized."To"
              AND authorized."From" >= 1
              AND authorized."To" <= @maxSequence
              AND authorized."CurrentSequence" < authorized."To"
              AND (@expiryApplies = 0 OR authorized."ExpiresAt" >= @expiryCutoff)
            ORDER BY authorized."From", authorized."Id"
            LIMIT 1
        )
          AND "From" <= "To"
          AND "From" >= 1
          AND "To" <= @maxSequence
          AND "CurrentSequence" < "To"
          AND (@expiryApplies = 0 OR "ExpiresAt" >= @expiryCutoff)
        RETURNING "CurrentSequence";
        """;

    private const string RangesSql = """
        SELECT authorized."Id", authorized."From", authorized."To", authorized."ExpiresAt", authorized."CurrentSequence", authorized."IssuerTaxId"
        FROM "NormalFiscalSequences" AS authorized
        WHERE authorized."CompanyId" = @companyId
          AND authorized."DocumentType" = @documentType
          AND authorized."IssuerTaxId" = @issuerTaxId
        ORDER BY authorized."From", authorized."Id";
        """;

    public async Task<int> NextSequenceAsync(
        Guid companyId,
        string issuerTaxId,
        NormalFiscalDocumentType documentType,
        CancellationToken ct = default)
    {
        // A number that leaves the transaction burns a number with no document
        // behind it: the allocation only happens inside the transaction that
        // persists the document.
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "The number of a normal document can only be taken inside the transaction that persists it.");

        var now = DateTime.UtcNow;
        var expiryApplies = NormalFiscalDocumentTypes.RequiresExpiry(documentType);
        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
            ? PostgresNextSequenceSql
            : SqliteNextSequenceSql;

        // The number only survives if the document that carries it survives:
        // the statement runs inside the transaction of the unit of work.
        command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();

        AddParameter(command, "companyId", companyId);
        AddParameter(command, "issuerTaxId", issuerTaxId, DbType.String);
        AddParameter(command, "documentType", (int)documentType);
        AddParameter(command, "expiryApplies", expiryApplies ? 1 : 0);
        AddParameter(command, "expiryCutoff", now.Date);
        AddParameter(command, "maxSequence", NormalFiscalSequence.MaxSequence);
        AddParameter(command, "updatedAt", now);

        var allocated = await command.ExecuteScalarAsync(ct);

        if (allocated is not null and not DBNull)
            return Convert.ToInt32(allocated);

        throw await NoRangeAsync(companyId, issuerTaxId, documentType, now, expiryApplies, ct);
    }

    private async Task<InvalidOperationException> NoRangeAsync(
        Guid companyId,
        string issuerTaxId,
        NormalFiscalDocumentType documentType,
        DateTime now,
        bool expiryApplies,
        CancellationToken ct)
    {
        var ranges = await RangesAsync(companyId, issuerTaxId, documentType, ct);

        if (ranges.Count == 0)
            return new InvalidOperationException(
                $"The issuer has no authorized range of normal documents for the type {(int)documentType}.");

        // A range that goes beyond the eight digits of the number cannot be
        // used: the number would not fit the format of a normal document.
        var outOfBounds = ranges.Where(range => !range.HasValidBounds).ToList();

        if (outOfBounds.Count == ranges.Count)
            return new InvalidOperationException(
                $"The authorized range of normal documents for the type {(int)documentType} goes beyond the maximum sequence a normal document can carry ({NormalFiscalSequence.MaxSequence}).");

        var expired = ranges.Where(range => range.IsExpired(now)).ToList();

        if (expired.Count == ranges.Count)
            return new InvalidOperationException(
                $"The authorized range of normal documents for the type {(int)documentType} expired on {expired.Max(range => range.ExpiresAt!.Value):yyyy-MM-dd}.");

        var usable = ranges
            .Where(range => range.HasRoom
                && range.HasValidBounds
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
                    $"The authorized range of normal documents for the type {(int)documentType} has no expiry date.");

            return new InvalidOperationException(
                $"The authorized range of normal documents for the type {(int)documentType} is exhausted.");
        }

        return new InvalidOperationException(
            $"The authorized range of normal documents for the type {(int)documentType} is exhausted.");
    }

    private async Task<List<NormalFiscalSequence>> RangesAsync(
        Guid companyId,
        string issuerTaxId,
        NormalFiscalDocumentType documentType,
        CancellationToken ct)
    {
        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = RangesSql;
        command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();

        AddParameter(command, "companyId", companyId);
        AddParameter(command, "issuerTaxId", issuerTaxId, DbType.String);
        AddParameter(command, "documentType", (int)documentType);

        var ranges = new List<NormalFiscalSequence>();

        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            ranges.Add(new NormalFiscalSequence
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
