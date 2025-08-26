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
    public string Status { get; set; } // "G", "L" oder ""
}

class ConsoleApp1
{
    static async Task Main(string[] args)
    {
        // 🔑 API-Keys
        string finnhubApiKey = "d1rtm7pr01qskg7q9rp0d1rtm7pr01qskg7q9rpg"; 
        string exchangeRateApiKey = "08865cd18e1aa40e2458a9a5"; 
        string ntfyTopic = "mein-script";

        // CSV-Datei von GitHub laden (READ-ONLY)
        string csvUrl = "https://raw.githubusercontent.com/Viiperz20/Stock/main/aktien.csv";
        var stocks = await LoadStocksFromWeb(csvUrl);

        using (HttpClient client = new HttpClient())
        {
            // Wechselkurs USD -> EUR
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

                    Console.WriteLine($"{stock.Symbol}: {kursUSD:F2} USD ≈ {kursEUR:F2} EUR (Schwelle: {stock.ThresholdEUR} EUR, CSV-Status: {stock.Status})");

                    // ---- NEUE BENACHRICHTIGUNGS-LOGIK ----
                    if (stock.Status == "G")
                    {
                        if (kursEUR > stock.ThresholdEUR)
                        {
                            string nachricht = $"🚀 {stock.Symbol} über Schwelle! {kursEUR:F2} € > {stock.ThresholdEUR} €";
                            await SendNotification(client, ntfyTopic, nachricht);
                        }
                    }
                    else if (stock.Status == "L")
                    {
                        if (kursEUR < stock.ThresholdEUR)
                        {
                            string nachricht = $"📉 {stock.Symbol} unter Schwelle! {kursEUR:F2} € < {stock.ThresholdEUR} €";
                            await SendNotification(client, ntfyTopic, nachricht);
                        }
                    }
                    else // Status leer
                    {
                        if (kursEUR > stock.ThresholdEUR)
                        {
                            string nachricht = $"🚀 {stock.Symbol} über Schwelle! {kursEUR:F2} € > {stock.ThresholdEUR} €";
                            await SendNotification(client, ntfyTopic, nachricht);
                        }
                        else if (kursEUR < stock.ThresholdEUR)
                        {
                            string nachricht = $"📉 {stock.Symbol} unter Schwelle! {kursEUR:F2} € < {stock.ThresholdEUR} €";
                            await SendNotification(client, ntfyTopic, nachricht);
                        }
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
            var parts = lines[i].Split(',', '\t'); // CSV oder TAB getrennt
            if (parts.Length < 2) continue;

            if (decimal.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal threshold))
            {
                string status = parts.Length >= 3 ? parts[2].Trim() : "";
                stocks.Add(new StockConfig
                {
                    Symbol = parts[0].Trim(),
                    ThresholdEUR = threshold,
                    Status = status
                });
            }
        }
        return stocks;
    }

    // Push-Nachricht senden
    static async Task SendNotification(HttpClient client, string topic, string message)
    {
        var content = new StringContent(message);
        var response = await client.PostAsync($"https://ntfy.sh/{topic}", content);
        Console.WriteLine($"📩 Push gesendet ({response.StatusCode})");
    }
}
