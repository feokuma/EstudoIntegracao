using Demo.Application;
using Demo.Domain;
using Microsoft.EntityFrameworkCore;

namespace Demo.UnitTests;

/// <summary>
/// FAKE do IAppDbContext usado SÓ nos testes de unidade.
///
/// ⚠️ CONTRASTE DIDÁTICO: baseado em EF Core InMemory (banco em memória).
/// Como nada é gravado "de verdade", um bug de persistência — como esquecer de
/// chamar SaveChangesAsync — passa despercebido aqui. Somente o teste de
/// INTEGRAÇÃO, que consulta o PostgreSQL real via Testcontainers, consegue
/// revelar esse problema.
///
/// NENHUM teste de integração usa este tipo.
/// </summary>
public class FakeAppDbContext : DbContext, IAppDbContext
{
    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OrderItem> OrderItems { get; set; } = null!;

    // DbSet<X> que o DbContext base não conhece por convenção de DbSet property
    // já é descoberto; usamos DbSet<T> simples (InMemory não exige FK/ideias de banco).

    public FakeAppDbContext(DbContextOptions<FakeAppDbContext> options) : base(options)
    {
    }

    /// <summary>True = a chamada a SaveChangesAsync foi registrada (mas não persiste nada).</summary>
    public bool SaveChangesCalled { get; private set; }

    /// <summary>Cria um fake já conectado a um banco em memória e com os dados básicos.</summary>
    public static FakeAppDbContext Create(
        IEnumerable<Customer>? customers = null,
        IEnumerable<Product>? products = null)
    {
        var options = new DbContextOptionsBuilder<FakeAppDbContext>()
            .UseInMemoryDatabase($"demo-unit-{Guid.NewGuid():N}")
            .Options;

        var db = new FakeAppDbContext(options);
        if (customers is not null) db.Customers.AddRange(customers);
        if (products is not null) db.Products.AddRange(products);
        db.SaveChanges();

        return db;
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCalled = true;
        // Em memória não há nada para "commitar" de fato.
        return Task.FromResult(0);
    }
}