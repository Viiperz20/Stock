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
        // 🔑 Finnhub API-Schlüssel für Aktienkurs und ExchangeRate API-Schlüssel für den Wechselkurs
        string finnhubApiKey = "d1rtm7pr01qskg7q9rp0d1rtm7pr01qskg7q9rpg"; // Dein Finnhub API-Schlüssel
        string exchangeRateApiKey = "08865cd18e1aa40e2458a9a5"; // Dein ExchangeRate API-Schlüssel
        string ntfyTopic = "mein-script";

        // URL für die CSV-Datei auf GitHub
        string csvUrl = "https://raw.githubusercontent.com/Viiperz20/Stock/main/aktien.csv";  // Dein GitHub Raw-Link

        var stocks = await LoadStocksFromWeb(csvUrl);

        using (HttpClient client = new HttpClient())
        {
            // Wechselkurs USD -> EUR holen (ExchangeRate API)
            string fxUrl = $"https://v6.exchangerate-api.com/v6/{exchangeRateApiKey}/latest/USD";
            string fxResult = await client.GetStringAsync(fxUrl);
            using JsonDocument fxDoc = JsonDocument.Parse(fxResult);

            decimal fxRate = fxDoc.RootElement.GetProperty("conversion_rates").GetProperty("EUR").GetDecimal();
            Console.WriteLine($"Wechselkurs USD -> EUR: {fxRate}");

            // Alle Aktien prüfen
            foreach (var stock in stocks)
            {
                try
                {
                    // Kurs von Finnhub holen
                    string stockUrl = $"https://finnhub.io/api/v1/quote?symbol={stock.Symbol}&token={finnhubApiKey}";
                    string stockResult = await client.GetStringAsync(stockUrl);
                    using JsonDocument stockDoc = JsonDocument.Parse(stockResult);

                    decimal kursUSD = stockDoc.RootElement.GetProperty("c").GetDecimal();
                    decimal kursEUR = kursUSD * fxRate;

                    Console.WriteLine($"{stock.Symbol}: {kursUSD:F2} USD ≈ {kursEUR:F2} EUR (Schwelle: {stock.ThresholdEUR} EUR)");

                    // Benachrichtigung senden, wenn Schwelle erreicht
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

    // CSV von GitHub laden
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
