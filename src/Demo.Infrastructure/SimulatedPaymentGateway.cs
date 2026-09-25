using Demo.Application;
using Demo.Domain;

namespace Demo.Infrastructure;

/// <summary>
/// Implementação "real" do IPaymentGateway usada no runtime da API.
/// Não chama nenhum serviço externo de verdade — apenas simula uma cobrança aprovada.
///
/// O ponto didático é: em produção o corpo deste método faria um HTTP para um
/// PSP externo. Numa palestra isso não faz sentido, então simulamos.
/// Nos testes de integração, este tipo é o ÚNICO que pode ser trocado por um fake,
/// mantendo HTTP/ASP.NET/Application/Domain/EF Core/PostgreSQL 100% reais.
/// </summary>
public class SimulatedPaymentGateway : IPaymentGateway
{
    public Task<PaymentResult> ChargeAsync(Customer customer, decimal amount, CancellationToken cancellationToken = default)
    {
        // Simula aprovação: geraria um ID de transação retornado pelo PSP externo.
        return Task.FromResult(PaymentResult.Approved($"sim-{Guid.NewGuid():N}"));
    }
}