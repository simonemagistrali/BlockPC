using BlockPC.Shared;
using WatchdogService;

var builder = Host.CreateApplicationBuilder(args);

// Registra il servizio come Windows Service (RNF1: esecuzione come LocalSystem)
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = ServiceNames.Watchdog;
});

// Registra il worker service di monitoraggio
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
