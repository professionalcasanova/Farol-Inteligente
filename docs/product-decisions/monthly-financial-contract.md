# Contrato financeiro mensal

## Objetivo

Formalizar o contrato oficial dos conceitos mensais do Farol sem introduzir nova feature e sem alterar o frontend nesta etapa.

Restrições mantidas:

- o ledger de pagamento continua baseado em `transactions`
- `balance` nao incorpora `bills` pendentes
- orcamento nao recalcula saldo contabil
- recorrencia continua modelada com `bill_series` + `bills`

## Escopo do contrato

Este documento define a diferenca oficial entre:

- `balance`
- `freeToSpend`
- `projection`

Tambem registra:

- onde o codigo atual ja respeita esse contrato
- onde ainda existe ambiguidade semantica
- qual o menor ajuste recomendado no backend
- quais testes devem proteger o contrato

## Definicoes oficiais

### `balance`

`balance` e o saldo contabil realizado do mes consultado.

Regra oficial:

- `balance = totalIncome - totalExpense`

Fontes:

- `totalIncome`: soma das `transactions` do tipo `Income` ocorridas dentro do mes
- `totalExpense`: soma das `transactions` do tipo `Expense` ocorridas dentro do mes

O que entra:

- qualquer movimentacao real persistida em `transactions`
- inclusive a transacao real criada ao pagar uma `bill`

O que nao entra:

- `bills` ainda nao pagas
- orcamento planejado
- reservas
- previsoes
- recorrencia futura ainda nao paga

Semantica oficial:

- `balance` representa apenas realizado contabil
- `balance` nunca deve ser reinterpretado como disponibilidade
- `balance` deve ter o mesmo significado em qualquer endpoint que o exponha

### `freeToSpend`

`freeToSpend` e a disponibilidade operacional do mes depois de reservar compromissos ainda nao realizados, mas ja conhecidos.

Regra oficial:

- `freeToSpend = balance - plannedReserve - unpaidBillsReserve`

Onde:

- `plannedReserve = max(totalBudgetRemaining, 0)`
- `unpaidBillsReserve = totalPendingBills + totalOverdueBills`

Semantica oficial:

- `freeToSpend` nao e saldo contabil
- `freeToSpend` e uma leitura de margem disponivel para decisao
- `freeToSpend` pode ser menor que `balance`
- `freeToSpend` pode ser negativo mesmo quando `balance` ainda e positivo

O que nao pode acontecer:

- usar orcamento para alterar `balance`
- usar `bills` pendentes para alterar `balance`
- fazer `freeToSpend` significar ora disponibilidade, ora projecao, ora saldo

### `projection`

`projection` nao e um valor monetario.

`projection` e um estado do periodo consultado, indicando que o mes pedido ainda nao e o mes corrente realizado.

Regra oficial:

- `projection = (periodo consultado > mes corrente)`

Semantica oficial:

- `projection` informa o modo de leitura do periodo
- `projection` nao muda a definicao de `balance`
- `projection` nao muda a definicao de `freeToSpend`
- `projection` apenas informa que os numeros daquele mes ainda devem ser lidos como fotografia de um periodo futuro

Implicacao:

- se o sistema expuser `balance` e `freeToSpend` em mes futuro, os campos mantem o mesmo contrato semantico
- o tratamento de UX, alertas e tom para mes futuro deve depender de `isProjection`, nao de formulas especiais embutidas em `freeToSpend`

## Mapeamento do contrato para o codigo atual

### O que ja esta correto

#### `balance`

O endpoint de resumo mensal ja usa o contrato correto:

- soma `transactions` do mes
- calcula `balance = totalIncome - totalExpense`

Arquivo atual:

- `src/Farol.Api/Modules/Dashboard/DashboardController.cs`

O endpoint de `free-money` tambem deriva `balance` da mesma base contabil:

- soma `transactions` do mes
- calcula `balance = totalIncome - totalExpense`

Arquivo atual:

- `src/Farol.Api/Modules/Insights/MonthlyInsightsService.cs`

#### Pagamento ledger-based

O pagamento de `bill` continua coerente com o contrato:

- pagar uma `bill` cria uma `transaction` real do tipo `Expense`
- a `bill` recebe `PaidTransactionId`
- ao desfazer pagamento, a `transaction` vinculada e removida

Arquivo atual:

- `src/Farol.Api/Modules/Bills/BillPaymentService.cs`

Isso preserva a regra central:

- `balance` muda apenas porque o ledger mudou

#### Recorrencia

O modelo `bill_series` + `bills` tambem continua compativel:

- recorrencia representa compromisso futuro
- apenas o pagamento converte esse compromisso em saida contabil real

Arquivos atuais:

- `src/Farol.Domain/Bills/BillSeries.cs`
- `src/Farol.Api/Modules/Bills/BillSeriesExpansionService.cs`

### Pontos de conflito no codigo atual

#### 1. `freeToSpend` muda de formula quando `isProjection` e verdadeiro

Hoje o codigo faz:

- mes atual/passado: `freeToSpend = balance - plannedRemaining - unpaidBillsReserve`
- mes futuro: `freeToSpend = balance`

Arquivo atual:

- `src/Farol.Api/Modules/Insights/MonthlyInsightsService.cs`

Conflito:

