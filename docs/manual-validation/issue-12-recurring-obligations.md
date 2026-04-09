# Issue 12 Manual Validation

## Objetivo
Confirmar que contas recorrentes e parceladas entram no dashboard e nos insights do mes como compromissos previsiveis reais.

## Preparacao
1. Suba PostgreSQL, API, web e servico Python.
2. Entre com um usuario limpo.
3. Cadastre uma conta financeira.

## Fluxo principal
1. Em `Contas a pagar`, crie uma conta recorrente mensal, por exemplo `Internet`, com vencimento no mes atual.
2. Ainda em `Contas a pagar`, crie uma conta parcelada, por exemplo `Notebook`, com `10` parcelas.
3. Volte ao `Dashboard` no mesmo mes.
4. Confira que o bloco `Compromissos previsiveis em aberto` aparece com totais de recorrentes e parceladas.
5. Confira que a lista `Proximas contas` mostra selo `Recorrente` para a serie mensal e `Parcela X/Y` para a parcelada.
6. Confira que `Dinheiro livre` menciona que parte das contas em aberto ja vem de contas recorrentes e parcelas.

## Fluxo de insight
1. Deixe o mes pressionado com renda baixa ou saidas altas.
2. Garanta que as contas recorrentes ou parceladas ainda estejam em aberto.
3. No dashboard, valide se o estado do mes menciona pressao de compromissos previsiveis quando relevante.
4. No sino ou bloco critico, valide que a navegacao continua contextual e nao perde o foco do problema.

## Resultado esperado
- O dashboard mostra os compromissos previsiveis do mes sem esconder essa pressao.
- O dinheiro livre segue coerente com contas pendentes, vencidas, recorrentes e parceladas.
- O month health consegue citar compromissos previsiveis quando eles ja estao pesando no caixa.
