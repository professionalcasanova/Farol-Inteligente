# Sprint 5 - Insight de Dinheiro Livre

## Objetivo da sprint

Entregar o primeiro insight financeiro de alto valor do Farol, respondendo quanto o usuario ainda pode gastar no mes sem comprometer o que ja esta reservado no orcamento.

## Entregas concluidas

### Modulo entregue

- `Insights`

### Fluxo entregue

- consulta do insight mensal de dinheiro livre do usuario autenticado
- uso combinado de transacoes e orcamento mensal ja existentes
- calculo consolidado sem criar estruturas extras de persistencia

## Abordagem implementada

- endpoint `GET` protegido com JWT
- uso direto de `FarolDbContext`
- leitura dos dados do mes/ano solicitado
- uso apenas de estruturas ja existentes:
  - `transactions`
  - `monthly_budgets`
  - `monthly_budget_categories`

Persistencia:

- nenhuma entidade nova foi criada nesta sprint
- nenhuma migration nova foi necessaria
- o insight utiliza apenas dados que ja eram persistidos pelo sistema

## Endpoint implementado

- `GET /api/insights/free-money?month={m}&year={y}`

Resposta minima entregue:

- `month`
- `year`
- `totalIncome`
- `totalExpense`
- `balance`
- `totalPlannedBudget`
- `totalBudgetSpent`
- `totalBudgetRemaining`
- `freeToSpend`

## Regra de calculo usada para free money

Calculos entregues:

- `totalIncome = soma das transacoes de receita do mes`
- `totalExpense = soma das transacoes de despesa do mes`
- `balance = totalIncome - totalExpense`
- `totalPlannedBudget = soma do orcamento planejado do mes`
- `totalBudgetSpent = soma das despesas realizadas nas categorias orcadas`
- `totalBudgetRemaining = totalPlannedBudget - totalBudgetSpent`

Definicao atual de `freeToSpend`:

- `freeToSpend = balance - max(totalBudgetRemaining, 0)`

Interpretacao:

- se ainda existe valor reservado no orcamento, ele reduz o dinheiro livre
- se o orcamento ja foi totalmente consumido ou estourado, ele nao reduz alem de zero nesta etapa

## Decisoes tecnicas importantes

- mantido o monolito modular atual
- mantido uso direto de `FarolDbContext` no controller
- sem `Application` layer, sem `CQRS` e sem repository generico
- sem nova estrutura de persistencia
- calculo restrito ao usuario autenticado e ao mes/ano solicitado
- categorias fora do orcamento continuam afetando `totalExpense` e `balance`, mas nao entram em `totalBudgetSpent`

## Testes automatizados existentes nesta sprint

Cobertura adicionada:

- `FreeMoneyInsightsEndpointsTests`

Casos cobertos:

- autenticacao obrigatoria
- calculo correto com receitas e despesas
- calculo correto com orcamento mensal
- orcamento vazio
- usuario sem transacoes
- isolamento por usuario autenticado

Estado validado no fechamento desta sprint:

- `48` testes passando na suite atual

## Como validar manualmente a Sprint 5

### 1. Subir banco, compilar e aplicar schema atual

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
5. criar transacoes em `POST /api/transactions`
6. criar orcamento em `POST /api/budgets/monthly`
7. consultar `GET /api/insights/free-money?month=3&year=2026`

### 4. Validacoes manuais minimas

- sem token, o endpoint retorna `401`
- sem orcamento, `freeToSpend` fica igual ao `balance`
- sem transacoes, os totais retornam `0`
- usuario autenticado nao enxerga dados de outro usuario
- quando o orcamento restante fica negativo, ele nao reduz alem de zero o `freeToSpend`

## O que ficou explicitamente fora da Sprint 5

- dashboard visual
- insights narrativos
- alertas automaticos
- bills
- previsao de fluxo de caixa futura
- recomendacoes personalizadas
- IA para interpretacao dos dados

## Proximos passos recomendados para a Sprint 6

- evoluir bills e vencimentos
- adicionar primeiros alertas operacionais
- construir insights adicionais sobre risco do mes
- preparar base para uma camada de recomendacoes futuras
- continuar ampliando cobertura de testes conforme novos fluxos entrarem
