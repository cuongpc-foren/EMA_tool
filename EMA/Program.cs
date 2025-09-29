using Binance.Net.Clients;
using Binance.Net.Enums;
using CryptoExchange.Net.Authentication;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using System.Net;
using System.Net.Mail;
using System.Text.Json;

enum EmaIntervalOption
{
    OneMinute,
    ThreeMinutes,
    FiveMinutes,
    FifteenMinutes,
    ThirtyMinutes,
    OneHour,
    TwoHour,
    FourHour,
    SixHour,
    EightHour,
    TwelveHour,
    OneDay,
    ThreeDay,
    OneWeek,
    OneMonth
}

class Program
{
    const string StateFile = "lastChecked.json";

    static async Task Main()
    {
        // ======= Load configuration =======
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        string apiKey = config["Binance:ApiKey"];
        string apiSecret = config["Binance:ApiSecret"];
        string gmailUser = config["Email:GmailUser"];
        string gmailAppPass = config["Email:GmailAppPass"];
        string notifyTo = config["Email:NotifyTo"];
        int shortPeriod = int.Parse(config["EMA:ShortPeriod"]);
        int longPeriod = int.Parse(config["EMA:LongPeriod"]);
        string intervalOptionStr = config["EMA:Interval"] ?? "OneDay";
        EmaIntervalOption intervalOption = Enum.TryParse<EmaIntervalOption>(intervalOptionStr, out var parsedOpt) ? parsedOpt : EmaIntervalOption.OneDay;
        KlineInterval interval = intervalOption switch
        {
            EmaIntervalOption.OneMinute => KlineInterval.OneMinute,
            EmaIntervalOption.ThreeMinutes => KlineInterval.ThreeMinutes,
            EmaIntervalOption.FiveMinutes => KlineInterval.FiveMinutes,
            EmaIntervalOption.FifteenMinutes => KlineInterval.FifteenMinutes,
            EmaIntervalOption.ThirtyMinutes => KlineInterval.ThirtyMinutes,
            EmaIntervalOption.OneHour => KlineInterval.OneHour,
            EmaIntervalOption.TwoHour => KlineInterval.TwoHour,
            EmaIntervalOption.FourHour => KlineInterval.FourHour,
            EmaIntervalOption.SixHour => KlineInterval.SixHour,
            EmaIntervalOption.EightHour => KlineInterval.EightHour,
            EmaIntervalOption.TwelveHour => KlineInterval.TwelveHour,
            EmaIntervalOption.OneDay => KlineInterval.OneDay,
            EmaIntervalOption.ThreeDay => KlineInterval.ThreeDay,
            EmaIntervalOption.OneWeek => KlineInterval.OneWeek,
            EmaIntervalOption.OneMonth => KlineInterval.OneMonth,
            _ => KlineInterval.OneDay
        };
        int maxConcurrency = int.TryParse(config["App:MaxConcurrency"], out var mc) ? mc : 10;

        var client = new BinanceRestClient(o =>
        {
            o.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
        });

        AnsiConsole.Write(
            new FigletText("EMA 50/200 Scanner")
                .Color(Spectre.Console.Color.Green)
                .Centered());

        // ======= Fetch ALL USDT spot symbols =======
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Fetching all USDT trading pairs...", async _ => { });

        var ex = await client.SpotApi.ExchangeData.GetExchangeInfoAsync();
        if (!ex.Success)
        {
            Fail($"Cannot get exchange info: {ex.Error}");
            return;
        }

        var allSymbols = ex.Data.Symbols
            .Where(s => s.QuoteAsset == "USDT" && s.Status == SymbolStatus.Trading)
            .Select(s => s.Name)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        if (allSymbols.Count == 0)
        {
            Fail("No active USDT pairs found.");
            return;
        }

        Ok($"Scanning {allSymbols.Count} USDT pairs ({intervalOption}, EMA {shortPeriod}/{longPeriod}). Email: {gmailUser} → {notifyTo}");

        var lastChecked = LoadLastChecked();
        var semaphore = new SemaphoreSlim(maxConcurrency);

        // ======= Continuous monitoring loop =======
        while (true)
        {
            try
            {
                Info($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | scan start");

                const int batchSize = 25;
                for (int i = 0; i < allSymbols.Count; i += batchSize)
                {
                    var batch = allSymbols.Skip(i).Take(batchSize).ToList();
                    var tasks = batch.Select(async sym => {
                        await semaphore.WaitAsync();
                        try {
                            await ScanOneSymbol(
                                client, sym, interval, shortPeriod, longPeriod,
                                gmailUser, gmailAppPass, notifyTo, lastChecked);
                        } finally {
                            semaphore.Release();
                        }
                    });
                    await Task.WhenAll(tasks);
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }

                SaveLastChecked(lastChecked);
                Info($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | scan done, waiting 10 minutes...");
                await Task.Delay(TimeSpan.FromMinutes(10));
            }
            catch (Exception exn)
            {
                Fail($"Loop error: {exn.Message}");
                await Task.Delay(TimeSpan.FromMinutes(2));
            }
        }
    }

    static Dictionary<string, DateTime?> LoadLastChecked()
    {
        try
        {
            if (!File.Exists(StateFile)) return new Dictionary<string, DateTime?>();
            var json = File.ReadAllText(StateFile);
            var dict = JsonSerializer.Deserialize<Dictionary<string, DateTime?>>(json);
            return dict ?? new Dictionary<string, DateTime?>();
        }
        catch
        {
            return new Dictionary<string, DateTime?>();
        }
    }

    static void SaveLastChecked(Dictionary<string, DateTime?> dict)
    {
        try
        {
            var json = JsonSerializer.Serialize(dict);
            File.WriteAllText(StateFile, json);
        }
        catch (Exception ex)
        {
            Warn($"Save state error: {ex.Message}");
        }
    }

    static async Task ScanOneSymbol(
        BinanceRestClient client,
        string sym,
        KlineInterval interval,
        int shortPeriod,
        int longPeriod,
        string gmailUser,
        string gmailAppPass,
        string notifyTo,
        Dictionary<string, DateTime?> lastCheckedDict)
    {
        try
        {
            if (!lastCheckedDict.ContainsKey(sym))
                lastCheckedDict[sym] = null;

            var res = await client.SpotApi.ExchangeData.GetKlinesAsync(sym, interval, limit: 400);
            if (!res.Success)
            {
                Warn($"{sym} klines error: {res.Error}");
                return;
            }

            var all = res.Data.ToList();
            if (all.Count < longPeriod + 2) return;

            // Remove currently open candle
            var last = all[^1];
            var closed = last.CloseTime <= DateTime.UtcNow ? all : all.Take(all.Count - 1).ToList();
            var lastClosed = closed[^1];

            // Only act once per newly-closed daily candle
            if (lastCheckedDict[sym] != null && lastClosed.CloseTime <= lastCheckedDict[sym]) return;

            var closes = closed.Select(k => k.ClosePrice).ToList();
            var emaShort = ComputeEmaSeries(closes, shortPeriod);
            var emaLong = ComputeEmaSeries(closes, longPeriod);

            int n = closes.Count;
            decimal sPrev = emaShort[n - 2], sNow = emaShort[n - 1];
            decimal lPrev = emaLong[n - 2], lNow = emaLong[n - 1];
            decimal pxNow = closes[^1];

            bool golden = sPrev <= lPrev && sNow > lNow;   // cross up
            bool death = sPrev >= lPrev && sNow < lNow;   // cross down

            if (golden)
            {
                Ok($"{sym} GOLDEN CROSS");
                await SendMailSafe(gmailUser, gmailAppPass, notifyTo,
                    $"[Golden Cross] {sym}",
                    $@"Golden Cross on {sym} (1D)
                    Price: {pxNow}
                    EMA{shortPeriod} prev: {sPrev} -> now: {sNow}
                    EMA{longPeriod}  prev: {lPrev} -> now: {lNow}");
            }
            else if (death)
            {
                Warn($"{sym} Death Cross");
                await SendMailSafe(gmailUser, gmailAppPass, notifyTo,
                    $"[Death Cross] {sym}",
                    $@"Death Cross on {sym} (1D)
                    Price: {pxNow}
                    EMA{shortPeriod} prev: {sPrev} -> now: {sNow}
                    EMA{longPeriod}  prev: {lPrev} -> now: {lNow}");
            }

            lastCheckedDict[sym] = lastClosed.CloseTime;
        }
        catch (Exception ex)
        {
            Warn($"{sym} scan error: {ex.Message}");
        }
    }

    static List<decimal> ComputeEmaSeries(IReadOnlyList<decimal> prices, int period)
    {
        var ema = new List<decimal>(new decimal[prices.Count]);
        decimal seed = prices.Take(period).Average();
        for (int i = 0; i < period - 1; i++) ema[i] = seed;
        ema[period - 1] = seed;

        decimal k = 2m / (period + 1m);
        for (int i = period; i < prices.Count; i++)
            ema[i] = prices[i] * k + ema[i - 1] * (1 - k);

        return ema;
    }

    static async Task SendMailSafe(string from, string appPass, string to, string subject, string body)
    {
        int maxRetry = 3;
        for (int attempt = 1; attempt <= maxRetry; attempt++)
        {
            try
            {
                using var smtp = new SmtpClient("smtp.gmail.com", 587)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(from, appPass)
                };
                await smtp.SendMailAsync(new MailMessage(from, to, subject, body));
                Ok($"Email sent to {to}: {subject}");
                break;
            }
            catch (Exception ex)
            {
                if (attempt == maxRetry)
                    Fail($"SMTP error: {ex.Message}");
                else
                    Warn($"SMTP retry {attempt}: {ex.Message}");
                await Task.Delay(1000 * attempt);
            }
        }
    }

    static void Ok(string msg) => AnsiConsole.MarkupLine($"[green]{msg}[/]");
    static void Warn(string msg) => AnsiConsole.MarkupLine($"[yellow]{msg}[/]");
    static void Info(string msg) => AnsiConsole.MarkupLine($"[grey]{msg}[/]");
    static void Fail(string msg) => AnsiConsole.MarkupLine($"[red]{msg}[/]");
}
