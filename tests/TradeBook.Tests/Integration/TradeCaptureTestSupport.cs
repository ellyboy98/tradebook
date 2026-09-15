using System.Net.Http.Json;
using System.Text.Json;
using TradeBook.Api.Persistence.Entities;

namespace TradeBook.Tests.Integration;

/// <summary>
/// Shared arrangement helpers for tests that exercise POST /api/trades.
/// Each test creates its own account so positions never bleed between tests.
/// </summary>
public static class TradeCaptureTestSupport
{
    /// <summary>Seeded instrument id for AAPL.</summary>
    public const int Aapl = 1;

    /// <summary>Safely in the past whatever the clock says; "not in the future" is a real rule.</summary>
    public static readonly DateTimeOffset ExecutedAt = new(2026, 9, 1, 2, 31, 0, TimeSpan.Zero);

    /// <summary>An anonymous object shaped like the request body in design.md section 7.</summary>
    public static object Trade(
        int accountId,
        string side,
        decimal quantity,
        decimal price,
        string? externalRef = null,
        DateTimeOffset? executedAt = null,
        int instrumentId = Aapl) => new
        {
            accountId,
            instrumentId,
            side,
            quantity,
            price,
            executedAtUtc = executedAt ?? ExecutedAt,
            externalRef,
        };

    /// <summary>
    /// Posts a trade and returns the response with its body parsed as JSON. A
    /// body that is not JSON (an unexpected 500 page, say) is wrapped rather
    /// than thrown, so the status code assertion is what fails.
    /// </summary>
    public static async Task<(HttpResponseMessage Response, JsonElement Body)> PostTradeAsync(
        this HttpClient client,
        object body)
    {
        var response = await client.PostAsJsonAsync("/api/trades", body);
        var text = await response.Content.ReadAsStringAsync();

        JsonElement json;
        try
        {
            using var document = JsonDocument.Parse(text);
            json = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            json = JsonSerializer.SerializeToElement(new { raw = text });
        }

        return (response, json);
    }

    /// <summary>GETs a URL and returns the response with its body parsed as JSON.</summary>
    public static async Task<(HttpResponseMessage Response, JsonElement Body)> GetJsonAsync(
        this HttpClient client,
        string url)
    {
        var response = await client.GetAsync(url);
        var text = await response.Content.ReadAsStringAsync();

        JsonElement json;
        try
        {
            using var document = JsonDocument.Parse(text);
            json = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            json = JsonSerializer.SerializeToElement(new { raw = text });
        }

        return (response, json);
    }

    public static IEnumerable<string?> FieldErrors(JsonElement problem, string field)
        => problem.GetProperty("errors").GetProperty(field).EnumerateArray().Select(e => e.GetString());

    /// <param name="sqlServer">The shared database.</param>
    /// <param name="ownerSubject">The owning trader's subject; a fresh random one when the test does not care.</param>
    public static async Task<int> CreateAccountAsync(this SqlServerFixture sqlServer, string? ownerSubject = null)
    {
        await using var dbContext = sqlServer.CreateDbContext();
        var account = new Account
        {
            Code = $"T-{Guid.NewGuid():N}"[..12],
            Name = "Test account",
            BaseCurrency = "USD",
            OwnerSubject = ownerSubject ?? Guid.NewGuid().ToString(),
            CreatedAtUtc = DateTime.UtcNow,
        };
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();
        return account.Id;
    }

    public static async Task<int> CreateInstrumentAsync(this SqlServerFixture sqlServer, bool isActive)
    {
        await using var dbContext = sqlServer.CreateDbContext();
        var instrument = new Instrument
        {
            Symbol = $"X{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            Name = "Test instrument",
            Currency = "USD",
            TickSize = 0.01m,
            LotSize = 1,
            IsActive = isActive,
        };
        dbContext.Instruments.Add(instrument);
        await dbContext.SaveChangesAsync();
        return instrument.Id;
    }
}
