# Demo — Testes de Integração com .NET 10 (Loja/Pedidos)

Aplicação de demonstração para uma palestra sobre **testes de integração com .NET 10**.
A arquitetura foi pensada para ser pequena e didática, mas com integrações reais:
HTTP → ASP.NET Core → Application → Domain → EF Core (Npgsql) → PostgreSQL real em container.

> O repositório usa o novo formato de solution `.slnx` (padrão do .NET 10) e o novo
> runner **Microsoft.Testing.Platform** (habilitado via `global.json`).
> Testcontainers exige **Docker** rodando.

---

## Pré-requisitos

- SDK **.NET 10**
- **Docker** (para o Testcontainers subir o PostgreSQL `postgres:17`)

---

## Rodar a API (PostgreSQL local via docker compose)

```bash
# 1) Sobe o banco PostgreSQL (imagem postgres:17, ver docker-compose.yml)
docker compose up -d
# 2) Aguarde ficar "healthy" (docker compose ps)

# 3) Aplica as migrations (cria as tabelas E insere dados de exemplo — basta rodar uma vez)
dotnet dotnet-ef database update --project src/Demo.Infrastructure --startup-project src/Demo.Api

# 4) Roda a aplicação (Scalar UI em /scalar/v1; a porta pode variar)
dotnet run --project src/Demo.Api
```

Para parar: `docker compose down` (dados ficam no volume) ou `docker compose down -v` (apaga dados).
As credenciais do compose coincidem com a `ConnectionStrings:Default` do `appsettings.json`.

**Dados de exemplo:** a migration `SeedData` popula o banco com 6 clientes, 10 produtos
e 6 pedidos com itens (status aprovado/recusado/pendente). O seed roda automaticamente no
`database update`, e após aplicá-lo a API já tem dados para demonstrar no GET `/api/orders/{id}`
e `/api/products`.

Os testes de integração **não** usam esse Postgres local — cada execução sobe o
próprio container isolado via Testcontainers, com porta efêmera (sem conflito).

## Rodar os testes

```bash
dotnet test --solution PalestraIntegracao.slnx
```

Primeira execução baixa a imagem `postgres:17` (fica em cache para as próximas).

---

## Estrutura

```
src/
  Demo.Domain/          Entidades puras + regra (Total calculado, status)
  Demo.Application/     OrderService + IAppDbContext + IPaymentGateway + DTOs
  Demo.Infrastructure/  AppDbContext (EF Core/Npgsql), SimulatedPaymentGateway, migrations
  Demo.Api/             Minimal APIs + Scalar UI
tests/
  Demo.UnitTests/       Testes de unidade (FAKE em memória) — contraste com integração
  Demo.IntegrationTests/ Testcontainers + WebApplicationFactory + PostgreSQL real
```

### Infraestrutura dos testes de integração

```
PostgreSqlContainer (postgres:17)
        ↓
IntegrationTestFixture (IAsyncLifetime — 1 container por suíte)
        ↓
CustomWebApplicationFactory : WebApplicationFactory<Program>
        ↓ HttpClient
```

- Connection string **dinâmica** (porta efêmera) sobrescreve a configuração da API.
- `Migrate()` aplica as migrações no banco real.
- `CreateScope()` cria um **scope novo** por teste; os asserts de banco usam
  `AsNoTracking()` para garantir que o dado vem do PostgreSQL e não do ChangeTracker.
- `ResetDatabaseAsync()` faz `TRUNCATE ... RESTART IDENTITY CASCADE` entre testes.

---

## Os testes e o que cada um demonstra

### Demo.IntegrationTests

| Teste | Cenário |
|---|---|
| `GetOrder_WhenOrderExists_...` | **Arrange no banco** → GET → valida conteúdo/total calculado |
| `GetOrder_WhenOrderDoesNotExist_...` | 404 vindo do pipeline real |
| `CreateOrder_WithValidRequest_...` | POST → **Assert direto no banco** (efeito real) |
| `CreateOrder_WhenProductDoesNotExist_...` | 400 (validação consultou o Postgres) + nada persistido |
| `CreateOrder_WhenPaymentGatewayApproves_...` | **mock da dependência externa** aprovando |
| `CreateOrder_WhenPaymentGatewayRefuses_...` | **mock** recusando → 402 + pedido persistido |

Em todos: HTTP, ASP.NET Core, Application, Domain, EF Core e PostgreSQL são **reais**;
o único componente substituível é `IPaymentGateway`.

### Demo.UnitTests

Testam `OrderService` com um **FAKE em memória** (EF Core InMemory). Funcionam mesmo
quando a persistência está quebrada — é o contraste que a palestra explora.

---

## Demonstração ao vivo: branch com bug "invisível" para testes de unidade

Há um bug **didático** que testes de unidade **não** capturam, mas o teste de
integração **sim**. O motivo: o teste de unidade usa um FAKE em memória e valida só o
valor de retorno; o teste de integração consulta o **PostgreSQL real**.

```bash
git checkout demo/integration-only-bug   # vê o teste quebrar
dotnet test --solution PalestraIntegracao.slnx
# → os 3 testes de unidade passam
# → os testes de integração que conferem PERSISTÊNCIA falham

# "correção" ao vivo: adicionar o SaveChangesAsync ausente no OrderService

dotnet test --solution PalestraIntegracao.slnx   # → tudo verde
git checkout main                                # main já contém a versão corrigida
```

Deixe o `SaveChangesAsync` presente em `main`; no branch de demonstração ele foi
removido (veja o trecho destacado em `src/Demo.Application/OrderService.cs`).

### Branch `demo/unit-vs-integration` — regras #5 e #6

Outro branch de demonstração com **dois bugs "invisíveis" a testes de unidade**,
relacionados às regras de negócio #5 (unicidade) e #6 (preço congelado):

```bash
git checkout demo/unit-vs-integration
dotnet test --solution PalestraIntegracao.slnx
# → 5 testes de unidade PASSAM
# → 2 testes de integração FALHAM, exatamente os que dependem do banco real:
#      - CatalogEndpointTests.ProductName_UniqueConstraint_EnforcedByDatabase
#      - GetOrderEndpointTests.GetOrder_WhenProductPriceChangesAfterPurchase_ShouldKeepOriginalUnitPrice
```

- **#5:** o índice único do produto foi "esquecido" na migration (constraint não existe no
  banco; só a checagem em memória do service). O fake dos testes de unidade não impõe
  constraint, então engana — só o PostgreSQL real detecta.
- **#6:** o total passou a usar o preço **atual** do produto (`Product.Price`) em vez do
  `UnitPrice` congelado na compra. Nos testes de unidade o preço nunca muda, então nada
  aparece; no teste de integração, alteramos o preço após a compra e o total muda.

Em `main` as duas regras estão corretas (índice único aplicado + `LineTotal` por `UnitPrice`).