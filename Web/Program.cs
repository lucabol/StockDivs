using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OutputCaching;
using StackExchange.Redis;

var quotesEndpoint = Environment.GetEnvironmentVariable("QUOTES_ENDPOINT")
    ?? throw new Exception("QUOTES_ENDPOINT environment variable is not set");
var tickers = Environment.GetEnvironmentVariable("TICKERS")
    ?? throw new Exception("TICKERS environment variable is not set");
var redis = Environment.GetEnvironmentVariable("REDIS")
    ?? throw new Exception("REDIS environment variable is not set");

var builder = WebApplication.CreateSlimBuilder(args);

#if !DOCKER_BUILD
builder.AddServiceDefaults();
#endif

builder.Services.AddHttpClient();
builder.Services.AddStackExchangeRedisOutputCache(
    //options => options.Configuration = redis
    options => options.ConfigurationOptions = new ConfigurationOptions
    {
        EndPoints = { redis, "6379" },
        ConnectRetry = 2,
        AbortOnConnectFail = false,
        ConnectTimeout = 500,
        SyncTimeout = 500,
     }
);
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(builder =>
        builder.Expire(TimeSpan.FromSeconds(10)));
});

var app = builder.Build();

app.UseStaticFiles();
app.UseOutputCache();

app.MapGet("/", [OutputCache] async (HttpClient client) =>
{
    var ticks = tickers.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    var tickersPrices = await GetStocksPrices(client, ticks);
    var htmlTable = GenerateHtmlTable(tickersPrices);

    return new HtmlResult(html(htmlTable));
});

app.MapGet("/health", () => "OK");

app.Run();

string GenerateHtmlTable(Dictionary<string, double> data)
{
    StringBuilder html = new StringBuilder();

    // Start the table
    html.AppendLine("<table>");
    html.AppendLine("<tr><th>Ticker</th><th>Price</th></tr>");

    // Add rows for each key-value pair in the dictionary
    foreach (var item in data)
    {
        html.AppendLine(
            $"<tr><td>{item.Key}</td><td>{item.Value:0.00}</td></tr>");
    }

    // End the table
    html.AppendLine("</table>");

    return html.ToString();
}

async Task<Dictionary<string,double>> GetStocksPrices(HttpClient client, string[] tickers) {
    // Create a 2D array to store the ticker and price
    string[,] stocks = new string[tickers.Length, 2];

    // Craft the body of a POST request in the format
    // { "symbols": ["AAPL", "MSFT", "GOOGL", "AMZN"] }
    var tickDict = new Dictionary<string, string[]> {
        { "symbols", tickers }
    };
    var body = JsonSerializer.Serialize(
        tickDict,
        StockInJsonContext.Default.DictionaryStringStringArray);
    
    // Send the POST request to the stock service
    var quotesUrl = $"{quotesEndpoint}/api/quotes";

    var response = await client.PostAsync(quotesUrl,
        new StringContent(body, Encoding.UTF8, MediaTypeNames.Application.Json));
        
    response.EnsureSuccessStatusCode();

    // Read the response content
    var content = await response.Content.ReadAsStringAsync();

    // Deserialize the JSON response into a dictionary
    var dict = JsonSerializer.Deserialize(content, StockJsonContext.Default.DictionaryStringDouble)
        ?? throw new Exception("Failed to deserialize the response");

    return dict;
}

string html(string tableBody) => @$"<!doctype html>
<html>
    <head>
    <link rel=""icon"" href=""favicon.ico"" type=""image/x-icon"">
    <title>My Stocks</title>
    <style>{css()}</style>
    </head>
    <body>
        <h1>My Stocks</h1>
        {tableBody}
        <p>As of {DateTime.Now:O}</p>
    </body>
</html>";

string css() => @"
body {
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; /* Corporate font */
    font-size: large;
}
table {
    width: 100%;
    border-collapse: collapse;
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; /* Corporate font */
    font-size: 16px; /* Increase font size */
    color: #333; /* Corporate text color */
}
table tr:nth-child(even) {
    background-color: #f9f9f9; /* Light grey for alternating rows */
}
table th, table td {
    padding: 12px; /* Add padding to table cells */
    text-align: center; /* Center-align table headers */
    border: 1px solid #ddd; /* Light grey border */
}
table th {
    background-color: #004080; /* Corporate blue background */
    color: white; /* White text color */
}
table tr:hover {
    background-color: #e6f2ff; /* Light blue hover effect */
}";

[JsonSerializable(typeof(Dictionary<string, string[]>))]
public partial class StockInJsonContext : JsonSerializerContext
{
}

[JsonSerializable(typeof(Dictionary<string, double>))]
public partial class StockJsonContext : JsonSerializerContext
{
}

class HtmlResult(string html) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = MediaTypeNames.Text.Html;
        httpContext.Response.ContentLength = Encoding.UTF8.GetByteCount(html);
        return httpContext.Response.WriteAsync(html);
    }
}