using System.Text.Json.Serialization;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using TradeBook.Api.Features.Accounts;
using TradeBook.Api.Features.Instruments;
using TradeBook.Api.Features.Positions;
using TradeBook.Api.Features.PriceFeed;
using TradeBook.Api.Features.TradeCapture;
using TradeBook.Api.Hubs;
using TradeBook.Api.Infrastructure.Auth;
using TradeBook.Api.Infrastructure.Health;
using TradeBook.Api.Persistence;

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

    builder.Services
        .AddControllers()
        // Enums travel as their names ("Buy", "Sell"), matching the API
        // contract in design.md section 7, rather than as 1 and 2.
        .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.AddProblemDetails();
    builder.Services.AddTradeBookPersistence();
    builder.Services.AddTradeBookHealthChecks();
    builder.Services.AddTradeBookAuthentication();
    builder.Services.AddTradeBookAuthorization();
    builder.Services.AddSignalR();
    builder.Services.AddSingleton<IPositionNotifier, SignalRPositionNotifier>();
    builder.Services.AddTradeBookPriceFeed();

    // Handlers are plain scoped classes (CLAUDE.md): one per request, same
    // lifetime as the DbContext they use.
    builder.Services.AddScoped<CaptureTradeHandler>();
    builder.Services.AddScoped<BlotterHandler>();
    builder.Services.AddScoped<GetPositionsHandler>();
    builder.Services.AddScoped<ListInstrumentsHandler>();
    builder.Services.AddScoped<CreateInstrumentHandler>();
    builder.Services.AddScoped<ListAccountsHandler>();
    builder.Services.AddScoped<CreateAccountHandler>();

    // The clock is injected so "not in the future" can be tested without
    // waiting, and so the audit timestamps come from one source.
    builder.Services.AddSingleton(TimeProvider.System);

    var app = builder.Build();

    if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        // Opt-in. On for the compose stack and local development so a clean
        // start produces a working schema; off where a pipeline owns the
        // database (ADR-008).
        await app.MigrateDatabaseAsync(app.Lifetime.ApplicationStopping);
    }

    // Outermost so the one-line request summary reflects the final status code.
    app.UseSerilogRequestLogging(options => options.GetLevel = GetRequestLogLevel);
    app.UseExceptionHandler();
    // Gives the empty 401 and 403 responses from authentication and
    // authorisation a problem-details body, like every other error.
    app.UseStatusCodePages();

    // The blotter page and its script, served from wwwroot. Placed before
    // authentication on purpose: these are middleware, not endpoints, so the
    // fallback "must be signed in" policy does not apply to them, and the page
    // has to load before anyone can sign in. (MapStaticAssets would make them
    // endpoints and the fallback policy would then block index.html.)
    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<PositionHub>("/hubs/positions");
    app.MapTradeBookHealthChecks();

    await app.RunAsync();
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
