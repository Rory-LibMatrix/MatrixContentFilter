using MatrixContentFilter;
using MatrixContentFilter.Handlers;
using LibMatrix.Services;
using LibMatrix.Utilities.Bot;
using MatrixContentFilter.Abstractions;
using MatrixContentFilter.Handlers.Filters;
using MatrixContentFilter.Services;
using MatrixContentFilter.Services.AsyncActionQueues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureHostOptions(host => {
    host.ServicesStartConcurrently = true;
    host.ServicesStopConcurrently = true;
    host.ShutdownTimeout = TimeSpan.FromSeconds(5);
});

if (Environment.GetEnvironmentVariable("MATRIXCONTENTFILTER_APPSETTINGS_PATH") is string path)
    builder.ConfigureAppConfiguration(x => x.AddJsonFile(path));

var host = builder.ConfigureServices((ctx, services) => {
    var config = new MatrixContentFilterConfiguration(ctx.Configuration);
    services.AddSingleton<MatrixContentFilterConfiguration>(config);

    services.AddRoryLibMatrixServices(new() {
        AppName = "MatrixContentFilter"
    });

    services.AddMatrixBot().AddCommandHandler().DiscoverAllCommands()
        .WithInviteHandler(InviteHandler.HandleAsync)
        .WithCommandResultHandler(CommandResultHandler.HandleAsync);

    services.AddSingleton<InfoCacheService>();

    services.AddSingleton<IContentFilter, ImageFilter>();

    services.AddSingleton<ConfigurationService>();
    services.AddSingleton<IHostedService, ConfigurationService>(s => s.GetRequiredService<ConfigurationService>());
    services.AddSingleton<AsyncMessageQueue>();
    services.AddSingleton<IHostedService, AsyncMessageQueue>(s => s.GetRequiredService<AsyncMessageQueue>());

    switch (config.AppMode) {
        case "bot":
            services.AddHostedService<MatrixContentFilterBot>();
            // services.AddHostedService<BotModeSanityCheckService>();
            break;
        default:
            throw new NotSupportedException($"Unknown app mode: {config.AppMode}");
    }
    
    switch (config.AsyncQueueImplementation) {
        case "lifo":
            services.AddSingleton<LiFoAsyncActionQueue>();
            services.AddSingleton<AbstractAsyncActionQueue, LiFoAsyncActionQueue>(s => s.GetRequiredService<LiFoAsyncActionQueue>());
            services.AddSingleton<IHostedService, LiFoAsyncActionQueue>(s => s.GetRequiredService<LiFoAsyncActionQueue>());
            break;
        case "fifo":
            services.AddSingleton<FiFoAsyncActionQueue>();
            services.AddSingleton<AbstractAsyncActionQueue, FiFoAsyncActionQueue>(s => s.GetRequiredService<FiFoAsyncActionQueue>());
            services.AddSingleton<IHostedService, FiFoAsyncActionQueue>(s => s.GetRequiredService<FiFoAsyncActionQueue>());
            break;
        default:
            throw new NotSupportedException($"Unknown async queue implementation: {config.AsyncQueueImplementation}");
    }
}).UseConsoleLifetime().Build();

await host.RunAsync();