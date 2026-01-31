# CookFinder

CookFinder is a Telegram bot MVP that helps users save short-form video recipes, auto-draft ingredients/steps, and browse saved recipes by category or name.

## MVP Features

- Accept TikTok, YouTube Shorts, and Instagram Reels links.
- Draft a recipe (title, ingredients, steps) from the video metadata with OpenAI fallbacks.
- Categorize recipes using keyword rules and OpenAI.
- Store recipes in MongoDB.
- Browse recipes by category, search by name, or search by ingredient.
- Favorites and pantry match commands.
- Nutrition estimation and localized recipe translation.
- Localization in English and Ukrainian with language selection on `/start` or `/language`.

### Telegram Commands

- `/start` - Choose language and shows usage instructions.
- `/language` - Change language.
- `/categories` - Browse saved recipes by category.
- `/search <name>` - Search recipes by title.
- `/ingredients <name>` - Search recipes by ingredient.
- `/favorites` - Show saved favorite recipes.
- `/pantry <comma-separated list>` - Find recipes matching your pantry list.
- `/nutrition <name>` - Show nutrition estimate for a saved recipe.
- `/info` - Show quick help.

## How the LLM parsing works

- The bot fetches metadata for the video (title/description/author) via `IVideoMetadataClient`.
- It sends that metadata to OpenAI Chat Completions via `OpenAiRecipeParser`.
- The response must be JSON (title/description/ingredients/steps). If the LLM fails, the pipeline falls back to a rule-based parser.

## How video metadata is selected

The bot uses a factory (`IVideoMetadataClientFactory`) to choose the correct platform client based on the link host:

- YouTube links resolve to `YouTubeMetadataClient`.
- Instagram links resolve to `InstagramMetadataClient`.
- TikTok links resolve to `TikTokMetadataClient`.
- Unknown hosts fall back to `StubVideoMetadataClient`.

## How video metadata is fetched

- **YouTube Shorts**: Uses YouTube Data API v3 (`videos.list`) with the API key to fetch title/description/channel.
- **Instagram Reels**: Uses the Instagram Graph API oEmbed endpoint with an access token.
- **TikTok**: Uses TikTok oEmbed (no token required).
