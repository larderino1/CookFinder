using CookFinder.Bot.Configurations;
using CookFinder.Bot.Repositories;
using CookFinder.Bot.Services;
using CookFinder.Bot.Services.Bot;
using CookFinder.Bot.Services.Localization;
using CookFinder.Bot.Services.Metadata;
using CookFinder.Bot.Services.Parsing;
using CookFinder.Bot.Services.Recipes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services.Configure<TelegramOptions>(context.Configuration.GetSection(TelegramOptions.SectionName));
        services.Configure<MongoOptions>(context.Configuration.GetSection(MongoOptions.SectionName));
        services.Configure<RecipeOptions>(context.Configuration.GetSection(RecipeOptions.SectionName));
        services.Configure<OpenAiOptions>(context.Configuration.GetSection(OpenAiOptions.SectionName));
        services.Configure<VideoSourceOptions>(context.Configuration.GetSection(VideoSourceOptions.SectionName));

        services.AddSingleton<ITelegramBotClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TelegramOptions>>();
            return new TelegramBotClient(options.Value.Token);
        });

        services.AddHttpClient<OpenAiRecipeParser>();
        services.AddHttpClient<OpenAiRecipeCategorizer>();
        services.AddHttpClient<OpenAiRecipeTranslationService>();
        services.AddHttpClient<OpenAiRecipeNutritionService>();
        services.AddHttpClient<YouTubeMetadataClient>();
        services.AddHttpClient<InstagramMetadataClient>();
        services.AddHttpClient<TikTokMetadataClient>();

        services.AddSingleton<IRecipeRepository, MongoRecipeRepository>();
        services.AddSingleton<IUserPreferenceRepository, MongoUserPreferenceRepository>();
        services.AddSingleton<ILocalizationService, ResourceLocalizationService>();
        services.AddSingleton<IRecipeTranslationService, OpenAiRecipeTranslationService>();
        services.AddSingleton<IRecipeNutritionService, OpenAiRecipeNutritionService>();

        services.AddScoped<StubVideoMetadataClient>();
        services.AddSingleton<IVideoMetadataClientFactory, HostBasedVideoMetadataClientFactory>();

        services.AddSingleton<IRecipeParser, OpenAiRecipeParser>();
        services.AddSingleton<RuleBasedRecipeParser>();
        services.AddSingleton<IRecipeExtractor>(serviceProvider => new RecipeExtractionPipeline(
            serviceProvider.GetRequiredService<IVideoMetadataClientFactory>(),
            serviceProvider.GetRequiredService<IRecipeParser>(),
            serviceProvider.GetRequiredService<RuleBasedRecipeParser>(),
            serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RecipeExtractionPipeline>>()));
        services.AddSingleton<KeywordRecipeCategorizer>();
        services.AddSingleton<IRecipeCategorizer, OpenAiRecipeCategorizer>();
        services.AddHostedService<TelegramBotService>();
    })
    .Build();

await host.RunAsync();
