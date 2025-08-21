using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Globalization;
using System.Collections.Generic;

class StockConfig
{
    public string Symbol { get; set; }
    public decimal ThresholdEUR { get; set; }
}

class ConsoleApp1
{
    static async Task Main(string[] args)
    {
        // 🔑 API-Key und ntfy-Topic HIER einsetzen
        string apiKey = "2KFFV8DIGOLMJTIS";
        string ntfyTopic = "mein-script";

        // 🔗 Google Drive Direktlink (uc?export=download&id=...)
        string csvUrl = "https://docs.google.com/spreadsheets/d/1zc7gNCnxdxPSTJTnZQl3FD4lU2CdmIr3AuMxMw0mDqQ/export?format=csv";

        // CSV laden
        var stocks = await LoadStocksFromWeb(csvUrl);

        using (HttpClient client = new HttpClient())
        {
            // Wechselkurs USD -> EUR holen
            string fxUrl = $"https://www.alphavantage.co/query?function=CURRENCY_EXCHANGE_RATE&from_currency=USD&to_currency=EUR&apikey={apiKey}";
            string fxResult = await client.GetStringAsync(fxUrl);
            using JsonDocument fxDoc = JsonDocument.Parse(fxResult);
            decimal fxRate = decimal.Parse(
                fxDoc.RootElement
                .GetProperty("Realtime Currency Exchange Rate")
                .GetProperty("5. Exchange Rate")
                .GetString(),
                CultureInfo.InvariantCulture);

            Console.WriteLine($"Wechselkurs USD -> EUR: {fxRate}");

            // Alle Aktien prüfen
            foreach (var stock in stocks)
            {
                try
                {
                    string stockUrl = $"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={stock.Symbol}&apikey={apiKey}";
                    string stockResult = await client.GetStringAsync(stockUrl);
                    using JsonDocument stockDoc = JsonDocument.Parse(stockResult);

                    if (!stockDoc.RootElement.TryGetProperty("Global Quote", out JsonElement quote))
                    {
                        Console.WriteLine($"⚠️ Keine Kursdaten für {stock.Symbol} erhalten!");
                        continue;
                    }

                    string preisStr = quote.GetProperty("05. price").GetString();
                    decimal kursUSD = decimal.Parse(preisStr, CultureInfo.InvariantCulture);
                    decimal kursEUR = kursUSD * fxRate;

                    Console.WriteLine($"{stock.Symbol}: {kursUSD:F2} USD ≈ {kursEUR:F2} EUR (Schwelle: {stock.ThresholdEUR} EUR)");

                    // Benachrichtigung, wenn Schwelle erreicht
                    if (kursEUR >= stock.ThresholdEUR)
                    {
                        string nachricht = $"🚀 {stock.Symbol} hat {kursUSD:F2} USD (≈ {kursEUR:F2} €) erreicht! Schwelle: {stock.ThresholdEUR} €";
                        var content = new StringContent(nachricht);
                        var response = await client.PostAsync($"https://ntfy.sh/{ntfyTopic}", content);
                        Console.WriteLine($"📩 Push gesendet ({response.StatusCode})");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Fehler bei {stock.Symbol}: {ex.Message}");
                }
            }
        }
    }

    // CSV von Google Drive laden
    static async Task<List<StockConfig>> LoadStocksFromWeb(string url)
    {
        using var client = new HttpClient();
        string csv = await client.GetStringAsync(url);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var stocks = new List<StockConfig>();

        for (int i = 1; i < lines.Length; i++) // erste Zeile = Header
        {
            var parts = lines[i].Split(',');
            if (parts.Length < 2) continue;

            if (decimal.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal threshold))
            {
                stocks.Add(new StockConfig
                {
                    Symbol = parts[0].Trim(),
                    ThresholdEUR = threshold
                });
            }
        }
        return stocks;
    }
}


