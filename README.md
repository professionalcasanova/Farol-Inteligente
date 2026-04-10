# Farol

Farol e um assistente financeiro pessoal para o mercado brasileiro, construido com foco em simplicidade, clareza e evolucao incremental do backend.

## Estado atual

Sprint 1 a Sprint 10 concluidas, com MVP fechado para demonstracao local:

- estrutura base da solution em .NET
- dominio inicial
- persistencia com EF Core e PostgreSQL
- migrations aplicadas para a persistencia atual
- seed de categorias de sistema
- autenticacao minima com JWT
- endpoints de contas financeiras
- endpoints de categorias e transacoes
- endpoint de resumo mensal
- endpoints de orcamento mensal por categoria
- endpoint de importacao CSV de transacoes com categorizacao simples por regras
- endpoint de insight de dinheiro livre
- endpoint de alertas financeiros
- modulo minimo de bills/vencimentos no backend e no front MVP
- endpoint de resumo de bills no dashboard
- front-end MVP em `web/` com Next.js, TypeScript e Tailwind CSS
- login web com persistencia temporaria de `accessToken` em `localStorage` para demo local
- dashboard web com resumo mensal, bills, orcamento, dinheiro livre, alertas e onboarding
- alertas web acionaveis com redirecionamento para a proxima acao
- telas web de transacoes, orcamento, importacao CSV e bills consumindo a API existente
- fluxo de onboarding simples para primeiro valor do usuario
- polimento de microcopy, CTAs e estados vazios para demonstracao
- testes unitarios e testes de API para autenticacao, contas, categorias, transacoes, resumo mensal, orcamento, importacao CSV, insights e bills

O MVP esta fechado para demonstracao local.

## Deploy beta fechado

Para publicar o Farol para testers com o menor atrito operacional hoje:

- `web` no Vercel
- `api`, `farol-intelligence` e PostgreSQL no Render

Tutorial passo a passo:

- [docs/deployment/vercel-render-beta.md](docs/deployment/vercel-render-beta.md)

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

### 3. Aplicar migrations

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

Health:

- `http://localhost:5258/health`

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

### 7. Rodar o servico de inteligencia

```powershell
Set-Location services/farol_intelligence
python -m pip install -e .
python -m uvicorn app.main:app --reload --port 8000
```

Health:

- `http://127.0.0.1:8000/health`

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

## Bills no dashboard

Endpoint disponivel:

- `GET /api/dashboard/bills-summary?month={m}&year={y}`

Retorno atual:

- `totalPending`
- `totalOverdue`
- `totalPaid`
- `countPending`
- `countOverdue`
- `countPaid`
- `upcoming`

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

## Bills

Endpoints disponiveis:

- `POST /api/bills`
- `GET /api/bills`
- `PATCH /api/bills/{id}/pay`
- `PATCH /api/bills/{id}/unpay`

Fluxo atual:

- bills pertencem ao usuario autenticado
- criacao com descricao, valor e vencimento
- listagem com filtros simples por `month`, `year` e `status`
- marcacao e desmarcacao de pagamento
- pagina web `/bills` para demonstracao do fluxo

## Insights

Endpoints disponiveis:

- `GET /api/insights/free-money?month={m}&year={y}`
- `GET /api/insights/alerts?month={m}&year={y}`

Retorno atual de `free-money`:

- `totalIncome`
- `totalExpense`
- `balance`
- `totalPlannedBudget`
- `totalBudgetSpent`
- `totalBudgetRemaining`
- `freeToSpend`

Retorno atual de `alerts`:

- `alerts`
- `type`
- `severity`
- `message`
- `amount`
- `actionUrl`

## Analise critica de saude financeira

Endpoint disponivel:

- `GET /api/insights/month-health?month={m}&year={y}`

Contrarato e decisoes de criticidade:

- Motor determinístico em Python (`services/farol_intelligence/app/analysis.py`)
- Status: `healthy`, `attention`, `critical`
- Critical é acionado quando:
  - `maxOverdueDays >= 7` (dias em atraso >= 7)
  - OU `free_to_spend < 0` E `variable_expense_ratio > 0.8` (saldo negativo + despesas variáveis > 80% da renda)
- Retorno incluindo:
  - `status`: saúde do mês
  - `score`: 0-100, penalidades acumuladas
  - `message`: resumo principal (fonte de verdade para exibição no dashboard)
  - `reasons`: array de razões explícitas (ex: "Conta de energia vencida há 8 dias")
  - `actions`: array de ações recomendadas (ex: "Priorize o pagamento das contas fixas vencidas")
  - `priority`: nível de urgência (ex: 110 para crítico)
  - `summary`: causa e ação adicionais
  - `insights`: detalhes completos por tipo de pressão
  - `recommendedActions`: navegação web para próximo passo do usuário

Dashboard:

- Bloco crítico proeminente exibe quando `status === "critical"`
- Título: "Risco financeiro crítico"
- Mostra mensagem + até 2 razões + até 2 ações
- CTA padrão: "Ver contas a pagar"
- Fallback: se `reasons/actions` ausentes, usa `cause`/`action` de insights
- Comportamento não-crítico preservado

Testes:

- Cenários críticos cobertos (overdue, negative+variável)
- Fallback validado
- Backend mapeia resposta sem duplicar lógica
- Frontend renderiza sem reimplementar regras

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

Fechamento detalhado da Sprint 7:

- [docs/sprints/sprint-7.md](docs/sprints/sprint-7.md)

Fechamento detalhado da Sprint 10:

- [docs/sprints/sprint-10.md](docs/sprints/sprint-10.md)
