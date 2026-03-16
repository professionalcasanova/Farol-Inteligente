# Sprint 3 - Orcamento Mensal por Categoria

## Objetivo da sprint

Entregar a base inicial de planejamento financeiro mensal do Farol, permitindo que o usuario:

- defina quanto pretende gastar por categoria em um mes
- consulte o orcamento consolidado do mes
- compare planejado, gasto e restante por categoria

## Entregas concluidas

### Modulo entregue

- `Budgets`

### Fluxo entregue

- criacao ou substituicao do orcamento mensal do usuario para um `month/year`
- consulta consolidada do orcamento mensal do usuario autenticado
- calculo de gasto realizado a partir das transacoes de despesa ja existentes

## Modelagem implementada

Entidades novas:

- `MonthlyBudget`
- `MonthlyBudgetCategory`

Regras refletidas na modelagem:

- o orcamento pertence a um usuario
- o orcamento e referente a um `month/year`
- o orcamento contem categorias e valores planejados
- nao existe duplicidade de orcamento para o mesmo `userId + month + year`
- nao existe duplicidade de categoria dentro do mesmo orcamento
- itens de orcamento aceitam apenas categorias compativeis com despesa

Persistencia criada:

- tabela `monthly_budgets`
- tabela `monthly_budget_categories`

## Endpoints implementados

- `POST /api/budgets/monthly`
- `GET /api/budgets/monthly?month={m}&year={y}`

Resposta minima entregue no `GET`:

- `month`
- `year`
- `totalPlanned`
- `totalSpent`
- `totalRemaining`
- `categories`
  - `categoryId`
  - `categoryName`
  - `planned`
  - `spent`
  - `remaining`

## Decisoes tecnicas importantes

- mantido o monolito modular atual
- mantido uso direto de `FarolDbContext` nos controllers
- sem `Application` layer, sem `CQRS` e sem repository generico
- sem navegacoes novas no dominio
- gasto realizado calculado dinamicamente a partir das transacoes de despesa do mes
- categorias fora do orcamento nao aparecem no retorno desta sprint
- substituicao do orcamento do mesmo mes/ano implementada com remocao do orcamento anterior e criacao do novo

## Migration criada

Migration criada nesta sprint:

- `AddMonthlyBudgets`

Arquivos gerados:

- `20260316172352_AddMonthlyBudgets.cs`
- `20260316172352_AddMonthlyBudgets.Designer.cs`
- `FarolDbContextModelSnapshot.cs`

## Testes automatizados existentes nesta sprint

Cobertura adicionada:

- `BudgetsEndpointsTests`

Casos cobertos:

- autenticacao obrigatoria no `POST /api/budgets/monthly`
- autenticacao obrigatoria no `GET /api/budgets/monthly`
- criacao de orcamento mensal para o usuario autenticado
- substituicao do orcamento do mesmo mes/ano
- rejeicao de categoria duplicada no mesmo payload
- rejeicao de categoria de receita no orcamento
- retorno correto de `planned`, `spent` e `remaining`
- isolamento por usuario autenticado

Estado validado no fechamento desta sprint:

- `35` testes passando na suite atual

## Como validar manualmente a Sprint 3

### 1. Subir banco, compilar e aplicar migrations

```powershell
docker compose up -d
$env:DOTNET_CLI_HOME='c:\Users\masuc\Desktop\PensarNoNome\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
dotnet build Farol.sln --no-restore -c Release -m:1 -v minimal
& "$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe" database update `
  --project src/Farol.Infrastructure/Farol.Infrastructure.csproj `
  --startup-project src/Farol.Api/Farol.Api.csproj `
  --context FarolDbContext `
  --configuration Release `
  --no-build
```

### 2. Subir a API

```powershell
dotnet run --project src/Farol.Api/Farol.Api.csproj -c Release --no-build
```

Swagger:

- `http://localhost:5258/swagger`

### 3. Validar fluxo principal

1. registrar usuario em `POST /api/auth/register`
2. fazer login em `POST /api/auth/login`
3. autorizar o token no Swagger
4. criar conta financeira em `POST /api/accounts`
5. listar categorias em `GET /api/categories`
6. criar transacoes de despesa em `POST /api/transactions`
7. criar ou substituir o orcamento em `POST /api/budgets/monthly`
8. consultar `GET /api/budgets/monthly?month=3&year=2026`

### 4. Validacoes manuais minimas

- sem token, endpoints de orcamento retornam `401`
- categoria de receita no payload retorna `400`
- categoria duplicada no payload retorna `400`
- `totalSpent` considera apenas despesas do usuario autenticado no mes informado
- `remaining` fica negativo se o gasto ultrapassar o planejado
- categorias nao planejadas nao aparecem na resposta

## O que ficou explicitamente fora da Sprint 3

- orcamento de receitas
- rollover entre meses
- metas complexas
- multiplos cenarios de orcamento
- exibicao de categorias nao planejadas no resumo do orcamento
- bills
- recorrencia
- alertas
- importacao CSV
- dashboard visual

## Proximos passos recomendados para a Sprint 4

- importar transacoes via CSV
- evoluir categorizacao automatica simples por regras
- adicionar primeiros insights para demo
- preparar base para bills e vencimentos
- continuar ampliando testes conforme os novos fluxos entrarem
