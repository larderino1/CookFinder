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
    IRecipeTranslationService translator,
    IRecipeNutritionService nutritionService,
    IRecipeRepository repository,
    IUserPreferenceRepository userPreferences,
    ILocalizationService localization,
    ILogger<TelegramBotService> logger) : BackgroundService
{
    private static readonly string[] SupportedHostSuffixes =
    {
        "tiktok.com",
        "youtube.com",
        "youtu.be",
        "instagram.com"
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
            logger.LogInformation("Skipping message {MessageId} in chat {ChatId} because it has no text.", message.MessageId, message.Chat.Id);
            return;
        }

        var userId = message.From?.Id ?? 0;
        var language = await userPreferences.GetLanguageAsync(userId, cancellationToken);
        var command = ParseCommand(message.Text, language);
        logger.LogInformation(
            "Handling message {MessageId} for user {UserId} in chat {ChatId}. Language={Language}, Command={Command}, ArgumentLength={ArgumentLength}.",
            message.MessageId,
            userId,
            message.Chat.Id,
            language,
            command.Name,
            command.Argument.Length);

        if (command.Name.Equals("start", StringComparison.OrdinalIgnoreCase))
        {
            await SendLanguageSelectionAsync(message.Chat.Id, cancellationToken);
            await SendMainMenuAsync(message.Chat.Id, "en", cancellationToken);
            return;
        }

        if (command.Name.Equals("language", StringComparison.OrdinalIgnoreCase))
        {
            await SendLanguageSelectionAsync(message.Chat.Id, cancellationToken);
            await SendMainMenuAsync(message.Chat.Id, language, cancellationToken);
            return;
        }

        if (command.Name.Equals("categories", StringComparison.OrdinalIgnoreCase))
        {
            await SendCategoriesAsync(message.Chat.Id, userId, language, cancellationToken);
            return;
        }

        if (command.Name.Equals("search", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(command.Argument))
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    localization.GetString("SearchHelp", language),
                    cancellationToken: cancellationToken);
                return;
            }

            var query = command.Argument;
            await SendSearchResultsAsync(message.Chat.Id, userId, query, language, cancellationToken);
            return;
        }

        if (command.Name.Equals("ingredients", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(command.Argument))
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    localization.GetString("IngredientSearchHelp", language),
                    cancellationToken: cancellationToken);
                return;
            }

            var query = command.Argument;
            await SendIngredientSearchResultsAsync(message.Chat.Id, userId, query, language, cancellationToken);
            return;
        }

        if (command.Name.Equals("favorites", StringComparison.OrdinalIgnoreCase))
        {
            await SendFavoritesAsync(message.Chat.Id, userId, language, cancellationToken);
            return;
        }

        if (command.Name.Equals("pantry", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(command.Argument))
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    localization.GetString("PantryHelp", language),
                    cancellationToken: cancellationToken);
                return;
            }

            await SendPantryMatchesAsync(message.Chat.Id, userId, command.Argument, language, cancellationToken);
            return;
        }

        if (command.Name.Equals("nutrition", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(command.Argument))
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    localization.GetString("NutritionHelp", language),
                    cancellationToken: cancellationToken);
                return;
            }

            await SendNutritionByNameAsync(message.Chat.Id, userId, command.Argument, language, cancellationToken);
            return;
        }

        if (command.Name.Equals("info", StringComparison.OrdinalIgnoreCase))
        {
            await SendInfoAsync(message.Chat.Id, language, cancellationToken);
            return;
        }

        if (!TryExtractSupportedUrl(message, out var url))
        {
            logger.LogWarning(
                "Message {MessageId} from user {UserId} failed URL validation. Text preview: {TextPreview}",
                message.MessageId,
                userId,
                message.Text.Length > 200 ? message.Text[..200] : message.Text);
            await botClient.SendMessage(
                message.Chat.Id,
                localization.GetString("InvalidLink", language),
                cancellationToken: cancellationToken);
            return;
        }

        logger.LogInformation("Validated supported URL {Url} for user {UserId}.", url, userId);

        var existingRecipe = await repository.GetBySourceUrlAsync(userId, url.ToString(), cancellationToken);
        if (existingRecipe is not null)
        {
            logger.LogInformation("Recipe already exists for user {UserId} and URL {Url}. RecipeId={RecipeId}", userId, url, existingRecipe.Id);
            await botClient.SendMessage(
                message.Chat.Id,
                localization.GetString("RecipeAlreadyAdded", language),
                cancellationToken: cancellationToken);
            await SendRecipeSummaryAsync(message.Chat.Id, existingRecipe, language, cancellationToken);
            return;
        }

        await botClient.SendMessage(
            message.Chat.Id,
            localization.GetString("Drafting", language),
            cancellationToken: cancellationToken);

        logger.LogInformation("Starting extraction pipeline for user {UserId}, URL {Url}.", userId, url);

        var draft = await extractor.ExtractAsync(userId, url, cancellationToken);
        logger.LogInformation(
            "Extraction completed for user {UserId}. Title='{Title}', Ingredients={IngredientCount}, Steps={StepCount}.",
            userId,
            draft.Title,
            draft.Ingredients.Count,
            draft.Steps.Count);

        var categories = await categorizer.CategorizeAsync(draft.Title, draft.Description, cancellationToken);
        logger.LogInformation("Categorization completed for user {UserId}. Categories={Categories}.", userId, string.Join(", ", categories));

        var translated = await translator.TranslateAsync(
            new Recipe
            {
                UserId = draft.UserId,
                SourceUrl = draft.SourceUrl,
                Title = draft.Title,
                Description = draft.Description,
                Ingredients = draft.Ingredients,
                Steps = draft.Steps,
                Categories = categories
            },
            categories,
            language,
            cancellationToken);
        logger.LogInformation(
            "Translation completed for user {UserId}. Language={Language}, Ingredients={IngredientCount}, Steps={StepCount}, Categories={CategoryCount}.",
            userId,
            language,
            translated.Ingredients.Count,
            translated.Steps.Count,
            translated.Categories.Count);

        var nutrition = await nutritionService.EstimateAsync(
            new Recipe
            {
                UserId = draft.UserId,
                SourceUrl = draft.SourceUrl,
                Title = translated.Title,
                Description = translated.Description,
                Ingredients = translated.Ingredients,
                Steps = translated.Steps,
                Categories = translated.Categories
            },
            language,
            cancellationToken);
        logger.LogInformation(
            "Nutrition estimation completed for user {UserId}. HasNutrition={HasNutrition}.",
            userId,
            nutrition is not null);

        var recipe = new Recipe
        {
            UserId = draft.UserId,
            SourceUrl = draft.SourceUrl,
            Title = translated.Title,
            Description = translated.Description,
            Ingredients = translated.Ingredients,
            Steps = translated.Steps,
            Categories = translated.Categories,
            Nutrition = nutrition
        };

        await repository.SaveAsync(recipe, cancellationToken);
        logger.LogInformation("Recipe saved for user {UserId}. RecipeId={RecipeId}, SourceUrl={SourceUrl}", userId, recipe.Id, recipe.SourceUrl);

        await SendRecipeSummaryAsync(message.Chat.Id, recipe, language, cancellationToken);
        logger.LogInformation("Recipe summary sent to chat {ChatId} for user {UserId}.", message.Chat.Id, userId);
    }

    private static bool TryExtractSupportedUrl(Message message, out Uri url)
    {
        url = null!;

        if (message.Text is null)
        {
            return false;
        }

        if (message.Entities is not null)
        {
            foreach (var entity in message.Entities)
            {
                if (entity.Type == MessageEntityType.TextLink && entity.Url is not null
                    && TryExtractSupportedUrl(entity.Url.ToString(), out url))
                {
                    return true;
                }

                if (entity.Type == MessageEntityType.Url)
                {
                    var part = message.Text.Substring(entity.Offset, entity.Length);
                    if (TryExtractSupportedUrl(part, out url))
                    {
                        return true;
                    }
                }
            }
        }

        foreach (var part in message.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryExtractSupportedUrl(part, out url))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryExtractSupportedUrl(string value, out Uri url)
    {
        url = null!;
        var part = value.Trim('(', '[', '"', '\'')
            .TrimEnd('.', ',', ';', ':', '!', '?', ')', ']', '"', '\'', '\\');

        if (!TryCreateUri(part, out var candidate))
        {
            return false;
        }

        if (candidate.Scheme is not ("http" or "https"))
        {
            return false;
        }

        if (!SupportedHostSuffixes.Any(suffix =>
                candidate.Host.Equals(suffix, StringComparison.OrdinalIgnoreCase)
                || candidate.Host.EndsWith($".{suffix}", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        url = candidate;
        return true;
    }

    private static bool TryCreateUri(string value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out uri))
        {
            return true;
        }

        if (!value.Contains("://", StringComparison.Ordinal))
        {
            return Uri.TryCreate($"https://{value}", UriKind.Absolute, out uri);
        }

        return false;
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

            await SendMainMenuAsync(query.Message.Chat.Id, language, cancellationToken);
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
                await SendRecipeSummaryAsync(query.Message.Chat.Id, recipe, language, cancellationToken);
            }

            await botClient.AnswerCallbackQuery(query.Id, cancellationToken: cancellationToken);
            return;
        }

        if (query.Data.StartsWith("favorite:", StringComparison.OrdinalIgnoreCase))
        {
            var language = await userPreferences.GetLanguageAsync(query.From.Id, cancellationToken);
            var id = query.Data[9..];
            var recipe = await repository.GetByIdAsync(query.From.Id, id, cancellationToken);

            if (recipe is null)
            {
                await botClient.AnswerCallbackQuery(query.Id, cancellationToken: cancellationToken);
                return;
            }

            var isFavorite = !recipe.IsFavorite;
            await repository.SetFavoriteAsync(query.From.Id, id, isFavorite, cancellationToken);

            await botClient.SendMessage(
                query.Message.Chat.Id,
                localization.GetString(isFavorite ? "FavoriteAdded" : "FavoriteRemoved", language),
                cancellationToken: cancellationToken);

            await botClient.AnswerCallbackQuery(query.Id, cancellationToken: cancellationToken);
            return;
        }

        if (query.Data.StartsWith("nutrition:", StringComparison.OrdinalIgnoreCase))
        {
            var language = await userPreferences.GetLanguageAsync(query.From.Id, cancellationToken);
            var id = query.Data[10..];
            var recipe = await repository.GetByIdAsync(query.From.Id, id, cancellationToken);

            if (recipe is null)
            {
                await botClient.AnswerCallbackQuery(query.Id, cancellationToken: cancellationToken);
                return;
            }

            await SendNutritionAsync(query.Message.Chat.Id, recipe, language, cancellationToken);
            await botClient.AnswerCallbackQuery(query.Id, cancellationToken: cancellationToken);
            return;
        }

        if (query.Data.StartsWith("cook-finish:", StringComparison.OrdinalIgnoreCase))
        {
            var language = await userPreferences.GetLanguageAsync(query.From.Id, cancellationToken);
            await botClient.SendMessage(
                query.Message.Chat.Id,
                localization.GetString("CookingFinished", language),
                cancellationToken: cancellationToken);
            await SendMainMenuAsync(query.Message.Chat.Id, language, cancellationToken);
            await botClient.AnswerCallbackQuery(query.Id, cancellationToken: cancellationToken);
            return;
        }

        if (query.Data.StartsWith("cook:", StringComparison.OrdinalIgnoreCase))
        {
            var language = await userPreferences.GetLanguageAsync(query.From.Id, cancellationToken);
            var parts = query.Data.Split(':', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3)
            {
                await botClient.AnswerCallbackQuery(query.Id, cancellationToken: cancellationToken);
                return;
            }

            var recipeId = parts[1];
            if (!int.TryParse(parts[2], out var stepIndex))
            {
                await botClient.AnswerCallbackQuery(query.Id, cancellationToken: cancellationToken);
                return;
            }

            var recipe = await repository.GetByIdAsync(query.From.Id, recipeId, cancellationToken);
            if (recipe is null)
            {
                await botClient.AnswerCallbackQuery(query.Id, cancellationToken: cancellationToken);
                return;
            }

            await SendCookingStepAsync(query.Message.Chat.Id, recipe, stepIndex, language, cancellationToken);
            await botClient.AnswerCallbackQuery(query.Id, cancellationToken: cancellationToken);
            return;
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

    private async Task SendMainMenuAsync(long chatId, string language, CancellationToken cancellationToken)
    {
        var buttons = new[]
        {
            new KeyboardButton[]
            {
                localization.GetString("MenuCategories", language),
                localization.GetString("MenuSearch", language)
            },
            new KeyboardButton[]
            {
                localization.GetString("MenuIngredients", language),
                localization.GetString("MenuFavorites", language)
            },
            new KeyboardButton[]
            {
                localization.GetString("MenuPantry", language),
                localization.GetString("MenuNutrition", language)
            },
            new KeyboardButton[]
            {
                localization.GetString("MenuLanguage", language),
                localization.GetString("MenuInfo", language)
            }
        };

        await botClient.SendMessage(
            chatId,
            localization.GetString("MainMenu", language),
            replyMarkup: new ReplyKeyboardMarkup(buttons)
            {
                ResizeKeyboard = true,
                IsPersistent = true
            },
            cancellationToken: cancellationToken);
    }

    private async Task SendInfoAsync(long chatId, string language, CancellationToken cancellationToken)
    {
        await botClient.SendMessage(
            chatId,
            localization.GetString("InfoMessage", language),
            cancellationToken: cancellationToken);
    }

    private async Task SendFavoritesAsync(long chatId, long userId, string language, CancellationToken cancellationToken)
    {
        var favorites = await repository.GetFavoritesAsync(userId, cancellationToken);

        if (favorites.Count == 0)
        {
            await botClient.SendMessage(
                chatId,
                localization.GetString("FavoritesEmpty", language),
                cancellationToken: cancellationToken);
            return;
        }

        await botClient.SendMessage(
            chatId,
            localization.GetString("FavoritesTitle", language),
            cancellationToken: cancellationToken);

        foreach (var recipe in favorites)
        {
            await SendRecipeSummaryAsync(chatId, recipe, language, cancellationToken);
        }
    }

    private async Task SendPantryMatchesAsync(long chatId, long userId, string pantryInput, string language, CancellationToken cancellationToken)
    {
        var ingredients = pantryInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (ingredients.Length == 0)
        {
            await botClient.SendMessage(
                chatId,
                localization.GetString("PantryMissing", language),
                cancellationToken: cancellationToken);
            return;
        }

        var recipes = await repository.GetAllAsync(userId, cancellationToken);
        var matches = recipes
            .Select(recipe => new
            {
                Recipe = recipe,
                MatchCount = recipe.Ingredients.Count(ingredient =>
                    ingredients.Any(item => ingredient.Name.Contains(item, StringComparison.OrdinalIgnoreCase)))
            })
            .Where(result => result.MatchCount > 0)
            .OrderByDescending(result => result.MatchCount)
            .ThenBy(result => result.Recipe.Title)
            .ToList();

        if (matches.Count == 0)
        {
            await botClient.SendMessage(
                chatId,
                localization.GetString("PantryNone", language),
                cancellationToken: cancellationToken);
            return;
        }

        await botClient.SendMessage(
            chatId,
            localization.GetString("PantryResults", language),
            cancellationToken: cancellationToken);

        foreach (var match in matches.Take(5))
        {
            await SendRecipeSummaryAsync(chatId, match.Recipe, language, cancellationToken);
        }
    }

    private async Task SendNutritionByNameAsync(long chatId, long userId, string query, string language, CancellationToken cancellationToken)
    {
        var recipes = await repository.SearchByNameAsync(userId, query, cancellationToken);
        var recipe = recipes.FirstOrDefault();

        if (recipe is null)
        {
            await botClient.SendMessage(
                chatId,
                localization.GetString("NutritionNotFound", language),
                cancellationToken: cancellationToken);
            return;
        }

        await SendNutritionAsync(chatId, recipe, language, cancellationToken);
    }

    private async Task SendNutritionAsync(long chatId, Recipe recipe, string language, CancellationToken cancellationToken)
    {
        var estimate = recipe.Nutrition ?? await nutritionService.EstimateAsync(recipe, language, cancellationToken);

        if (estimate is null)
        {
            await botClient.SendMessage(
                chatId,
                localization.GetString("NutritionUnavailable", language),
                cancellationToken: cancellationToken);
            return;
        }

        await botClient.SendMessage(
            chatId,
            localization.GetString(
                "NutritionResult",
                language,
                estimate.Calories,
                estimate.Protein,
                estimate.Carbs,
                estimate.Fat),
            cancellationToken: cancellationToken);
    }

    private async Task SendCookingStepAsync(long chatId, Recipe recipe, int stepIndex, string language, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(recipe.Id))
        {
            await botClient.SendMessage(
                chatId,
                localization.GetString("CookingNoSteps", language),
                cancellationToken: cancellationToken);
            return;
        }

        var steps = recipe.Steps.OrderBy(step => step.Order).ToList();
        if (steps.Count == 0)
        {
            await botClient.SendMessage(
                chatId,
                localization.GetString("CookingNoSteps", language),
                cancellationToken: cancellationToken);
            return;
        }

        var clampedIndex = Math.Clamp(stepIndex, 0, steps.Count - 1);
        var step = steps[clampedIndex];
        var instruction = step.Instruction?.Trim() ?? string.Empty;
        var text = localization.GetString("CookingStep", language, clampedIndex + 1, steps.Count, instruction);

        var buttons = new List<InlineKeyboardButton[]>();
        var row = new List<InlineKeyboardButton>();

        if (clampedIndex > 0)
        {
            row.Add(InlineKeyboardButton.WithCallbackData(
                localization.GetString("ActionPrevStep", language),
                $"cook:{recipe.Id}:{clampedIndex - 1}"));
        }

        row.Add(InlineKeyboardButton.WithCallbackData(
            localization.GetString("ActionRepeatStep", language),
            $"cook:{recipe.Id}:{clampedIndex}"));

        if (clampedIndex < steps.Count - 1)
        {
            row.Add(InlineKeyboardButton.WithCallbackData(
                localization.GetString("ActionNextStep", language),
                $"cook:{recipe.Id}:{clampedIndex + 1}"));
        }

        buttons.Add(row.ToArray());

        if (clampedIndex == steps.Count - 1)
        {
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    localization.GetString("ActionFinishCooking", language),
                    $"cook-finish:{recipe.Id}")
            });
        }

        await botClient.SendMessage(
            chatId,
            text,
            replyMarkup: new InlineKeyboardMarkup(buttons),
            cancellationToken: cancellationToken);
    }

    private async Task SendRecipeSummaryAsync(long chatId, Recipe recipe, string language, CancellationToken cancellationToken)
    {
        var markup = BuildRecipeActions(recipe, language);
        await botClient.SendMessage(
            chatId,
            RecipeFormatter.FormatSummary(recipe),
            parseMode: ParseMode.MarkdownV2,
            replyMarkup: markup,
            cancellationToken: cancellationToken);
    }

    private InlineKeyboardMarkup? BuildRecipeActions(Recipe recipe, string language)
    {
        if (string.IsNullOrWhiteSpace(recipe.Id))
        {
            return null;
        }

        var favoriteLabel = recipe.IsFavorite
            ? localization.GetString("ActionUnfavorite", language)
            : localization.GetString("ActionFavorite", language);

        var buttons = new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    localization.GetString("ActionStartCooking", language),
                    $"cook:{recipe.Id}:0"),
                InlineKeyboardButton.WithCallbackData(
                    favoriteLabel,
                    $"favorite:{recipe.Id}")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    localization.GetString("ActionNutrition", language),
                    $"nutrition:{recipe.Id}")
            }
        };

        return new InlineKeyboardMarkup(buttons);
    }

    private CommandParseResult ParseCommand(string text, string language)
    {
        var trimmed = text.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return new CommandParseResult(string.Empty, string.Empty);
        }

        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "/start", "start" },
            { "start", "start" },
            { "/language", "language" },
            { "language", "language" },
            { "/categories", "categories" },
            { "categories", "categories" },
            { "/search", "search" },
            { "search", "search" },
            { "/ingredients", "ingredients" },
            { "ingredients", "ingredients" },
            { "/favorites", "favorites" },
            { "favorites", "favorites" },
            { "/pantry", "pantry" },
            { "pantry", "pantry" },
            { "/nutrition", "nutrition" },
            { "nutrition", "nutrition" },
            { "/info", "info" },
            { "info", "info" }
        };

        var localizedAliases = new[]
        {
            ("MenuLanguage", "language"),
            ("MenuCategories", "categories"),
            ("MenuSearch", "search"),
            ("MenuIngredients", "ingredients"),
            ("MenuFavorites", "favorites"),
            ("MenuPantry", "pantry"),
            ("MenuNutrition", "nutrition"),
            ("MenuInfo", "info")
        };

        foreach (var (key, command) in localizedAliases)
        {
            var label = localization.GetString(key, language);
            if (!string.IsNullOrWhiteSpace(label))
            {
                aliases[label] = command;
                aliases[$"/{label}"] = command;
            }
        }

        foreach (var alias in aliases.Keys)
        {
            if (trimmed.Equals(alias, StringComparison.OrdinalIgnoreCase))
            {
                return new CommandParseResult(aliases[alias], string.Empty);
            }

            if (trimmed.StartsWith(alias + " ", StringComparison.OrdinalIgnoreCase))
            {
                var argument = trimmed[alias.Length..].Trim();
                return new CommandParseResult(aliases[alias], argument);
            }
        }

        return new CommandParseResult(string.Empty, string.Empty);
    }

    private sealed record CommandParseResult(string Name, string Argument);

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
            await SendRecipeSummaryAsync(chatId, recipe, language, cancellationToken);
        }
    }

    private async Task SendIngredientSearchResultsAsync(long chatId, long userId, string query, string language, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            await botClient.SendMessage(
                chatId,
                localization.GetString("IngredientSearchMissing", language),
                cancellationToken: cancellationToken);
            return;
        }

        var recipes = await repository.SearchByIngredientAsync(userId, query, cancellationToken);

        if (recipes.Count == 0)
        {
            await botClient.SendMessage(
                chatId,
                localization.GetString("IngredientSearchNone", language),
                cancellationToken: cancellationToken);
            return;
        }

        foreach (var recipe in recipes)
        {
            await SendRecipeSummaryAsync(chatId, recipe, language, cancellationToken);
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
