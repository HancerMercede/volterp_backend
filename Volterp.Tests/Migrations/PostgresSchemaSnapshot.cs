using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Volterp.Infrastructure.Data;

namespace Volterp.Tests.Migrations;

internal sealed record PostgresColumnDefinition(
    string Table,
    string Name,
    int Position,
    string DataType,
    string? UdtName,
    string Nullable,
    int? MaxLength,
    int? NumericPrecision,
    int? NumericScale,
    int? DateTimePrecision,
    string? Default);

internal sealed record PostgresIndexDefinition(
    string Table,
    string Name,
    string Definition);

internal sealed record PostgresSchemaSnapshot(
    IReadOnlyList<string> Tables,
    IReadOnlyList<PostgresColumnDefinition> Columns,
    IReadOnlyList<PostgresIndexDefinition> Indexes)
{
    public static async Task<PostgresSchemaSnapshot> CaptureAsync(VolterpDbContext context)
    {
        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        var tables = await ReadTablesAsync(connection);
        var columns = await ReadColumnsAsync(connection);
        var indexes = await ReadIndexesAsync(connection);

        return new PostgresSchemaSnapshot(tables, columns, indexes);
    }

    private static async Task<List<string>> ReadTablesAsync(DbConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_type = 'BASE TABLE'
            ORDER BY table_name
            """;

        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));

        return tables;
    }

    private static async Task<List<PostgresColumnDefinition>> ReadColumnsAsync(DbConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT table_name,
                   column_name,
                   ROW_NUMBER() OVER (
                       PARTITION BY table_name
                       ORDER BY ordinal_position
                   ) AS position,
                   data_type,
                   udt_name,
                   is_nullable,
                   character_maximum_length,
                   numeric_precision,
                   numeric_scale,
                   datetime_precision,
                   column_default
            FROM information_schema.columns
            WHERE table_schema = 'public'
            ORDER BY table_name, ordinal_position
            """;

        var columns = new List<PostgresColumnDefinition>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            columns.Add(new PostgresColumnDefinition(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                NullableInt(reader, 6),
                NullableInt(reader, 7),
                NullableInt(reader, 8),
                NullableInt(reader, 9),
                reader.IsDBNull(10) ? null : reader.GetString(10)));
        }

        return columns;
    }

    private static async Task<List<PostgresIndexDefinition>> ReadIndexesAsync(DbConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT tablename, indexname, indexdef
            FROM pg_indexes
            WHERE schemaname = 'public'
            ORDER BY tablename, indexname
            """;

        var indexes = new List<PostgresIndexDefinition>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            indexes.Add(new PostgresIndexDefinition(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2)));
        }

        return indexes;
    }

    private static int? NullableInt(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
}
