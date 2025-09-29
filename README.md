# EMA 50/200 Scanner

## Overview

This project is an automated scanner for EMA (Exponential Moving Average) 50/200 cross signals on all USDT spot trading pairs from Binance.  
It continuously monitors the market and sends email notifications when a Golden Cross or Death Cross is detected.

- **Golden Cross:** EMA50 crosses above EMA200 (bullish signal)
- **Death Cross:** EMA50 crosses below EMA200 (bearish signal)

## Features

- Scans all USDT spot pairs on Binance at configurable intervals.
- Detects EMA cross signals (Golden/Death Cross) for each symbol.
- Sends email notifications when a new signal is detected.
- Configurable via `appsettings.json` (excluded from Git for security).
- Supports concurrency and interval selection.

## Requirements

- .NET 9 SDK
- Binance API Key and Secret
- Gmail account and App Password (for sending notifications)
- NuGet packages:
  - Binance.Net
  - CryptoExchange.Net
  - Spectre.Console
  - Microsoft.Extensions.Configuration.Json

## Configuration

Create a file named `appsettings.json` in the `EMA` directory with the following structure:
{ 
  "Binance": { "ApiKey": "YOUR_BINANCE_API_KEY", "ApiSecret": "YOUR_BINANCE_API_SECRET" }, 
  "Email": { "GmailUser": "your_gmail@gmail.com", "GmailAppPass": "your_gmail_app_password", 
  "NotifyTo": "recipient_email@gmail.com" }, 
  "EMA": { "ShortPeriod": 50, "LongPeriod": 200, 
  "Interval": "OneDay" // Options: OneMinute, ThreeMinutes, FiveMinutes, FifteenMinutes, ThirtyMinutes, OneHour, TwoHour, FourHour, SixHour, EightHour, TwelveHour, OneDay, ThreeDay, OneWeek, OneMonth }, 
  "App": { "MaxConcurrency": 10 } 
}

> **Note:**  
> Do not commit your `appsettings.json` to GitHub. The file is excluded via `.gitignore` for security.

## How to Run

1. **Restore NuGet packages:**
2. **Build the project:**
3. **Run the scanner:**
## Customization

- **EMA Periods:** Change `ShortPeriod` and `LongPeriod` in `appsettings.json`.
- **Interval:** Set `Interval` to any supported value (see above).
- **Concurrency:** Adjust `MaxConcurrency` for performance tuning.
- **Email:** Update Gmail credentials and recipient as needed.

## Security

- Keep your API keys and email credentials private.
- Use environment variables or secret managers for production deployments.

## License

This project is provided for educational and personal use.  
Please review Binance and Gmail API terms before deploying in production.
