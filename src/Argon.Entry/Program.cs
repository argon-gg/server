using Argon;
using Argon.Controllers;
using Argon.Extensions;
using Argon.Features.Env;
using Argon.Features.Jwt;
using Argon.Features.Logging;
using Argon.Features.MediaStorage;
using Argon.Features.Middlewares;
using Argon.Features.Web;
using Argon.Features.OrleansStreamingProviders;
using Argon.Services;
using Argon.Streaming;
using MessagePack;
using Newtonsoft.Json.Converters;
using Orleans.Clustering.Kubernetes;
using Orleans.Configuration;
using Orleans.Serialization;
using Microsoft.AspNetCore.Http.Features;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddLogging();
builder.UseMessagePack();
builder.AddSentry();
builder.Services.AddServerTiming();
builder.ConfigureDefaultKestrel();
builder.AddContentDeliveryNetwork();
builder.AddServiceDefaults();
builder.AddNatsStreaming();
builder.AddJwt();
builder.Services.AddControllers().AddApplicationPart(typeof(FilesController).Assembly)
   .AddNewtonsoftJson(x => x.SerializerSettings.Converters.Add(new StringEnumConverter()));
builder.AddDefaultCors();
builder.AddSwaggerWithAuthHeader();
builder.Services
   .AddSerializer(x => x.AddMessagePackSerializer(null, null, MessagePackSerializer.DefaultOptions))
   .AddOrleansClient(x =>
{
    x.Configure<ClusterOptions>(builder.Configuration.GetSection("Orleans"))
       .AddStreaming()
       .AddPersistentStreams("default", NatsAdapterFactory.Create, options => { })
       .AddPersistentStreams(IArgonEvent.ProviderId, NatsAdapterFactory.Create, options => { })
       .AddBroadcastChannel(IArgonEvent.Broadcast);
    if (builder.Environment.IsProduction())
        x.UseKubeGatewayListProvider();
    else
        x.UseLocalhostClustering();
});
builder.AddArgonTransport(x =>
{
    x.AddService<IServerInteraction, ServerInteraction>();
    x.AddService<IUserInteraction, UserInteraction>();
    x.AddService<IEventBus, EventBusService>();
});
builder.Services.AddAuthorization();
var app = builder.Build();
app.UseServerTiming();
app.UseCors();
app.UseWebSockets();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapDefaultEndpoints();
app.MapArgonTransport();

if (builder.Environment.IsKube())
    app.UseSerilogRequestLogging();

app.Map("/IEventBus/SubscribeToMeEvents.wt", x => {
    x.Use(async (context, func) => {
    #pragma warning disable CA2252
        var wt = context.Features.Get<IHttpWebTransportFeature>();

        if (wt is null)
            return;

        var session = await wt.AcceptAsync();
    #pragma warning restore CA2252

        await func(context);
    });
});


app.MapGet("/", () => new
{
    version = $"{GlobalVersion.FullSemVer}.{GlobalVersion.ShortSha}"
});

await app.RunAsync();