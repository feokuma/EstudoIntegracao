using Microsoft.EntityFrameworkCore;
using Demo.Domain;

namespace Demo.Application;

/// <summary>
/// Contrato do DbContext usado pela camada de aplicação.
/// Os testes de integração usam a implementação real (AppDbContext, com Npgsql + PostgreSQL).
/// Os testes de unidade usam um FAKE em memória (é justamente aí que bugs de persistência
/// passam despercebidos — o objetivo da demonstração).
/// </summary>
public interface IAppDbContext
{
    DbSet<Customer> Customers { get; }
    DbSet<Product> Products { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}