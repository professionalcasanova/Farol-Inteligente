# Base de Cenários de Demo do Farol

Usuários locais de desenvolvimento:

- Senha de todos: `123456`
- Conta padrão de todos: `Conta principal`

## Matriz

| Cenário | Email | Objetivo | Estado esperado |
|---|---|---|---|
| Cenário 1 | `saudavel@farol.local` | Mês saudável com sobra | `healthy` |
| Cenário 2 | `apertado@farol.local` | Mês apertado sem atraso | `attention` |
| Cenário 3 | `negativo@farol.local` | Saldo negativo | `critical` |
| Cenário 4 | `vencido@farol.local` | Saldo negativo + contas vencidas | `critical` |
| Cenário 5 | `orcamento@farol.local` | Orçamento estourado | `attention` |
| Cenário 6 | `lazer@farol.local` | Gasto não essencial alto | `attention` |
| Cenário 7 | `curtoprazo@farol.local` | Curto prazo pressionado | `critical` |
| Cenário 8 | `poucosdados@farol.local` | Poucos dados | ativação / pouco contexto |
| Cenário 9 | `multiplos@farol.local` | Múltiplos problemas simultâneos | `critical` |

## Leitura rápida

- `saudavel@farol.local`: sobra real e planejamento ainda respeitado.
- `apertado@farol.local`: sem atraso, mas o que ainda falta pagar já aperta o mês.
- `negativo@farol.local`: o mês já ficou no vermelho.
- `vencido@farol.local`: além do vermelho, já existem contas vencidas.
- `orcamento@farol.local`: o planejamento do mês foi ultrapassado.
- `lazer@farol.local`: os gastos ajustáveis estão pesando demais.
- `curtoprazo@farol.local`: os próximos vencimentos já passam da folga.
- `poucosdados@farol.local`: conta criada, mas quase sem dados para leitura real.
- `multiplos@farol.local`: cenário de crise com sinais simultâneos.

## Observação

Esses usuários são apenas para ambiente local de desenvolvimento. Não devem ser ativados em produção.
