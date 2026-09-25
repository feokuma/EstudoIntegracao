using Demo.Application;
using Demo.Domain;
using Microsoft.EntityFrameworkCore;

namespace Demo.UnitTests;

/// <summary>
/// Fake do IAppDbContext usado SÓ nos testes de unidade, baseado em EF Core InMemory.
/// Como nada é gravado "de verdade", um bug de persistência — como esquecer de chamar
/// SaveChangesAsync — passa despercebido aqui. Somente o teste de INTEGRAÇÃO, que
/// consulta o PostgreSQL real via Testcontainers, revela esse problema.
/// </summary>
public class FakeAppDbContext : DbContext, IAppDbContext
{
    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OrderItem> OrderItems { get; set; } = null!;

    public FakeAppDbContext(DbContextOptions<FakeAppDbContext> options) : base(options)
    {
    }

    /// <summary>True = a chamada a SaveChangesAsync foi registrada (mas não persiste nada).</summary>
    public bool SaveChangesCalled { get; private set; }

    /// <summary>Cria um fake já conectado a um banco em memória e com os dados básicos.
    /// Um `databaseName` compartilhado permite simular duas instalações do contexto
    /// apontando para o MESMO banco (usado pela suíte de contrato).</summary>
    public static FakeAppDbContext Create(
        IEnumerable<Customer>? customers = null,
        IEnumerable<Product>? products = null,
        string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<FakeAppDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"demo-unit-{Guid.NewGuid():N}")
            .Options;

        var db = new FakeAppDbContext(options);
        if (customers is not null) db.Customers.AddRange(customers);
        if (products is not null) db.Products.AddRange(products);
        db.SaveChanges();

        return db;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCalled = true;
        // Persiste no banco em memória, mas SEM impor índices únicos — a duplicidade
        // só é barrada pela checagem no service. Esse limite é o que o teste de
        // integração (PostgreSQL real) demonstra.
        return await base.SaveChangesAsync(cancellationToken);
    }
}