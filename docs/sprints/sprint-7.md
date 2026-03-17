# Sprint 7 - Bills e Vencimentos

## Objetivo da sprint

Entregar o modulo minimo de bills/vencimentos no Farol, com backend e front MVP integrados, adicionando tambem um resumo acionavel de contas a pagar no dashboard.

## Entregas concluidas

### Modulos entregues

- `Bills`
- `Dashboard`
- `web`

### Fluxos entregues

- criacao de bills do usuario autenticado
- listagem de bills com filtros simples por mes, ano e status
- marcacao e desmarcacao de pagamento
- pagina web `/bills` para demonstracao do fluxo
- resumo de bills no dashboard com totais, contagens e proximos vencimentos

## Abordagem implementada

- modulo simples sobre a arquitetura atual do monolito modular
- uso direto de `FarolDbContext`
- entidade `Bill` com ownership por `userId`
- persistencia em tabela propria com migration nova
- status derivado a partir de `IsPaid` e `DueOn`, sem coluna extra de status
- front MVP reutilizando a estrutura existente em `web/`

Persistencia:

- entidade nova: `Bill`
- `DbSet` adicionado ao `FarolDbContext`
- mapeamento EF Core dedicado
- migration `AddBills` criada nesta sprint

## Endpoints de bills implementados

Endpoints disponiveis:

- `POST /api/bills`
- `GET /api/bills`
- `PATCH /api/bills/{id}/pay`
- `PATCH /api/bills/{id}/unpay`

Comportamento atual:

- bills pertencem ao usuario autenticado
- criacao exige `description`, `amount > 0` e `dueOn`
- `GET /api/bills` aceita filtros simples por `month`, `year` e `status`
- marcar como pago define `IsPaid = true` e `PaidAtUtc`
- desmarcar pagamento limpa `PaidAtUtc`

## Comportamento do bills-summary no dashboard

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

Regras atuais:

- considera apenas bills do usuario autenticado
- considera apenas o mes/ano informado
- `paid`: `IsPaid = true`
- `overdue`: `IsPaid = false` e `dueOn < hoje`
- `pending`: `IsPaid = false` e `dueOn >= hoje`
- `upcoming` retorna ate `5` bills pendentes ordenadas por `dueOn`

## Decisoes tecnicas importantes

- mantido o monolito modular atual
- mantido uso direto de `FarolDbContext` nos controllers
- sem `CQRS`, sem camada de application e sem repository generico
- sem recorrencia de bills nesta etapa
- sem alertas automaticos persistidos nesta etapa
- status de bill calculado em tempo de leitura para evitar duplicacao de estado
- front mantido como MVP com sessao local em `localStorage`, decisao temporaria ja assumida desde a Sprint 6

## Cobertura de testes adicionada nesta sprint

Cobertura adicionada:

- `BillTests`
- `BillsEndpointsTests`
- `BillsSummaryEndpointsTests`

Casos cobertos:

- autenticacao obrigatoria
- criacao de bill
- ownership por usuario autenticado
- marcar bill como paga
- desmarcar pagamento
- filtros simples por status e por mes/ano
- calculo correto de pending, overdue e paid no dashboard
- ordenacao de `upcoming`
- isolamento entre usuarios

Estado validado no fechamento desta sprint:

- `65` testes passando na suite atual

## Como validar manualmente a Sprint 7

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

### 2. Subir backend e front

```powershell
dotnet run --project src/Farol.Api/Farol.Api.csproj -c Release --no-build
```

Em outro terminal:

```powershell
Set-Location web
npm install
npm run dev
```

Aplicacao web:

- `http://localhost:3000`

### 3. Validar fluxo principal

1. acessar `/login`
2. entrar com um usuario existente
3. abrir `/bills`
4. criar bills com vencimentos futuros e vencidos
5. marcar uma bill como paga
6. desmarcar o pagamento de uma bill
7. voltar para `/dashboard`
8. validar os cards de `A pagar`, `Vencido` e `Pago`
9. validar a lista de proximas contas pendentes

### 4. Validacoes manuais minimas

- usuario autenticado so enxerga as proprias bills
- filtro por status em `/bills` funciona
- filtro por mes em `/bills` funciona
- bill paga muda de status e registra `PaidAtUtc`
- bill desmarcada limpa `PaidAtUtc`
- dashboard mostra totais coerentes com as bills do mes
- `upcoming` mostra apenas contas pendentes e ordenadas por vencimento

## O que ficou explicitamente fora da Sprint 7

- recorrencia automatica de bills
- lembretes e notificacoes
- pagamento automatico
- anexos ou comprovantes
- conciliacao com transacoes
- dashboard com historico de bills entre meses
- edicao e exclusao de bills no front

## Proximos passos recomendados para a Sprint 8

- conectar bills com alertas mais visiveis no produto
- considerar conciliacao simples entre bills e transacoes
- evoluir o dashboard com sinais de risco de vencimento
- adicionar edicao e exclusao de bills quando o fluxo do MVP estiver estavel
- preparar o primeiro passo para alertas e assistente financeiro sobre vencimentos
