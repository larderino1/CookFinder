using CookFinder.Bot.Models;
using CookFinder.Bot.Repositories;
using CookFinder.Bot.Services.Localization;
using CookFinder.Bot.Services.Recipes;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CookFinder.Bot.Services.Bot;

public sealed class TelegramBotService(
    ITelegramBotClient botClient,
    IRecipeExtractor extractor,
    IRecipeCategorizer categorizer,
    IRecipeRepository repository,
    IUserPreferenceRepository userPreferences,
    ILocalizationService localization,
    ILogger<TelegramBotService> logger) : BackgroundService
{
    private static readonly HashSet<string> SupportedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "tiktok.com",
        "www.tiktok.com",
        "youtube.com",
        "www.youtube.com",
        "youtu.be",
        "instagram.com",
        "www.instagram.com"
    };

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
        };

        botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            stoppingToken);

        logger.LogInformation("Telegram bot started.");
        return Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not null)
        {
            await HandleMessageAsync(update.Message, cancellationToken);
            return;
        }

        if (update.CallbackQuery is not null)
        {
            await HandleCallbackAsync(update.CallbackQuery, cancellationToken);
        }
    }

    private async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.Text is null)
        {
            return;
        }

        var userId = message.From?.Id ?? 0;

        if (message.Text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            await SendLanguageSelectionAsync(message.Chat.Id, cancellationToken);
            return;
        }

        if (message.Text.StartsWith("/language", StringComparison.OrdinalIgnoreCase))
        {
            await SendLanguageSelectionAsync(message.Chat.Id, cancellationToken);
            return;
        }

        var language = await userPreferences.GetLanguageAsync(userId, cancellationToken);

        if (message.Text.StartsWith("/categories", StringComparison.OrdinalIgnoreCase))
        {
            await SendCategoriesAsync(message.Chat.Id, userId, language, cancellationToken);
            return;
        }

        if (message.Text.StartsWith("/search", StringComparison.OrdinalIgnoreCase))
        {
            if (message.Text.Length <= 7)
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    localization.GetString("SearchHelp", language),
                    cancellationToken: cancellationToken);
                return;
            }

            var query = message.Text[8..].Trim();
            await SendSearchResultsAsync(message.Chat.Id, userId, query, language, cancellationToken);
            return;
        }

        if (!Uri.TryCreate(message.Text.Trim(), UriKind.Absolute, out var url) || !SupportedHosts.Contains(url.Host))
        {
            await botClient.SendMessage(
                message.Chat.Id,
                localization.GetString("InvalidLink", language),
                cancellationToken: cancellationToken);
            return;
        }

        await botClient.SendMessage(
            message.Chat.Id,
            localization.GetString("Drafting", language),
            cancellationToken: cancellationToken);

        var draft = await extractor.ExtractAsync(userId, url, cancellationToken);
        var categories = await categorizer.CategorizeAsync(draft.Title, draft.Description, cancellationToken);

        var recipe = new Recipe
        {
            UserId = draft.UserId,
            SourceUrl = draft.SourceUrl,
            Title = draft.Title,
            Description = draft.Description,
            Ingredients = draft.Ingredients,
            Steps = draft.Steps,
            Categories = categories
        };

        await repository.SaveAsync(recipe, cancellationToken);

        await botClient.SendMessage(
            message.Chat.Id,
            RecipeFormatter.FormatSummary(recipe),
            parseMode: ParseMode.MarkdownV2,
            cancellationToken: cancellationToken);

        await botClient.SendMessage(
            message.Chat.Id,
            localization.GetString("RecipeSaved", language),
            cancellationToken: cancellationToken);
    }

    private async Task HandleCallbackAsync(CallbackQuery query, CancellationToken cancellationToken)
    {
        if (query.Data is null || query.Message is null)
        {
            return;
        }

        if (query.Data.StartsWith("lang:", StringComparison.OrdinalIgnoreCase))
        {
            var language = query.Data[5..];
            await userPreferences.SetLanguageAsync(query.From.Id, language, cancellationToken);

            await botClient.SendMessage(
                query.Message.Chat.Id,
                localization.GetString("LanguageSet", language),
                cancellationToken: cancellationToken);

            await botClient.AnswerCallbackQuery(query.Id, cancellationToken: cancellationToken);
            return;
        }

        if (query.Data.StartsWith("category:", StringComparison.OrdinalIgnoreCase))
        {
            var language = await userPreferences.GetLanguageAsync(query.From.Id, cancellationToken);
            var category = query.Data[9..];
            var recipes = await repository.GetByCategoryAsync(query.From.Id, category, cancellationToken);

            if (recipes.Count == 0)
            {
                await botClient.AnswerCallbackQuery(
                    query.Id,
                    localization.GetString("NoRecipesInCategory", language, category),
                    cancellationToken: cancellationToken);
                return;
            }

            foreach (var recipe in recipes)
            {
                await botClient.SendMessage(
                    query.Message.Chat.Id,
                    RecipeFormatter.FormatSummary(recipe),
                    parseMode: ParseMode.MarkdownV2,
                    cancellationToken: cancellationToken);
            }

            await botClient.AnswerCallbackQuery(query.Id, cancellationToken: cancellationToken);
        }
    }

    private async Task SendLanguageSelectionAsync(long chatId, CancellationToken cancellationToken)
    {
        var buttons = new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("English", "lang:en"),
                InlineKeyboardButton.WithCallbackData("Українська", "lang:uk")
            }
        };

        await botClient.SendMessage(
            chatId,
            localization.GetString("ChooseLanguage", "en"),
            replyMarkup: new InlineKeyboardMarkup(buttons),
            cancellationToken: cancellationToken);
    }

    private async Task SendCategoriesAsync(long chatId, long userId, string language, CancellationToken cancellationToken)
    {
        var categories = await repository.GetCategoriesAsync(userId, cancellationToken);

        if (categories.Count == 0)
        {
            await botClient.SendMessage(
                chatId,
                localization.GetString("CategoriesEmpty", language),
                cancellationToken: cancellationToken);
            return;
        }

        var buttons = categories
            .OrderBy(category => category)
            .Select(category => InlineKeyboardButton.WithCallbackData(category, $"category:{category}"))
            .Chunk(2)
            .Select(row => row.ToArray())
            .ToArray();

        await botClient.SendMessage(
            chatId,
            localization.GetString("ChooseCategory", language),
            replyMarkup: new InlineKeyboardMarkup(buttons),
            cancellationToken: cancellationToken);
    }

    private async Task SendSearchResultsAsync(long chatId, long userId, string query, string language, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            await botClient.SendMessage(
                chatId,
                localization.GetString("SearchMissing", language),
                cancellationToken: cancellationToken);
            return;
        }

        var recipes = await repository.SearchByNameAsync(userId, query, cancellationToken);

        if (recipes.Count == 0)
        {
            await botClient.SendMessage(
                chatId,
                localization.GetString("SearchNone", language),
                cancellationToken: cancellationToken);
            return;
        }

        foreach (var recipe in recipes)
        {
            await botClient.SendMessage(
                chatId,
                RecipeFormatter.FormatSummary(recipe),
                parseMode: ParseMode.MarkdownV2,
                cancellationToken: cancellationToken);
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient client, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException =>
                $"Telegram API Error:\n[{apiRequestException.ErrorCode}] {apiRequestException.Message}",
            _ => exception.ToString()
        };

        logger.LogError(exception, "Telegram polling error: {Message}", errorMessage);
        return Task.CompletedTask;
    }
}
