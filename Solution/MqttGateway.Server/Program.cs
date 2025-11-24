using Microsoft.Extensions.Internal;
using MqttGateway.Server.Hubs;
using MqttGateway.Server.Services;
using MqttGateway.Server.Services.Contracts;

namespace MqttGateway.Server;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddSignalR();

        // CORS liberar tudo
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy
                    .WithOrigins("http://localhost:3000")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        // outros services
        builder.Services.AddSingleton<ISessionContextStore, SessionContextStore>();
        builder.Services.AddSingleton<MqttBrokerConnectionHandler>();
        builder.Services.AddSingleton<ISystemClock, SystemClock>();
        builder.Services.AddSingleton<IMqttBrokerConnectionHandler>(services => services.GetRequiredService<MqttBrokerConnectionHandler>());
        builder.Services.AddSingleton<IMqttMessageDispatcher>(services => services.GetRequiredService<MqttBrokerConnectionHandler>());
        builder.Services.AddSingleton<ISessionManager, SessionManagerService>();
        builder.Services.AddSingleton<IMqttEventDispatcher, SignalRMessageRelay>(); // <- depois do AddSignalR()

        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        var app = builder.Build();

        app.UseRouting();

        // habilita CORS
        app.UseCors("AllowAll");

        var handler = app.Services.GetRequiredService<IMqttBrokerConnectionHandler>();
        var dispatcher = app.Services.GetRequiredService<IMqttEventDispatcher>();
        handler.SetDispatcher(dispatcher);

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<UserHub>("/hub");
        app.Run();
    }
}
