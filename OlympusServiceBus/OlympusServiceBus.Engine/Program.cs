using Azure.Identity;
using Microsoft.Graph;
using OlympusServiceBus.Engine;

var builder = Host.CreateApplicationBuilder(args);

var graphOptions = builder.Configuration.GetSection("Graph").Get<GraphOptions>() ?? new GraphOptions();
builder.Services.AddSingleton(graphOptions);

builder.Services.AddSingleton(sp =>
{
    var opts = sp.GetRequiredService<GraphOptions>();
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DeviceCode");

    var credential = new DeviceCodeCredential(new DeviceCodeCredentialOptions
    {
        TenantId = opts.TenantId,
        ClientId = opts.ClientId,
        DeviceCodeCallback = (info, ct) =>
        {
            logger.LogInformation("{Message}", info.Message);
            return Task.CompletedTask;
        }
    });
    
    return new GraphServiceClient(credential, opts.Scopes);
});

builder.Services.AddSingleton<GraphCalendarService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();