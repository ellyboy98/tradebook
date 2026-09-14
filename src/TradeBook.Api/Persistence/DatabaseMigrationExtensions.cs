using Microsoft.EntityFrameworkCore;

namespace TradeBook.Api.Persistence;

public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Applies pending EF Core migrations. This is the "explicit startup step"
    /// from design.md section 11, used by the compose stack and local
    /// development so a clean <c>docker compose up</c> produces a working demo.
    /// It is gated by configuration in Program.cs; a pipeline-driven
    /// deployment leaves it off and runs <c>dotnet ef database update</c> instead.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this WebApplication app, CancellationToken cancellationToken)
    {
        // DbContext is scoped and there is no request here, so create a scope by hand.
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TradeBookDbContext>();

        var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            app.Logger.LogInformation("Database schema is up to date");
            return;
        }

        app.Logger.LogInformation("Applying {MigrationCount} pending migration(s): {Migrations}", pending.Count, pending);
        await dbContext.Database.MigrateAsync(cancellationToken);
        app.Logger.LogInformation("Database migration complete");
    }
}
