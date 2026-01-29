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

### Register for API keys

Use the steps below to create the required credentials for each service and then copy them into
`src/CookFinder.Bot/appsettings.json`.

#### Telegram Bot Token

1. Open [@BotFather](https://t.me/botfather) in Telegram.
2. Run `/newbot`, follow the prompts, and choose a name + username.
3. Copy the bot token that BotFather returns.
4. Paste it into `Telegram:Token` in `appsettings.json`.

#### OpenAI API Key

1. Sign in to [OpenAI](https://platform.openai.com/).
2. Open **Settings → API keys** (or go to https://platform.openai.com/api-keys).
3. Create a new secret key and copy it.
4. Paste it into `OpenAI:ApiKey` in `appsettings.json`.

#### YouTube Data API v3 Key

1. Go to the [Google Cloud Console](https://console.cloud.google.com/).
2. Create or select a project.
3. Enable **YouTube Data API v3** for the project.
4. Open **APIs & Services → Credentials**, create an **API key**, and copy it.
5. Paste it into `VideoSources:YouTubeApiKey` in `appsettings.json`.

#### Instagram Graph API Access Token

1. Go to the [Meta for Developers](https://developers.facebook.com/) portal and create an app.
2. If you do not see **Instagram Graph API** in the use-case list, pick **Manage messaging & content on Instagram**
   or **Embed Facebook, Instagram and Threads content in other websites** to create the app.
3. After the app is created, open the app dashboard and click **Add product**, then add **Instagram Graph API**.
4. Create or connect a Facebook Page and an Instagram Business/Creator account.
5. Use the Graph API Explorer (or your own auth flow) to generate a long-lived access token.
6. Paste it into `VideoSources:InstagramAccessToken` in `appsettings.json`.

#### TikTok API Key (if available)

1. Sign up at [TikTok for Developers](https://developers.tiktok.com/).
2. Create an app and follow the verification/approval flow.
3. Once approved, generate credentials (API key or access token, depending on the product).
4. Paste the key into `VideoSources:TikTokApiKey` in `appsettings.json`.

> Note: TikTok’s official APIs are limited and may require approval. For the MVP, you can leave the
> TikTok key empty while using the stub metadata client.

### Set up MongoDB locally

#### Option A: Local MongoDB Community Server

1. Download MongoDB Community Server from https://www.mongodb.com/try/download/community.
2. Install and follow the default setup for your OS.
3. Start the MongoDB service (`mongod`).
4. Confirm it is running:
   ```bash
   mongosh --eval "db.runCommand({ ping: 1 })"
   ```
5. Keep the default connection string in `appsettings.json`:
   ```json
   "Mongo": {
     "ConnectionString": "mongodb://localhost:27017",
     "Database": "cookfinder"
   }
   ```

#### Option B: MongoDB via Docker

```bash
docker run --name cookfinder-mongo -d -p 27017:27017 mongo:7
```

Then keep the same `mongodb://localhost:27017` connection string.

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

## Project Structure

```
src/CookFinder.Bot
├── Configurations
├── Models
├── Repositories
├── Resources
└── Services
    ├── Bot
    ├── Localization
    ├── Metadata
    ├── Parsing
    └── Recipes
```

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
