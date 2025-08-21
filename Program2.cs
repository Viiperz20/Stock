using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Globalization;
using System.Collections.Generic;

class ConsoleApp1
{
    class StockConfig
    {
        public string Symbol { get; set; }
        public decimal ThresholdEUR { get; set; }
    }

    static async Task Main(string[] args)
    {
        string apiKey = "2KFFV8DIGOLMJTIS"; // besser über Secrets
        string ntfyTopic = "mein-script";

        string csvUrl = "https://docs.google.com/spreadsheets/d/1F24wlEfp9GhMTJrIIJKRV8aGEUJ1DnD6/export?format=csv";

        var stocks = await LoadStocksFromWeb(csvUrl);
        if (stocks.Count == 0)
        {
            Console.WriteLine("Keine Aktien in der Liste gefunden!");
            return;
        }

        using HttpClient client = new HttpClient();

        // Wechselkurs USD -> EUR abrufen
        string fxUrl = $"https://www.alphavantage.co/query?function=CURRENCY_EXCHANGE_RATE&from_currency=USD&to_currency=EUR&apikey={apiKey}";
        string fxResult = await client.GetStringAsync(fxUrl);
        using JsonDocument fxDoc = JsonDocument.Parse(fxResult);
        string fxRateStr = fxDoc.RootElement
            .GetProperty("Realtime Currency Exchange Rate")
            .GetProperty("5. Exchange Rate")
            .GetString();
        decimal fxRate = decimal.Parse(fxRateStr, CultureInfo.InvariantCulture);
        Console.WriteLine($"Wechselkurs USD -> EUR: {fxRate}");

        foreach (var stock in stocks)
        {
            string stockUrl = $"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={stock.Symbol}&apikey={apiKey}";
            string stockResult = await client.GetStringAsync(stockUrl);
            using JsonDocument stockDoc = JsonDocument.Parse(stockResult);

            if (!stockDoc.RootElement.TryGetProperty("Global Quote", out JsonElement quote))
            {
                Console.WriteLine($"Keine Kursdaten für {stock.Symbol} erhalten!");
                continue;
            }

            string preisStr = quote.GetProperty("05. price").GetString();
            decimal kursUSD = decimal.Parse(preisStr, CultureInfo.InvariantCulture);
            decimal kursEUR = kursUSD * fxRate;

            Console.WriteLine($"{stock.Symbol}: {kursUSD:F2} USD (≈ {kursEUR:F2} €)");

            // Prüfen, ob Kurs in EUR über Schwelle liegt
            if (kursEUR >= stock.ThresholdEUR)
            {
                string nachricht = $"🚀 {stock.Symbol} hat {kursEUR:F2} € erreicht (Schwelle {stock.ThresholdEUR:F2} €)!";
                var content = new StringContent(nachricht);
                var response = await client.PostAsync($"https://ntfy.sh/{ntfyTopic}", content);
                Console.WriteLine($"Push gesendet: {response.StatusCode}");
            }
        }
    }

    static async Task<List<StockConfig>> LoadStocksFromWeb(string url)
    {
        using HttpClient client = new HttpClient();
        string csv = await client.GetStringAsync(url);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var stocks = new List<StockConfig>();

        for (int i = 1; i < lines.Length; i++) // Header überspringen
        {
            var parts = lines[i].Split(',');
            if (parts.Length < 2) continue;

            if (decimal.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal thresholdEUR))
            {
                stocks.Add(new StockConfig
                {
                    Symbol = parts[0].Trim(),
                    ThresholdEUR = thresholdEUR
                });
            }
        }
        return stocks;
    }
}
