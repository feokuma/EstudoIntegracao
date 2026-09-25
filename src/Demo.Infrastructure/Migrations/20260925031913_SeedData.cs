using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------- Clientes ----------
            migrationBuilder.Sql(
                """
                INSERT INTO "Customers" ("Id", "Name", "Email") VALUES
                (1, 'Ana Souza',     'ana@exemplo.com'),
                (2, 'Bruno Lima',    'bruno@exemplo.com'),
                (3, 'Carla Mendes',  'carla@exemplo.com'),
                (4, 'Diego Alves',   'diego@exemplo.com'),
                (5, 'Elisa Castro',  'elisa@exemplo.com'),
                (6, 'Felipe Rocha',  'felipe@exemplo.com');
                """);

            // ---------- Produtos ----------
            migrationBuilder.Sql(
                """
                INSERT INTO "Products" ("Id", "Name", "Price") VALUES
                (1,  'Caneta BIC',              '3.50'),
                (2,  'Caderno 96 folhas',       '12.90'),
                (3,  'Lápis Grafite',           '1.80'),
                (4,  'Borracha Branca',         '2.20'),
                (5,  'Régua 30cm',              '4.75'),
                (6,  'Marcador Texto Amarelo',  '6.99'),
                (7,  'Papel Sulfite A4 (500)',  '22.50'),
                (8,  'Pasta Arquivo',           '8.40'),
                (9,  'Clips Caixa 100 un',      '3.10'),
                (10, 'Cola Bastão 40g',         '5.30');
                """);

            // ---------- Pedidos (Status é armazenado como string, conforme configuração) ----------
            migrationBuilder.Sql(
                """
                INSERT INTO "Orders" ("Id", "CustomerId", "Status", "CreatedAt") VALUES
                (1, 1, 'PaymentApproved', '2026-09-20T10:00:00Z'),
                (2, 2, 'PaymentRefused',  '2026-09-21T11:30:00Z'),
                (3, 3, 'PaymentApproved', '2026-09-22T09:15:00Z'),
                (4, 1, 'Pending',         '2026-09-23T14:45:00Z'),
                (5, 4, 'PaymentApproved', '2026-09-24T16:00:00Z'),
                (6, 5, 'PaymentRefused',  '2026-09-24T18:20:00Z');
                """);

            // ---------- Itens dos pedidos (UnitPrice é uma cópia do preço na compra) ----------
            migrationBuilder.Sql(
                """
                INSERT INTO "OrderItems" ("Id", "OrderId", "ProductId", "Quantity", "UnitPrice") VALUES
                (1,  1, 1,  2, '3.50'),
                (2,  1, 2,  1, '12.90'),
                (3,  2, 3,  5, '1.80'),
                (4,  3, 7,  1, '22.50'),
                (5,  3, 10, 1, '5.30'),
                (6,  3, 9,  2, '3.10'),
                (7,  4, 4,  3, '2.20'),
                (8,  4, 5,  1, '4.75'),
                (9,  5, 8,  2, '8.40'),
                (10, 5, 6,  1, '6.99'),
                (11, 6, 6,  2, '6.99');
                """);

            // Ajusta as sequências de identidade para o próximo valor vir depois dos seeds,
            // evitando conflito de Id quando a aplicação criar novos registros via POST.
            migrationBuilder.Sql("""SELECT setval(pg_get_serial_sequence('"Customers"', 'Id'), (SELECT MAX("Id") FROM "Customers"));""");
            migrationBuilder.Sql("""SELECT setval(pg_get_serial_sequence('"Products"', 'Id'), (SELECT MAX("Id") FROM "Products"));""");
            migrationBuilder.Sql("""SELECT setval(pg_get_serial_sequence('"Orders"', 'Id'), (SELECT MAX("Id") FROM "Orders"));""");
            migrationBuilder.Sql("""SELECT setval(pg_get_serial_sequence('"OrderItems"', 'Id'), (SELECT MAX("Id") FROM "OrderItems"));""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove os dados semeados (na ordem contrária das FKs).
            migrationBuilder.Sql("""DELETE FROM "OrderItems";""");
            migrationBuilder.Sql("""DELETE FROM "Orders";""");
            migrationBuilder.Sql("""DELETE FROM "Products";""");
            migrationBuilder.Sql("""DELETE FROM "Customers";""");
        }
    }
}
