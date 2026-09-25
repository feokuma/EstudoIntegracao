using Demo.Application;
using Demo.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Demo.IntegrationTests.ContractTests;

/// <summary>
/// =====================================================================
///  CONTRATO: Application ⇄ Persistência (IAppDbContext)
/// =====================================================================
///
/// Esta suíte é ESCRITA UMA VEZ e executada CONTRA CADA IMPLEMENTAÇÃO de
/// IAppDbContext. Ela define o que a Application PRESUME da persistência:
///
///   C1. O que é salvo numa instância de contexto é visível numa OUTRA
///       (persistência real, não "estado em memória do próprio objeto").
///   C2. SaveChangesAsync não é opcional: sem ele, nada persiste.
///   C3. E-mail de cliente é ÚNICO — garantido pela PERSISTÊNCIA
///       (índice único), não só pela checagem `AnyAsync` do service.
///   C4. Nome de produto é ÚNICO — mesmo contrato (bug didático do
///       branch demo/unit-vs-integration).
///   C5. O esquema impõe NOT NULL para e-mail (contrato de schema).
///
/// Implementações testadas:
///   • PostgresAppDbContextContractTests → AppDbContext + PostgreSQL real
///   • FakeAppDbContextContractTests     → FakeAppDbContext (InMemory)
///
/// O contraste entre as duas execuções É a aula: onde o fake consegue
/// honrar o contrato e onde ele é genuinamente incapaz fica EXPLÍCITO
/// na saída (SKIPPED com motivo, nunca um falso-verde).
/// </summary>
public abstract class AppDbContextContractTests
{
    protected abstract Task<PersistContext> CreateContextAsync();

    /// <summary>Wrapper com Dispose assíncrono uniforme para os testes filhos.</summary>
    public sealed class PersistContext(IAppDbContext inner) : IAsyncDisposable
    {
        public IAppDbContext Value { get; } = inner;

        public async ValueTask DisposeAsync()
        {
            switch (inner)
            {
                case DbContext db:
                    await db.DisposeAsync();
                    break;
                case IAsyncDisposable d:
                    await d.DisposeAsync();
                    break;
            }
        }
    }

    // ---------------------------------------------------------------
    // C1: o que é salvo é VISÍVEL A OUTRA INSTÂNCIA de contexto.
    // ---------------------------------------------------------------
    [ContractFact]
    public async Task Customer_PersistedByOneContext_IsVisibleToAnotherContext()
    {
        var email = $"c1-{Guid.NewGuid():N}@exemplo.com";
        int id;

        await using (var wrap = await CreateContextAsync())
        {
            var db = wrap.Value;
            var customer = new Customer { Name = "Ana", Email = email };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            id = customer.Id;
        } // contexto DESCARTADO — elimina o ChangeTracker do caminho.

        await using (var wrap = await CreateContextAsync())
        {
            var db = wrap.Value; // instância NOVA
            var loaded = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            loaded.Should().NotBeNull("o dado deve vir da PERSISTÊNCIA, não da memória do objeto");
            loaded!.Email.Should().Be(email);
        }
    }

    // ---------------------------------------------------------------
    // C2: SaveChangesAsync é OBRIGATÓRIO. Sem ele, nada persiste.
    // (discriminador do branch demo/integration-only-bug)
    // ---------------------------------------------------------------
    [ContractFact]
    public async Task Order_WithoutSaveChanges_IsNotPersisted()
    {
        await ResetDataAsync();

        var customerId = await SeedCustomerAsync("Sem-Save", $"c2-{Guid.NewGuid():N}@exemplo.com");
        var productId = await SeedProductAsync("Sem-Save", 9m);

        await using (var wrap = await CreateContextAsync())
        {
            var db = wrap.Value;
            var order = new Order
            {
                CustomerId = customerId,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            };
            order.AddItem(new Product { Id = productId, Price = 9m }, 1);
            db.Orders.Add(order);
            // NÃO chamamos SaveChangesAsync de propósito.
        }

        await using (var wrap = await CreateContextAsync())
        {
            var db = wrap.Value;
            (await db.Orders.CountAsync(o => o.CustomerId == customerId))
                .Should().Be(0, "sem SaveChangesAsync o pedido NÃO pode persistir");
        }
    }

    // ---------------------------------------------------------------
    // C3: e-mail é único — IMPOSTO PELA PERSISTÊNCIA, não pela aplicação.
    // (a checagem AnyAsync do service é conveniente; sob concorrência,
    //  só o índice único garante)
    // ---------------------------------------------------------------
    [ContractFact]
    public virtual async Task Customer_EmailUniqueIndex_IsEnforcedByPersistence()
    {
        await ResetDataAsync();

        var email = $"c3-{Guid.NewGuid():N}@exemplo.com";

        await using (var wrap = await CreateContextAsync())
        {
            var db = wrap.Value;
            db.Customers.Add(new Customer { Name = "Primeiro", Email = email });
            await db.SaveChangesAsync();
        }

        await using (var wrap = await CreateContextAsync())
        {
            var db = wrap.Value;
            db.Customers.Add(new Customer { Name = "Duplicado", Email = email });
            var act = async () => await db.SaveChangesAsync();
            await act.Should().ThrowAsync<Exception>(
                "a persistência DEVE rejeitar o e-mail duplicado (DbUpdateException no real)");
        }
    }

    // ---------------------------------------------------------------
    // C4: nome de produto é único — mesmo contrato de C3.
    // (bug didático: migration AddProductNameUniqueIndex omitida)
    // ---------------------------------------------------------------
    [ContractFact]
    public virtual async Task Product_NameUniqueIndex_IsEnforcedByPersistence()
    {
        await ResetDataAsync();

        var name = $"Produto-{Guid.NewGuid():N}";

        await using (var wrap = await CreateContextAsync())
        {
            var db = wrap.Value;
            db.Products.Add(new Product { Name = name, Price = 1m });
            await db.SaveChangesAsync();
        }

        await using (var wrap = await CreateContextAsync())
        {
            var db = wrap.Value;
            db.Products.Add(new Product { Name = name, Price = 2m });
            var act = async () => await db.SaveChangesAsync();
            await act.Should().ThrowAsync<Exception>(
                "a persistência DEVE rejeitar o nome duplicado (DbUpdateException no real)");
        }
    }

    // ---------------------------------------------------------------
    // C5: schema — o e-mail é NOT NULL na persistência.
    // ---------------------------------------------------------------
    [ContractFact]
    public virtual async Task Customer_SavingWithoutEmail_IsRejectedByPersistence()
    {
        await ResetDataAsync();

        await using (var wrap = await CreateContextAsync())
        {
            var db = wrap.Value;
            db.Customers.Add(new Customer { Name = "SemEmail", Email = null! });
            var act = async () => await db.SaveChangesAsync();
            await act.Should().ThrowAsync<Exception>(
                "a persistência DEVE rejeitar cliente sem e-mail (coluna NOT NULL)");
        }
    }

    // ---- helpers compartilhados ---------------------------------
    protected abstract Task ResetDataAsync();

    protected async Task<int> SeedCustomerAsync(string name, string email)
    {
        await using var wrap = await CreateContextAsync();
        var db = wrap.Value;
        var c = new Customer { Name = name, Email = email };
        db.Customers.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    }

    protected async Task<int> SeedProductAsync(string name, decimal price)
    {
        await using var wrap = await CreateContextAsync();
        var db = wrap.Value;
        var p = new Product { Name = $"{name}-{Guid.NewGuid():N}", Price = price };
        db.Products.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }
}
