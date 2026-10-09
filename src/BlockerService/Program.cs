using BlockerService;
using BlockPC.Shared;

var builder = Host.CreateApplicationBuilder(args);

// Registra il servizio come Windows Service (RNF1: esecuzione come LocalSystem)
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = ServiceNames.Blocker;
});

// Binding della configurazione dal file appsettings.json
builder.Services.Configure<BlockingPolicy>(
    builder.Configuration.GetSection("BlockingPolicy"));

// Registra il worker service principale
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<IpcServerWorker>();

var host = builder.Build();
host.Run();
