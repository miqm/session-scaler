using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace miqm.sbss;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Any, 8080, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1;
            });
            options.Listen(IPAddress.Any, 8998, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        });
        builder.Services.AddGrpc();
        builder.Services.AddSingleton<IServiceBusSessionCountProviderFactory, AmqpLite.ServiceBusSessionCountProviderFactory>();
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        });

        var app = builder.Build();

        app.MapGet("/", async _ => await Task.FromResult(Results.NoContent()));
        app.MapGrpcService<ServiceBusSessionService>();
        app.Run();
    }
}