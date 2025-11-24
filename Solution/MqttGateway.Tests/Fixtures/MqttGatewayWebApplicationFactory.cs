using Microsoft.AspNetCore.Mvc.Testing;
using MqttGateway.Server;
using MqttGateway.Server.Services.Contracts;

namespace MqttGateway.Tests.Fixtures;

/// <summary>
/// Factory para criar instâncias de teste da aplicação com configurações customizadas
/// </summary>
public class MqttGatewayWebApplicationFactory : WebApplicationFactory<Program>
{
    public Mock<IMqttBrokerConnectionHandler>? MockMqttBrokerConnectionHandler { get; private set; }
    public Mock<ISessionContextStore>? MockSessionContextStore { get; private set; }
    public Mock<ISessionManager>? MockSessionManager { get; private set; }

    public bool UseMockServices { get; set; } = true;
    public Action<IServiceCollection>? ConfigureTestServices { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Usar configuração de teste
            config.AddJsonFile("appsettings.Test.json", optional: false);
        });

        builder.ConfigureServices(services =>
        {
            if (UseMockServices)
            {
                // Remover serviços reais e adicionar mocks
                RemoveService<IMqttBrokerConnectionHandler>(services);
                RemoveService<ISessionContextStore>(services);
                RemoveService<ISessionManager>(services);

                // Criar e configurar mocks
                MockMqttBrokerConnectionHandler = new Mock<IMqttBrokerConnectionHandler>();
                MockSessionContextStore = new Mock<ISessionContextStore>();
                MockSessionManager = new Mock<ISessionManager>();

                // Adicionar mocks ao container
                services.AddSingleton(MockMqttBrokerConnectionHandler.Object);
                services.AddSingleton(MockSessionContextStore.Object);
                services.AddSingleton(MockSessionManager.Object);
            }

            // Permitir configuração adicional de serviços
            ConfigureTestServices?.Invoke(services);
        });

        builder.UseEnvironment("Test");

        // Suprimir logs desnecessários nos testes
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Warning);
        });
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        var serviceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(T));
        if (serviceDescriptor != null)
        {
            services.Remove(serviceDescriptor);
        }
    }

    /// <summary>
    /// Cria uma factory com configuração específica
    /// </summary>
    public static MqttGatewayWebApplicationFactory Create(
        bool useMockServices = true,
        Action<IServiceCollection>? configureServices = null)
    {
        return new MqttGatewayWebApplicationFactory
        {
            UseMockServices = useMockServices,
            ConfigureTestServices = configureServices
        };
    }
}