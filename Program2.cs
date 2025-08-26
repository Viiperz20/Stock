using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Globalization;
using System.Collections.Generic;
using System.IO;

class StockConfig
{
    public string Symbol { get; set; }
    public decimal ThresholdEUR { get; set; }
    public string Status { get; set; } // "G", "L" oder null
}

class ConsoleApp1
{
    static async Task Main(string[] args)
    {
        string finnhubApiKey = "d1rtm7pr01qskg7q9rp0d1rtm7pr01qskg7q9rpg"; 
        string exchangeRateApiKey = "08865cd18e1aa40e2458a9a5"; 
        string ntfyTopic = "mein-script";

        string csvUrl = "https://raw.githubusercontent.com/Viiperz20/Stock/main/aktien.csv";

        var stocks = await LoadStocksFromWeb(csvUrl);

        using (HttpClient client = new HttpClient())
        {
            string fxUrl = $"https://v6.exchangerate-api.com/v6/{exchangeRateApiKey}/latest/USD";
            string fxResult = await client.GetStringAsync(fxUrl);
            using JsonDocument fxDoc = JsonDocument.Parse(fxResult);

            decimal fxRate = fxDoc.RootElement.GetProperty("conversion_rates").GetProperty("EUR").GetDecimal();
            Console.WriteLine($"Wechselkurs USD -> EUR: {fxRate}");

            foreach (var stock in stocks)
            {
                try
                {
                    string stockUrl = $"https://finnhub.io/api/v1/quote?symbol={stock.Symbol}&token={finnhubApiKey}";
                    string stockResult = await client.GetStringAsync(stockUrl);
                    using JsonDocument stockDoc = JsonDocument.Parse(stockResult);

                    decimal kursUSD = stockDoc.RootElement.GetProperty("c").GetDecimal();
                    decimal kursEUR = kursUSD * fxRate;

                    Console.WriteLine($"{stock.Symbol}: {kursUSD:F2} USD ≈ {kursEUR:F2} EUR (Schwelle: {stock.ThresholdEUR} EUR)");

                    // Nur G oder L setzen, wenn Wert abweicht
                    if (kursEUR > stock.ThresholdEUR)
                    {
                        stock.Status = "G"; // größer
                        string nachricht = $"🚀 {stock.Symbol} über Schwelle! {kursUSD:F2} USD (≈ {kursEUR:F2} €) > {stock.ThresholdEUR} €";
                        var content = new StringContent(nachricht);
                        var response = await client.PostAsync($"https://ntfy.sh/{ntfyTopic}", content);
                        Console.WriteLine($"📩 Push gesendet ({response.StatusCode})");
                    }
                    else if (kursEUR < stock.ThresholdEUR)
                    {
                        stock.Status = "L"; // kleiner
                        string nachricht = $"📉 {stock.Symbol} unter Schwelle! {kursUSD:F2} USD (≈ {kursEUR:F2} €) < {stock.ThresholdEUR} €";
                        var content = new StringContent(nachricht);
                        var response = await client.PostAsync($"https://ntfy.sh/{ntfyTopic}", content);
                        Console.WriteLine($"📩 Push gesendet ({response.StatusCode})");
                    }
                    else
                    {
                        stock.Status = ""; // gleich -> nichts eintragen
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Fehler bei {stock.Symbol}: {ex.Message}");
                }
            }
        }

        // CSV lokal überschreiben
        SaveStocksToCsv("aktien_mit_status.csv", stocks);
    }

    static async Task<List<StockConfig>> LoadStocksFromWeb(string url)
    {
        using var client = new HttpClient();
        string csv = await client.GetStringAsync(url);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var stocks = new List<StockConfig>();

        for (int i = 1; i < lines.Length; i++) // erste Zeile = Header
        {
            var parts = lines[i].Split(',', '\t'); // CSV oder TAB getrennt
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

    static void SaveStocksToCsv(string path, List<StockConfig> stocks)
    {
        using (var writer = new StreamWriter(path))
        {
            writer.WriteLine("Symbol,ThresholdEUR,Status"); // Header
            foreach (var stock in stocks)
            {
                writer.WriteLine($"{stock.Symbol},{stock.ThresholdEUR},{stock.Status}");
            }
        }
        Console.WriteLine($"💾 CSV gespeichert unter: {path}");
    }
}
