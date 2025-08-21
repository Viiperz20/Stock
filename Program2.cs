using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Globalization;

class ConsoleApp1
{
    static async Task Main(string[] args)
    {
        // API Key & Topic aus Environment Variables holen
        string apiKey = Environment.GetEnvironmentVariable("ALPHA_KEY");
        string ntfyTopic = Environment.GetEnvironmentVariable("NTFY_TOPIC");

        string symbol = "NVDA";             // Nvidia
        decimal schwelleUSD = 170.00m;      // Alarm ab 170 USD

        string stockUrl = $"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={symbol}&apikey={apiKey}";
        string fxUrl = $"https://www.alphavantage.co/query?function=CURRENCY_EXCHANGE_RATE&from_currency=USD&to_currency=EUR&apikey={apiKey}";

        using (HttpClient client = new HttpClient())
        {
            // 1. Aktienkurs abrufen
            string stockResult = await client.GetStringAsync(stockUrl);
            using JsonDocument stockDoc = JsonDocument.Parse(stockResult);
            JsonElement stockRoot = stockDoc.RootElement;

            if (!stockRoot.TryGetProperty("Global Quote", out JsonElement quote))
            {
                Console.WriteLine("Keine Kursdaten erhalten!");
                return;
            }

            string preisStr = quote.GetProperty("05. price").GetString();
            decimal kursUSD = decimal.Parse(preisStr, CultureInfo.InvariantCulture);

            Console.WriteLine($"Aktueller Kurs von {symbol}: {kursUSD} USD");

            // 2. Wechselkurs abrufen
            string fxResult = await client.GetStringAsync(fxUrl);
            using JsonDocument fxDoc = JsonDocument.Parse(fxResult);
            JsonElement fxRoot = fxDoc.RootElement;

            string fxRateStr = fxRoot
                .GetProperty("Realtime Currency Exchange Rate")
                .GetProperty("5. Exchange Rate")
                .GetString();

            decimal fxRate = decimal.Parse(fxRateStr, CultureInfo.InvariantCulture);

            Console.WriteLine($"Wechselkurs USD -> EUR: {fxRate}");

            // 3. Umrechnen
            decimal kursEUR = kursUSD * fxRate;
            Console.WriteLine($"Kurs von {symbol} in EUR: {kursEUR:F2} €");

            // 4. ntfy Push senden
            string nachricht = $"🚀 {symbol} hat {kursUSD:F2} USD (≈ {kursEUR:F2} €) erreicht!";
            var content = new StringContent(nachricht);
            var response = await client.PostAsync($"https://ntfy.sh/{ntfyTopic}", content);

            Console.WriteLine($"Push gesendet: {response.StatusCode}");
        }
    }
}
