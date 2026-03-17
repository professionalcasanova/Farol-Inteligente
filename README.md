# Farol

Farol e um assistente financeiro pessoal para o mercado brasileiro, construido com foco em simplicidade, clareza e evolucao incremental do backend.

## Estado atual

Sprint 1, Sprint 2, Sprint 3, Sprint 4, Sprint 5 e Sprint 6 concluidas com:

- estrutura base da solution em .NET
- dominio inicial
- persistencia com EF Core e PostgreSQL
- migration inicial aplicada
- seed de categorias de sistema
- autenticacao minima com JWT
- endpoints de contas financeiras
- endpoints de categorias e transacoes
- endpoint de resumo mensal
- endpoints de orcamento mensal por categoria
- endpoint de importacao CSV de transacoes com categorizacao simples por regras
- endpoint de insight de dinheiro livre
- front-end MVP em `web/` com Next.js, TypeScript e Tailwind CSS
- login web com persistencia temporaria de `accessToken` em `localStorage` para demo local
- dashboard web com resumo mensal, orcamento e dinheiro livre
- telas web de transacoes, orcamento e importacao CSV consumindo a API existente
- testes unitarios e testes de API para autenticacao, contas, categorias, transacoes, resumo mensal, orcamento, importacao CSV e insights

O projeto ainda nao possui bills.

## Stack atual

- .NET 10
- ASP.NET Core Web API
- Next.js
- TypeScript
- Tailwind CSS
- EF Core
- PostgreSQL
- xUnit
- Docker Compose

## Estrutura

```txt
Farol.sln
src/
  Farol.Api/
  Farol.Domain/
  Farol.Infrastructure/
web/
tests/
  Farol.Tests/
docs/
  sprints/
```

## Rodando localmente

### 1. Subir o banco

```powershell
docker compose up -d
```

### 2. Restaurar e compilar

```powershell
$env:DOTNET_CLI_HOME='c:\Users\masuc\Desktop\PensarNoNome\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
dotnet restore Farol.sln
dotnet build Farol.sln --no-restore -c Release -m:1 -v minimal
```

### 3. Aplicar migration

```powershell
& "$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe" database update `
  --project src/Farol.Infrastructure/Farol.Infrastructure.csproj `
  --startup-project src/Farol.Api/Farol.Api.csproj `
  --context FarolDbContext `
  --no-build
```

### 4. Subir a API

```powershell
dotnet run --project src/Farol.Api/Farol.Api.csproj -c Release --no-build
```

Swagger:

- `http://localhost:5258/swagger`

### 5. Rodar testes

```powershell
dotnet test tests/Farol.Tests/Farol.Tests.csproj --no-build -c Release -m:1 -v minimal
```

### 6. Rodar o front-end MVP

Crie `web/.env.local` com:

```env
NEXT_PUBLIC_API_BASE_URL=http://localhost:5258
```

Depois, em outro terminal:

```powershell
Set-Location web
npm install
npm run dev
```

Aplicacao web:

- `http://localhost:3000`

Observacao:

- o `localStorage` e usado apenas como decisao temporaria de MVP para demo local
- nao existe refresh token nesta etapa

## Autenticacao atual

Endpoints disponiveis:

- `POST /api/auth/register`
- `POST /api/auth/login`

Resposta atual:

- `accessToken`
- `userId`
- `name`
- `email`

## Contas financeiras

Endpoints disponiveis:

- `POST /api/accounts`
- `GET /api/accounts`
- `PUT /api/accounts/{id}`

Regras atuais:

- endpoints protegidos com JWT
- cada conta pertence a um unico usuario
- a listagem retorna apenas contas do usuario autenticado
- atualizacao respeita ownership por `userId`
- validacoes do dominio continuam centralizadas em `FinancialAccount`

## Categorias e transacoes

Endpoints disponiveis:

- `GET /api/categories`
- `GET /api/transactions`
- `POST /api/transactions`
- `PUT /api/transactions/{id}`

Regras atuais:

- categorias listam categorias de sistema e categorias do usuario autenticado
- transacoes respeitam ownership por `userId`
- transacoes exigem conta financeira do usuario autenticado
- categoria, quando informada, deve ser visivel ao usuario e compativel com o tipo da transacao

## Resumo mensal

Endpoint disponivel:

- `GET /api/dashboard/monthly-summary`

Parametros:

- `month`
- `year`

Retorno atual:

- `totalIncome`
- `totalExpense`
- `balance`
- `byCategory`

## Orcamento mensal

Endpoints disponiveis:

- `POST /api/budgets/monthly`
- `GET /api/budgets/monthly?month={m}&year={y}`

Retorno atual:

- `totalPlanned`
- `totalSpent`
- `totalRemaining`
- `categories`

## Importacao CSV

Endpoint disponivel:

- `POST /api/imports/transactions/csv`

Fluxo atual:

- endpoint protegido com JWT
- aceita arquivo CSV simples com colunas `occurredOn,description,amount,type,categoryName`
- importa transacoes para uma conta financeira do usuario autenticado
- usa `categoryName` quando valido
- tenta categorizacao simples por regras quando `categoryName` vier vazio
- linhas invalidas sao ignoradas com erro registrado no resumo

## Insights

Endpoint disponivel:

- `GET /api/insights/free-money?month={m}&year={y}`

Retorno atual:

- `totalIncome`
- `totalExpense`
- `balance`
- `totalPlannedBudget`
- `totalBudgetSpent`
- `totalBudgetRemaining`
- `freeToSpend`

## Banco local

`docker-compose.yml` sobe um PostgreSQL local com:

- database: `farol_dev`
- user: `postgres`
- password: `postgres`
- porta: `5432`

## Documentacao

Fechamento detalhado da Sprint 1:

- [docs/sprints/sprint-1.md](docs/sprints/sprint-1.md)

Fechamento detalhado da Sprint 2:

- [docs/sprints/sprint-2.md](docs/sprints/sprint-2.md)

Fechamento detalhado da Sprint 3:

- [docs/sprints/sprint-3.md](docs/sprints/sprint-3.md)

Fechamento detalhado da Sprint 4:

- [docs/sprints/sprint-4.md](docs/sprints/sprint-4.md)

Fechamento detalhado da Sprint 5:

- [docs/sprints/sprint-5.md](docs/sprints/sprint-5.md)

Fechamento detalhado da Sprint 6:

- [docs/sprints/sprint-6.md](docs/sprints/sprint-6.md)
