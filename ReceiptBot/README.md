# ReceiptBot — Telegram → Azure Document Intelligence (prebuilt-receipt)

A minimal, production-leaning .NET 8 console bot that:
- receives a photo/PDF of a receipt in Telegram,
- sends it to **Azure Document Intelligence** (`prebuilt-receipt`),
- replies with a clean summary (merchant, total, items),
- is structured to scale (add DB, queues, web UI).

## ✅ Prerequisites

- Windows 11 + VS Code
- .NET SDK 8.x (`dotnet --info`)
- Telegram bot token from **@BotFather**
- Azure resource: **Azure AI Document Intelligence** (endpoint + key)

## 🔧 Install dependencies

```bash
cd ReceiptBot/src/ReceiptBot

# Add packages (pin to latest stable in your environment)
dotnet add package Telegram.Bot
dotnet add package Azure.AI.DocumentIntelligence
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Microsoft.Extensions.Http
