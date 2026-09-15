namespace TradeBook.Api.Features.PriceFeed;

public static class PriceFeedServiceCollectionExtensions
{
    public static IServiceCollection AddTradeBookPriceFeed(this IServiceCollection services)
    {
        services.AddOptions<PriceFeedOptions>()
            .BindConfiguration(PriceFeedOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IPriceGenerator, RandomWalkPriceGenerator>();
        services.AddScoped<PriceTick>();

        // Always registered; the service reads PriceFeed:Enabled when it starts.
        // Deciding here with builder.Configuration would run before test
        // overrides are applied (see PersistenceServiceCollectionExtensions).
        services.AddHostedService<PriceFeedService>();

        return services;
    }
}
