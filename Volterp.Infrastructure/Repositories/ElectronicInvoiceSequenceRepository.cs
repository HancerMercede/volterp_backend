using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;
using Volterp.ElectronicInvoicing;

namespace Volterp.Infrastructure.Repositories;

public class ElectronicInvoiceSequenceRepository(VolterpDbContext context)
    : RepositoryBase<ElectronicInvoiceSequence>(context), IElectronicInvoiceSequenceRepository
{
    // The allocation is a single atomic statement: the row is locked for the
    // duration of the statement, so two issuers can never take the same number.
    // The company is part of the statement because the sequence belongs to the
    // issuer, not to the caller.
    private const string NextSequenceSql = """
        INSERT INTO "ElectronicInvoiceSequences" ("Id", "CompanyId", "DocumentType", "CurrentSequence", "CreatedAt")
        VALUES (@id, @companyId, @documentType, 1, @createdAt)
        ON CONFLICT ("CompanyId", "DocumentType")
        DO UPDATE SET "CurrentSequence" = "ElectronicInvoiceSequences"."CurrentSequence" + 1,
                      "UpdatedAt" = @updatedAt
        RETURNING "CurrentSequence";
        """;

    public async Task<int> NextSequenceAsync(Guid companyId, EcfDocumentType documentType, CancellationToken ct = default)
    {
        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = NextSequenceSql;

        AddParameter(command, "id", Guid.CreateVersion7());
        AddParameter(command, "companyId", companyId);
        AddParameter(command, "documentType", (int)documentType);
        AddParameter(command, "createdAt", DateTime.UtcNow);
        AddParameter(command, "updatedAt", DateTime.UtcNow);

        var allocated = await command.ExecuteScalarAsync(ct);

        return Convert.ToInt32(allocated);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
