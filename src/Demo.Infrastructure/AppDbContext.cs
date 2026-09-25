using Microsoft.EntityFrameworkCore;
using Demo.Domain;
using Demo.Application;

namespace Demo.Infrastructure;

/// <summary>
/// DbContext REAL, com Npgsql. É ele que os testes de integração usam:
/// tanto para preparar dados (Arrange) quanto para verificar persistência (Assert).
/// O lifetime é ceita pelo container de DI como Scoped — cada request/scope
/// recebe uma instância nova, o que evita estado compartilhado.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(e =>
        {
            e.ToTable("Customers");
            e.Property(c => c.Name).IsRequired().HasMaxLength(120);
            e.Property(c => c.Email).IsRequired().HasMaxLength(200);
            e.HasIndex(c => c.Email).IsUnique();
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("Products");
            e.Property(p => p.Name).IsRequired().HasMaxLength(120);
            e.Property(p => p.Price).HasPrecision(10, 2);
            // Regra de negócio #5: nome de produto ÚNICO. Além da validação no service,
            // o banco garante a unicidade (o teste de integração que insere duplicidade
            // direto no DB depende deste índice — teste de unidade com fake em memória não o verifica).
            e.HasIndex(p => p.Name).IsUnique();
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.ToTable("Orders");
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(o => o.CreatedAt).IsRequired();
            // Total é calculado e não vai para o banco.
            e.Ignore(o => o.Total);
            // Map para o backing field "_items".
            e.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.HasOne(o => o.Customer)
                .WithMany()
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasMany(o => o.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.ToTable("OrderItems");
            e.Property(i => i.Quantity).IsRequired();
            e.Property(i => i.UnitPrice).HasPrecision(10, 2);
            e.HasOne(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}