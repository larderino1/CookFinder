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

## Getting Started

### Prerequisites

- .NET 10 SDK (preview) or newer.
- MongoDB instance.
- Telegram bot token from [BotFather](https://t.me/botfather).
- OpenAI API key (for LLM parsing).

### Configuration

Update `src/CookFinder.Bot/appsettings.json` or set equivalent environment variables:

```json
{
  "Telegram": {
    "Token": "YOUR_TELEGRAM_BOT_TOKEN"
  },
  "Mongo": {
    "ConnectionString": "mongodb://localhost:27017",
    "Database": "cookfinder"
  },
  "Recipe": {
    "DefaultCategories": [
      "Breakfast",
      "Lunch",
      "Dinner",
      "Snack",
      "Dessert",
      "Drinks"
    ]
  },
  "OpenAI": {
    "ApiKey": "YOUR_OPENAI_API_KEY",
    "Model": "gpt-4o-mini",
    "Endpoint": "https://api.openai.com/v1/chat/completions"
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
- `/ingredients <name>` - Search recipes by ingredient.
- `/favorites` - Show saved favorite recipes.
- `/pantry <comma-separated list>` - Find recipes matching your pantry list.
- `/nutrition <name>` - Show nutrition estimate for a saved recipe.
- `/info` - Show quick help.

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

The bot uses a factory (`IVideoMetadataClientFactory`) to choose the correct platform client based on the link host:

- YouTube links resolve to `YouTubeMetadataClient`.
- Instagram links resolve to `InstagramMetadataClient`.
- TikTok links resolve to `TikTokMetadataClient`.
- Unknown hosts fall back to `StubVideoMetadataClient`.

## How video metadata is fetched

- **YouTube Shorts**: Uses YouTube Data API v3 (`videos.list`) with the API key to fetch title/description/channel.
- **Instagram Reels**: Uses the Instagram Graph API oEmbed endpoint with an access token.
- **TikTok**: Uses TikTok oEmbed (no token required).

## Deployment: DigitalOcean Droplet + MongoDB Atlas

### 1) Create MongoDB Atlas

1. Create a cluster and database user in MongoDB Atlas.
2. Allow network access for your Droplet IP.
3. Copy the SRV connection string and set it as `Mongo__ConnectionString`.
4. Set `Mongo__Database` to your chosen database name (e.g., `cookfinder`).

### 2) Create a DigitalOcean Droplet

1. Create an Ubuntu Droplet and add your SSH key.
2. SSH to the Droplet and install dependencies:

```bash
sudo apt update
sudo apt install -y git
# install .NET 10 SDK from Microsoft package feeds
```

### 3) Deploy the bot (manual)

```bash
git clone <your-repo-url>
cd CookFinder/src/CookFinder.Bot
dotnet publish -c Release -o out
```

### 4) Configure environment variables

Set environment variables (recommended) instead of editing `appsettings.json` on the server:

- `Telegram__Token`
- `Mongo__ConnectionString`
- `Mongo__Database`
- `OpenAI__ApiKey`
- `OpenAI__Model`
- `OpenAI__Endpoint`
- `VideoSources__YouTubeApiKey`
- `VideoSources__InstagramAccessToken`
- `VideoSources__TikTokApiKey`

### 5) Run as a systemd service

Create `/etc/systemd/system/cookfinder.service`:

```ini
[Unit]
Description=CookFinder Telegram Bot
After=network.target

[Service]
WorkingDirectory=/home/<user>/CookFinder/src/CookFinder.Bot
ExecStart=/usr/bin/dotnet /home/<user>/CookFinder/src/CookFinder.Bot/out/CookFinder.Bot.dll
Restart=always
RestartSec=5
Environment=Telegram__Token=...
Environment=Mongo__ConnectionString=...
Environment=Mongo__Database=cookfinder
Environment=OpenAI__ApiKey=...
Environment=OpenAI__Model=gpt-4o-mini
Environment=OpenAI__Endpoint=https://api.openai.com/v1/chat/completions
Environment=VideoSources__YouTubeApiKey=...
Environment=VideoSources__InstagramAccessToken=...
Environment=VideoSources__TikTokApiKey=...

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable cookfinder
sudo systemctl start cookfinder
sudo systemctl status cookfinder
```

### 6) Optional CI/CD with GitHub Actions

This repo includes a GitHub Actions workflow at `.github/workflows/deploy.yml`. It builds the bot, uploads the published output to your Droplet, and restarts the service.

1. **Branch trigger:** the workflow runs on pushes to `main`. If you deploy from another branch, update the workflow trigger.
2. **Create a deploy user:** ensure the user can restart the systemd service and write to the app directory.
3. **Add GitHub secrets:**

| Secret | Description |
| --- | --- |
| `DO_HOST` | Droplet public IP or hostname |
| `DO_USER` | SSH username |
| `DO_SSH_KEY` | Private key for the deploy user |
| `DO_SSH_PORT` | SSH port (optional, defaults to 22) |
| `DO_APP_DIR` | Path to the published app directory (e.g. `/home/<user>/apps/cookfinder`) |
| `DO_SERVICE_NAME` | Systemd service name (e.g. `cookfinder`) |

4. **Make sure the service has environment variables** for the bot and Atlas connection string (see section 4).

### 7) Pricing notes (high level)

- **DigitalOcean Droplet**: low-cost monthly tiers for small CPU/RAM are a common fit for bots.
- **MongoDB Atlas**: free/shared tiers exist; production usage typically needs a paid tier based on storage and region.
- **OpenAI**: costs scale by token usage for recipe parsing, translation, and nutrition estimation.

## Next Steps

### TODO

- [ ] Add inline navigation for paging.
- [ ] Add pricing/quota notes for Google/Meta APIs.
