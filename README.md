# CookFinder

CookFinder is a Telegram bot MVP that helps users save short-form video recipes, auto-draft ingredients/steps, and browse saved recipes by category or name.

## MVP Features

- Accept TikTok, YouTube Shorts, and Instagram Reels links.
- Draft a recipe (title, ingredients, steps) from the video metadata (stubbed for now).
- Categorize recipes using keyword rules.
- Store recipes in MongoDB.
- Browse recipes by category or search by name.
- Localization in English and Ukrainian with language selection on `/start` or `/language`.

## Getting Started

### Prerequisites

- .NET 10 SDK (preview) or newer.
- MongoDB instance.
- Telegram bot token from [BotFather](https://t.me/botfather).
- OpenAI API key (for LLM parsing).

### Configuration

Update `src/CookFinder.Bot/appsettings.json`:

```json
{
  "Telegram": {
    "Token": "YOUR_TELEGRAM_BOT_TOKEN"
  },
  "Mongo": {
    "ConnectionString": "mongodb://localhost:27017",
    "Database": "cookfinder"
  },
  "OpenAI": {
    "ApiKey": "YOUR_OPENAI_API_KEY",
    "Model": "gpt-4o-mini"
  },
  "VideoSources": {
    "YouTubeApiKey": "YOUR_YOUTUBE_API_KEY",
    "InstagramAccessToken": "YOUR_INSTAGRAM_ACCESS_TOKEN",
    "TikTokApiKey": "YOUR_TIKTOK_API_KEY"
  }
}
```

### Run

```bash
cd src/CookFinder.Bot
dotnet run
```

### Telegram Commands

- `/start` - Choose language and shows usage instructions.
- `/language` - Change language.
- `/categories` - Browse saved recipes by category.
- `/search <name>` - Search recipes by title.

## How the LLM parsing works

- The bot fetches metadata for the video (title/description/author) via `IVideoMetadataClient`.
- It sends that metadata to OpenAI Chat Completions via `OpenAiRecipeParser`.
- The response must be JSON (title/description/ingredients/steps). If the LLM fails, the pipeline falls back to a rule-based parser.

## How video metadata is selected

The bot uses a factory (`IVideoMetadataClientFactory`) to choose the correct platform client based on the link host. Each platform client is currently stubbed but ready for API wiring:

- YouTube links resolve to `YouTubeMetadataClient`.
- Instagram links resolve to `InstagramMetadataClient`.
- TikTok links resolve to `TikTokMetadataClient`.
- Unknown hosts fall back to `StubVideoMetadataClient`.

## How to connect to video platforms

The MVP currently uses stub clients. Replace them with platform-specific API calls:

- **YouTube Shorts**: Use YouTube Data API v3 (requires an API key). Call `videos.list` with the video ID to fetch title/description/channel.
- **Instagram Reels**: Use the Instagram Graph API (requires a Facebook App + access token). Request the reel media fields for caption/owner.
- **TikTok**: TikTok’s official APIs are limited and typically require approval (e.g., TikTok Research API). For MVPs, teams often use oEmbed or third-party scraping with caution.

## Next Steps

### TODO

- [ ] Replace the stub platform clients with YouTube/Instagram/TikTok API clients.
- [ ] Add a summarization/translation service for structured recipes.
- [ ] Add inline navigation for paging.
- [ ] Add pricing/quota notes for Google/Meta APIs.
