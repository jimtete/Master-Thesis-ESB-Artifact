using OlympusServiceBus.Engine;

var builder = Host.CreateApplicationBuilder(args);
// builder.Services.AddHostedService<Worker>();
builder.Services.AddHttpClient();
builder.Services.AddHostedService<ApiToApiWorker>();

var host = builder.Build();
host.Run();