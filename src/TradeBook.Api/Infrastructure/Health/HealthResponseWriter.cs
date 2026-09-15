using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TradeBook.Api.Infrastructure.Health;

/// <summary>
/// Renders a <see cref="HealthReport"/> as JSON. The default writer returns
/// the bare word "Healthy" as text/plain, which tells a caller nothing about
/// which dependency failed.
/// </summary>
public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = (long)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = (long)entry.Value.Duration.TotalMilliseconds,
                description = entry.Value.Description,
            }),
        };

        return context.Response.WriteAsJsonAsync(payload, SerializerOptions, context.RequestAborted);
    }
}
