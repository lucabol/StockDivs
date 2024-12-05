var builder = DistributedApplication.CreateBuilder(args);

var quotes = builder.AddDockerfile("quotes", "../QuoteService")
                    .WithHttpEndpoint(port: 80, targetPort:80);
var redis = builder.AddDockerfile("redis", "../", "Dockerfile.redis")
                    .WithEndpoint(port: 6379, targetPort: 6379, scheme:"tcp", isExternal:false, name:"redis");

builder.AddProject<Projects.Web>("web")
       .WithEnvironment("TICKERS", "AAPL MSFT TSLA")
       .WithEnvironment("REDIS", $"{redis.GetEndpoint("redis").Property(EndpointProperty.Host)}")
       .WithEnvironment("QUOTES_ENDPOINT", quotes.GetEndpoint("http"))
       .WaitFor(quotes)
       .WaitFor(redis);

builder.Build().Run();
