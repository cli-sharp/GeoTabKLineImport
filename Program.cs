using GeoTabKLineImport;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<VehicleCache>();
builder.Services.AddHostedService<Worker>();

await builder.Build().RunAsync();