- `freeToSpend` deixa de ter definicao unica
- `isProjection` deixa de ser apenas estado do periodo e passa a alterar a semantica do campo monetario
- dois meses com mesma composicao podem expor contratos diferentes so por estarem em tempos diferentes

#### 2. O endpoint de `free-money` mistura contrato financeiro com politica de leitura

Hoje `plannedReserve` e `unpaidBillsReserve` continuam sendo calculados mesmo em projecao, mas `freeToSpend` deixa de desconta-los no mes futuro.

Conflito:

- o payload informa as reservas
- mas o campo final ignora essas reservas quando o mes e futuro
- isso gera leitura ambigua para consumidores do backend

#### 3. `balance` aparece em mais de um endpoint, mas sem contrato consolidado em documentacao unica

Hoje existe consistencia de implementacao, mas a documentacao historica ficou espalhada entre sprints.

Conflito:

- facil reabrir discussao errada no futuro
- risco de alguem tentar colocar `bills` pendentes dentro de `balance`
- risco de alguem tentar usar orcamento para recalcular saldo contabil

## Proposta objetiva de contrato

### Contrato oficial minimo

- `balance` = saldo contabil realizado do mes, baseado apenas em `transactions`
- `freeToSpend` = disponibilidade operacional apos descontar reserva de orcamento ainda nao consumida e `bills` em aberto do mes
- `projection` = indicador de que o periodo consultado e futuro; nao redefine formulas

### Formulas oficiais

- `totalIncome = sum(transactions where type = Income in month)`
- `totalExpense = sum(transactions where type = Expense in month)`
- `balance = totalIncome - totalExpense`
- `plannedReserve = max(totalBudgetRemaining, 0)`
- `unpaidBillsReserve = totalPendingBills + totalOverdueBills`
- `freeToSpend = balance - plannedReserve - unpaidBillsReserve`
- `isProjection = requestedMonthStart > currentMonthStart`

### Leitura oficial por caso

Mes atual:

- `balance` = realizado contabil do mes
- `freeToSpend` = margem operacional restante do mes
- `isProjection = false`

Mes passado:

- `balance` = realizado contabil fechado daquele mes
- `freeToSpend` = leitura operacional retrospectiva daquele mes
- `isProjection = false`

Mes futuro:

- `balance` = realizado contabil ja existente naquele mes futuro, se houver
- `freeToSpend` = disponibilidade projetada do mes segundo as reservas conhecidas
- `isProjection = true`

Observacao:

- o fato de um mes futuro ainda ter poucas ou nenhuma `transaction` nao justifica redefinir a semantica de `freeToSpend`
- isso deve ser tratado por UX, copy e politicas de alerta, nao por formula especial

## Sugestao minima de ajuste backend

Sem criar feature nova, o menor ajuste recomendado e:

1. manter `balance` exatamente como esta
2. manter `isProjection` exatamente como identificador de periodo futuro
3. remover o desvio de formula em `MonthlyInsightsService`

Em outras palavras:

- trocar a regra atual que faz `freeToSpend = balance` em mes futuro
- por uma formula unica para qualquer periodo:
  - `freeToSpend = balance - plannedReserve - unpaidBillsReserve`

Justificativa:

- elimina sobreposicao semantica
- preserva ledger-based payments
- nao mistura `bills` pendentes com `balance`
- nao usa orcamento para recalcular saldo contabil
- reduz surpresa para qualquer consumidor do backend

O que nao precisa mudar nesta tarefa:

- frontend
- fluxo de pagamento
- modelo de recorrencia
- DTOs publicos, se a equipe quiser adiar renomeacoes

## Lista de testes necessarios

### Contrato de `balance`

- `monthly-summary` deve calcular `balance` apenas com `transactions` do mes
- `free-money` deve calcular o mesmo `balance` contabil do resumo mensal quando consultarem o mesmo periodo
- `bills` pendentes sem pagamento nao devem alterar `balance`
- pagar `bill` deve alterar `balance` apenas porque criou `transaction`
- desfazer pagamento deve restaurar `balance` ao remover a `transaction`

### Contrato de `freeToSpend`

- `freeToSpend` deve descontar `plannedReserve` sem alterar `balance`
- `freeToSpend` deve descontar `unpaidBillsReserve` sem alterar `balance`
- `freeToSpend` nao deve descontar orcamento duas vezes quando gasto ja superou o planejado
- `freeToSpend` pode ficar negativo mesmo com `balance` positivo

### Contrato de `projection`

- mes futuro deve retornar `isProjection = true`
- mes atual e passado devem retornar `isProjection = false`
- mes futuro nao deve mudar a definicao de `balance`
- mes futuro nao deve mudar a definicao de `freeToSpend`
- mes futuro pode seguir politica diferente de alertas, mas nao de formula

### Regressao de fronteira

- mes futuro sem `transactions`, com orcamento e `bills`, deve manter `balance = 0` e `freeToSpend` calculado pela formula unica
- mes atual com `bills` recorrentes expandidas e ainda nao pagas deve refletir reserva apenas em `freeToSpend`
- pagamento de ocorrencia recorrente deve afetar ledger e `balance`, nao apenas status da `bill`

## Decisao recomendada

Adotar oficialmente:

- `balance` como contrato contabil
- `freeToSpend` como contrato operacional
- `projection` como contrato de contexto temporal

E tratar qualquer mensagem mais suave para mes futuro como responsabilidade de camada de apresentacao e alertas, nao como mudanca de formula monetaria.
