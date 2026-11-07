using Npgsql;

namespace Klowee.ContentSync;

/// <summary>A table's live rows as read from the source, column names and types included.</summary>
public sealed record TableRows(IReadOnlyList<string> Columns, IReadOnlyList<string> DataTypes, List<object?[]> Rows)
{
    public int IndexOf(string column) => Columns.ToList().IndexOf(column);
}

/// <summary>
/// Plain Npgsql over the content tables. Table names only ever come from
/// <see cref="ContentTables"/>, never from input, so they are safe to put in
/// SQL. Nothing here creates, alters or migrates anything.
/// </summary>
public static class ContentDatabase
{
    public static async Task<NpgsqlConnection> OpenAsync(Endpoint endpoint, bool readOnly, CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(endpoint.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException or System.Net.Sockets.SocketException)
        {
            await connection.DisposeAsync();
            throw new SettingsException(
                $"Cannot connect to the {endpoint.Name} database (project {endpoint.ProjectRef}, host {endpoint.DbHost}): " +
                $"{ex.GetType().Name}. If the Supabase project is paused, restore it from the dashboard first.");
        }

        if (readOnly)
        {
            // Belt and braces: a read-only session cannot write even by mistake.
            await using var command = new NpgsqlCommand("SET SESSION CHARACTERISTICS AS TRANSACTION READ ONLY", connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return connection;
    }

    public static async Task<TableRows> ReadLiveRowsAsync(NpgsqlConnection connection, string table, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"SELECT * FROM \"{table}\" WHERE deleted_at IS NULL ORDER BY id", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
        var types = Enumerable.Range(0, reader.FieldCount).Select(reader.GetDataTypeName).ToList();
        var rows = new List<object?[]>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++)
            {
                row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return new TableRows(columns, types, rows);
    }

    /// <summary>Every row, soft-deleted included: what an apply would delete.</summary>
    public static Task<long> CountAllAsync(NpgsqlConnection connection, string table, CancellationToken cancellationToken) =>
        ScalarAsync(connection, $"SELECT count(*) FROM \"{table}\"", cancellationToken);

    public static Task<long> CountSoftDeletedAsync(NpgsqlConnection connection, string table, CancellationToken cancellationToken) =>
        ScalarAsync(connection, $"SELECT count(*) FROM \"{table}\" WHERE deleted_at IS NOT NULL", cancellationToken);

    public static async Task<HashSet<Guid>> IdsAsync(NpgsqlConnection connection, string table, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"SELECT id FROM \"{table}\"", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var ids = new HashSet<Guid>();
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    /// <summary>The destination's columns, to prove both schemas match before anything is written.</summary>
    public static async Task<List<string>> ColumnsAsync(NpgsqlConnection connection, string table, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @t ORDER BY column_name",
            connection);
        command.Parameters.AddWithValue("t", table);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    public static async Task<int> DeleteAllAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string table, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"DELETE FROM \"{table}\"", connection, transaction);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Parameterised inserts, batched; each value typed with its source column's type.</summary>
    public static async Task<int> InsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string table,
        TableRows rows,
        CancellationToken cancellationToken)
    {
        if (rows.Rows.Count == 0)
        {
            return 0;
        }

        var columnList = string.Join(", ", rows.Columns.Select(column => $"\"{column}\""));
        var placeholders = string.Join(", ", rows.Columns.Select((_, i) => $"${i + 1}"));
        var sql = $"INSERT INTO \"{table}\" ({columnList}) VALUES ({placeholders})";

        var inserted = 0;
        foreach (var chunk in rows.Rows.Chunk(200))
        {
            await using var batch = new NpgsqlBatch(connection, transaction);
            foreach (var row in chunk)
            {
                var command = new NpgsqlBatchCommand(sql);
                for (var i = 0; i < row.Length; i++)
                {
                    command.Parameters.Add(new NpgsqlParameter
                    {
                        DataTypeName = rows.DataTypes[i],
                        Value = row[i] ?? DBNull.Value
                    });
                }

                batch.BatchCommands.Add(command);
            }

            inserted += await batch.ExecuteNonQueryAsync(cancellationToken);
        }

        return inserted;
    }

    private static async Task<long> ScalarAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }
}
