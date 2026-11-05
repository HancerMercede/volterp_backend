using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class CodeSequenceRepository(VolterpDbContext context)
    : RepositoryBase<CodeSequence>(context), ICodeSequenceRepository
{
    // The allocation is a single atomic statement: the row is locked for the
    // duration of the statement, so two requests can never take the same number.
    // The company is part of the statement because the sequence belongs to the
    // company, not to the caller.
    private const string NextValueSql = """
        INSERT INTO "CodeSequences" ("Id", "CompanyId", "EntityType", "CurrentValue", "CreatedAt")
        VALUES (@id, @companyId, @entityType, 1, @createdAt)
        ON CONFLICT ("CompanyId", "EntityType")
        DO UPDATE SET "CurrentValue" = "CodeSequences"."CurrentValue" + 1,
                      "UpdatedAt" = @updatedAt
        RETURNING "CurrentValue";
        """;

    public async Task<int> NextValueAsync(Guid companyId, CodeEntityType entityType, CancellationToken ct = default)
    {
        // The allocation may only survive if the insert that carries the code
        // survives too, so it runs inside the transaction of the unit of work.
        // When the caller did not open one, this is the first write of the flow
        // and it opens it: the unit of work commits it with the insert.
        if (context.Database.CurrentTransaction is null)
            await context.Database.BeginTransactionAsync(ct);

        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = NextValueSql;
        command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();

        AddParameter(command, "id", Guid.CreateVersion7());
        AddParameter(command, "companyId", companyId);
        AddParameter(command, "entityType", (int)entityType);
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
