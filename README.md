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
