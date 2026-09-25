using System.Net.Http.Json;
using Demo.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Demo.IntegrationTests;

/// <summary>
/// Baseado em WebApplicationFactory&lt;Program&gt;. É a ponte entre o container
/// PostgreSQL real e a aplicação que damos como alvo:
///
///   1. Sobrescreve a connection string da configuração com a do container;
///   2. (opcional) substitui APENAS o IPaymentGateway por um fake.
///
/// Nesta aplicação de estudo: mostra que HTTP → ASP.NET Core → Application → Domain →
/// EF Core → PostgreSQL continuam 100% REAIS; só a dependência externa é trocada.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly IPaymentGateway? _paymentGateway;

    public CustomWebApplicationFactory(string connectionString, IPaymentGateway? paymentGateway = null)
    {
        _connectionString = connectionString;
        _paymentGateway = paymentGateway;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // (1) Conexão dinâmica com o container (porta efêmera, sem depender de porta fixa).
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _connectionString,
            }));

        // (2) Única substituição de dependência (opcional).
        if (_paymentGateway is not null)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPaymentGateway>();
                // Singleton para o teste poder inspecionar Calls depois.
                services.AddSingleton(_paymentGateway);
            });
        }
    }
}