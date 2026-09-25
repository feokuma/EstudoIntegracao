using Demo.Application;
using Demo.Domain;

namespace Demo.IntegrationTests.Fakes;

/// <summary>
/// Fake do IPaymentGateway configurável por teste. Aprova ou recusa conforme
/// o modo escolhido e GRAVA cada chamada recebida (Calls) para verificação —
/// mostrando que, com um mock, podemos inspecionar como a dependência foi usada.
/// </summary>
public sealed class FakePaymentGateway(bool approved = true) : IPaymentGateway
{
    /// <summary>Chamadas recebidas: (Id do cliente, valor cobrado).</summary>
    public List<(int CustomerId, decimal Amount)> Calls { get; } = [];

    public bool Approved => approved;

    public Task<PaymentResult> ChargeAsync(Customer customer, decimal amount, CancellationToken cancellationToken = default)
    {
        Calls.Add((customer.Id, amount));
        return Task.FromResult(
            approved ? PaymentResult.Approved("fake-tx") : PaymentResult.Refused("saldo insuficiente"));
    }
}