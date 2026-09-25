using Demo.Domain;

namespace Demo.Application;

/// <summary>
/// Dependência EXTERNA mockável: cobrança em um gateway de pagamento.
/// A implementação "real" (SimulatedPaymentGateway) apenas simula a resposta,
/// pois nesta aplicação de estudo não chamamos nenhum serviço externo de verdade.
/// Nos testes, é o ÚNICO componente substituído por um fake.
/// </summary>
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(Customer customer, decimal amount, CancellationToken cancellationToken = default);
}

public record PaymentResult(bool IsApproved, string? TransactionId, string? RefusalReason)
{
    public static PaymentResult Approved(string transactionId) =>
        new(true, transactionId, null);

    public static PaymentResult Refused(string? reason) =>
        new(false, null, reason);
}