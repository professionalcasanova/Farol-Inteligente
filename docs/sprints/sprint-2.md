# Sprint 2 - Core Financeiro Inicial

## Objetivo da sprint

Entregar o primeiro conjunto de modulos de negocio do Farol sobre a base criada na Sprint 1:

- contas financeiras
- categorias visiveis ao usuario
- transacoes
- resumo mensal

## Entregas concluidas

### Modulos entregues

- `Accounts`
- `Categories`
- `Transactions`
- `Dashboard`

### Endpoints implementados

Autenticacao ja existente e utilizada nesta sprint:

- `POST /api/auth/register`
- `POST /api/auth/login`

Contas financeiras:

- `POST /api/accounts`
- `GET /api/accounts`
- `PUT /api/accounts/{id}`

Categorias:

- `GET /api/categories`

Transacoes:

- `GET /api/transactions`
- `POST /api/transactions`
- `PUT /api/transactions/{id}`

Resumo mensal:

- `GET /api/dashboard/monthly-summary`

### Comportamento entregue

- todos os endpoints de negocio desta sprint estao protegidos com JWT
- `accounts` retornam apenas dados do usuario autenticado
- `categories` retornam categorias de sistema e futuras categorias do usuario autenticado
- `transactions` respeitam ownership por `userId`
- `transactions` exigem conta financeira do usuario autenticado
- `transactions` validam compatibilidade entre tipo da transacao e tipo da categoria
- `monthly-summary` considera apenas transacoes do usuario autenticado
- `monthly-summary` aceita filtro por `month` e `year`
- `monthly-summary` retorna `totalIncome`, `totalExpense`, `balance` e `byCategory`

## Decisoes tecnicas importantes

- mantido o monolito modular sem `Application` layer
- mantido uso direto de `FarolDbContext` nos controllers
- sem `CQRS`, sem repository generico e sem servicos adicionais para esta etapa
- helper simples `AuthenticatedUser` centraliza a leitura do `sub` do token
- testes de API usam `WebApplicationFactory` com provider `InMemory`
- ambiente de teste continua sem `HttpsRedirection` e sem seed automatico no startup

## Estrutura relevante da sprint

```txt
src/
  Farol.Api/
    Common/
      AuthenticatedUser.cs
    Modules/
      Accounts/
      Categories/
      Transactions/
      Dashboard/
tests/
  Farol.Tests/
    Api/
      AccountsEndpointsTests.cs
      CategoriesEndpointsTests.cs
      TransactionsEndpointsTests.cs
      MonthlySummaryEndpointsTests.cs
```

## Testes automatizados existentes nesta sprint

Cobertura adicionada ou validada para os fluxos da Sprint 2:

- `AccountsEndpointsTests`
- `CategoriesEndpointsTests`
- `TransactionsEndpointsTests`
- `MonthlySummaryEndpointsTests`

Casos cobertos:

- autenticacao obrigatoria para endpoints protegidos
- criacao e atualizacao de contas financeiras do usuario autenticado
- listagem de contas financeiras somente do usuario autenticado
- listagem de categorias de sistema e do usuario autenticado
- criacao de transacoes validas
- rejeicao de categoria incompativel com o tipo da transacao
- listagem de transacoes somente do usuario autenticado
- atualizacao de transacao do proprio usuario
- resumo mensal filtrado por mes/ano
- resumo mensal com agregacao por categoria
- resumo mensal isolado por usuario

Estado validado no fechamento desta sprint:

- `27` testes passando

## Como validar manualmente a Sprint 2

### 1. Subir banco e aplicar schema

```powershell
docker compose up -d
& "$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe" database update `
  --project src/Farol.Infrastructure/Farol.Infrastructure.csproj `
  --startup-project src/Farol.Api/Farol.Api.csproj `
  --context FarolDbContext `
  --no-build
```

### 2. Compilar e subir a API

```powershell
$env:DOTNET_CLI_HOME='c:\Users\masuc\Desktop\PensarNoNome\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
dotnet build Farol.sln --no-restore -c Release -m:1 -v minimal
dotnet run --project src/Farol.Api/Farol.Api.csproj -c Release --no-build
```

Swagger:

- `http://localhost:5258/swagger`

### 3. Validar fluxo principal

1. Registrar usuario em `POST /api/auth/register`
2. Fazer login em `POST /api/auth/login`
3. Autorizar o token no Swagger
4. Criar conta financeira em `POST /api/accounts`
5. Listar categorias em `GET /api/categories`
6. Criar receita e despesa em `POST /api/transactions`
7. Atualizar uma transacao em `PUT /api/transactions/{id}`
8. Consultar `GET /api/transactions`
9. Consultar `GET /api/dashboard/monthly-summary?month=3&year=2026`

### 4. Validacoes manuais minimas

- sem token, endpoints de negocio retornam `401`
- `GET /api/accounts` nao retorna contas de outro usuario
- `GET /api/transactions` nao retorna transacoes de outro usuario
- `POST /api/transactions` com categoria incompativel retorna `400`
- `monthly-summary` reflete apenas o mes/ano solicitado
- `monthly-summary` soma receitas e despesas corretamente

## O que ficou explicitamente fora da Sprint 2

- bills
- recorrencia
- alertas
- importacao CSV
- dashboard visual
- orcamento
- metas
- integracoes bancarias
- assistente em linguagem natural sobre gastos
- negociacao de boletos e dividas

## Proximos passos recomendados para Sprint 3

- evoluir `monthly-summary` para basear o assistente em dados estruturados
- adicionar importacao CSV de transacoes
- iniciar categorizacao automatica simples por regras
- preparar primeiros endpoints de insights para demo
- manter aumento de cobertura de testes conforme novos fluxos entrarem
