using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using TradeBook.Api.Infrastructure.Health;

// Two-stage Serilog initialisation. The bootstrap logger exists so that a
// failure while building the host (bad configuration, missing connection
// string) is still written somewhere. UseSerilog below replaces it with the
// fully configured logger once configuration and DI are available.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting TradeBook API");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddControllers();
    builder.Services.AddProblemDetails();
    builder.Services.AddTradeBookHealthChecks();

    var app = builder.Build();

    // Outermost so the one-line request summary reflects the final status code.
    app.UseSerilogRequestLogging(options => options.GetLevel = GetRequestLogLevel);
    app.UseExceptionHandler();

    app.MapControllers();
    app.MapTradeBookHealthChecks();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException is thrown deliberately by tooling that builds the
    // host without running it: `dotnet ef` at design time and
    // WebApplicationFactory in the tests. It is not a failure.
    Log.Fatal(ex, "TradeBook API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static LogEventLevel GetRequestLogLevel(HttpContext httpContext, double elapsedMs, Exception? exception)
{
    if (exception is not null || httpContext.Response.StatusCode >= 500)
    {
        return LogEventLevel.Error;
    }

    // Container health probes hit /health every few seconds. Logging each one
    // at Information would drown out everything else.
    if (httpContext.Request.Path.StartsWithSegments("/health"))
    {
        return LogEventLevel.Verbose;
    }

    return LogEventLevel.Information;
}

// Top-level statements compile to an internal Program class. The test project
// needs a visible type for WebApplicationFactory<Program>, so make it public.
public partial class Program;
