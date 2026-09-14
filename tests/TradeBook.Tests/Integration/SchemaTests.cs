using Microsoft.Data.SqlClient;

namespace TradeBook.Tests.Integration;

/// <summary>
/// Proves the migrated schema matches design.md section 4 and
/// diagrams/erd.dot. Each expected list is the design document transcribed
/// line for line, so a mismatch names the exact column, index or key.
/// </summary>
[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class SchemaTests(SqlServerFixture sqlServer)
{
    private static readonly string[] ExpectedColumns =
    [
        "accounts.id int not null",
        "accounts.code nvarchar(16) not null",
        "accounts.name nvarchar(128) not null",
        "accounts.base_currency char(3) not null",
        "accounts.owner_subject nvarchar(64) not null",
        "accounts.is_active bit not null",
        "accounts.created_at_utc datetime2(3) not null",

        "instruments.id int not null",
        "instruments.symbol nvarchar(16) not null",
        "instruments.name nvarchar(128) not null",
        "instruments.instrument_type tinyint not null",
        "instruments.currency char(3) not null",
        "instruments.tick_size decimal(18,6) not null",
        "instruments.lot_size int not null",
        "instruments.is_active bit not null",

        "trades.id bigint not null",
        "trades.account_id int not null",
        "trades.instrument_id int not null",
        "trades.side tinyint not null",
        "trades.quantity decimal(18,4) not null",
        "trades.price decimal(18,6) not null",
        "trades.executed_at_utc datetime2(3) not null",
        "trades.external_ref nvarchar(64) null",
        "trades.captured_by_subject nvarchar(64) not null",
        "trades.captured_at_utc datetime2(3) not null",

        "positions.id int not null",
        "positions.account_id int not null",
        "positions.instrument_id int not null",
        "positions.net_quantity decimal(18,4) not null",
        "positions.average_cost decimal(18,6) not null",
        "positions.realised_pnl decimal(18,6) not null",
        "positions.last_trade_id bigint null",
        "positions.updated_at_utc datetime2(3) not null",
        "positions.row_version rowversion not null",

        "instrument_prices.instrument_id int not null",
        "instrument_prices.last_price decimal(18,6) not null",
        "instrument_prices.as_of_utc datetime2(3) not null",
    ];

    private static readonly string[] ExpectedIndexes =
    [
        "accounts(id) primary key",
        "accounts(code) unique",
        "accounts(owner_subject) index",

        "instruments(id) primary key",
        "instruments(symbol) unique",

        "trades(id) primary key",
        "trades(account_id) index",
        "trades(instrument_id) index",
        "trades(executed_at_utc) index",
        "trades(external_ref) unique filtered",

        "positions(id) primary key",
        "positions(account_id,instrument_id) unique",
        "positions(instrument_id) index",
        "positions(last_trade_id) index",

        "instrument_prices(instrument_id) primary key",
    ];

    private static readonly string[] ExpectedForeignKeys =
    [
        "trades.account_id -> accounts.id NO_ACTION",
        "trades.instrument_id -> instruments.id NO_ACTION",
        "positions.account_id -> accounts.id NO_ACTION",
        "positions.instrument_id -> instruments.id NO_ACTION",
        "positions.last_trade_id -> trades.id NO_ACTION",
        "instrument_prices.instrument_id -> instruments.id NO_ACTION",
    ];

    [Fact]
    public async Task Columns_match_design_section_4()
    {
        var actual = await QueryAsync(
            """
            SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH,
                   NUMERIC_PRECISION, NUMERIC_SCALE, DATETIME_PRECISION, IS_NULLABLE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME <> '__EFMigrationsHistory'
            ORDER BY TABLE_NAME, ORDINAL_POSITION
            """,
            reader =>
            {
                var nullability = reader.GetString(7) == "YES" ? "null" : "not null";
                return $"{reader.GetString(0)}.{reader.GetString(1)} {FormatType(reader)} {nullability}";
            });

        actual.Should().BeEquivalentTo(ExpectedColumns);
    }

    [Fact]
    public async Task Indexes_and_unique_constraints_match_design_section_4()
    {
        var actual = await QueryAsync(
            """
            SELECT t.name,
                   STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal),
                   i.is_primary_key, i.is_unique, i.has_filter
            FROM sys.indexes i
            JOIN sys.tables t ON t.object_id = i.object_id
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE t.name <> '__EFMigrationsHistory' AND ic.is_included_column = 0
            GROUP BY t.name, i.name, i.is_primary_key, i.is_unique, i.has_filter
            ORDER BY t.name, i.name
            """,
            reader =>
            {
                var kind = reader.GetBoolean(2) ? "primary key" : reader.GetBoolean(3) ? "unique" : "index";
                var filtered = reader.GetBoolean(4) ? " filtered" : string.Empty;
                return $"{reader.GetString(0)}({reader.GetString(1)}) {kind}{filtered}";
            });

        actual.Should().BeEquivalentTo(ExpectedIndexes);
    }

    [Fact]
    public async Task Foreign_keys_never_cascade()
    {
        var actual = await QueryAsync(
            """
            SELECT OBJECT_NAME(fk.parent_object_id), pc.name,
                   OBJECT_NAME(fk.referenced_object_id), rc.name,
                   fk.delete_referential_action_desc
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
            JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
            ORDER BY 1, 2
            """,
            reader => $"{reader.GetString(0)}.{reader.GetString(1)} -> {reader.GetString(2)}.{reader.GetString(3)} {reader.GetString(4)}");

        actual.Should().BeEquivalentTo(ExpectedForeignKeys);
    }

    private static string FormatType(SqlDataReader reader)
    {
        var dataType = reader.GetString(2);

        return dataType switch
        {
            "nvarchar" or "char" or "varchar" => $"{dataType}({ToInt(reader, 3)})",
            "decimal" => $"decimal({ToInt(reader, 4)},{ToInt(reader, 5)})",
            "datetime2" => $"datetime2({ToInt(reader, 6)})",
            // INFORMATION_SCHEMA reports rowversion under its legacy name.
            "timestamp" => "rowversion",
            _ => dataType,
        };
    }

    // INFORMATION_SCHEMA mixes int, tinyint and smallint for these columns.
    private static int ToInt(SqlDataReader reader, int ordinal) => Convert.ToInt32(reader.GetValue(ordinal));

    private async Task<List<string>> QueryAsync(string sql, Func<SqlDataReader, string> project)
    {
        await using var connection = new SqlConnection(sqlServer.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(project(reader));
        }

        return rows;
    }
}
