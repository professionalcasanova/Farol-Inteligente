# Issue 37 - Modelagem de meio de pagamento ou origem sem poluir categorias

## Status

Decisao tomada: nao implementar um novo campo agora na wave curta atual.

O produto deve preservar `categoria` como resposta para "com o que gastei ou recebi?".
`PIX`, boleto, cartao, dinheiro e transferencia continuam fora da taxonomia principal de
categoria e orcamento.

## Problema

O feedback da beta mostrou um risco recorrente: o usuario naturalmente tenta misturar
motivo da movimentacao com forma de pagamento. Se o produto aceitar isso dentro da
categoria, a leitura financeira fica pior em tres pontos:

1. o dashboard perde clareza sobre onde o dinheiro foi consumido
2. o orcamento deixa de refletir prioridades reais de gasto
3. os insights passam a reagir a trilhas operacionais, nao a comportamentos financeiros

## Alternativas avaliadas

### 1. Reabrir categoria para incluir PIX, boleto, cartao e afins

Decisao: rejeitada.

Motivo:

- mistura semantica de "motivo" com "como foi pago"
- quebra comparabilidade por categoria ao longo do tempo
- piora a leitura de orcamento por categoria
- reabre exatamente o erro conceitual corrigido na v1.1

### 2. Adicionar campo livre de texto na transacao

Decisao: rejeitada para agora.

Motivo:

- o dado fica ruidoso demais para analise automatica
- importacao CSV teria mapeamento fraco e inconsistente
- o dashboard nao conseguiria usar o dado com confianca
- abriria custo de UX sem entregar leitura melhor no MVP

### 3. Adicionar campo dedicado e opcional para trilha de pagamento

Decisao: direcao recomendada para fase posterior.

Forma recomendada quando esse trabalho entrar:

- nome conceitual: `paymentTrail` ou `paymentChannel`
- tipo: enum controlado, nao texto livre
- valores iniciais sugeridos:
  - `pix`
  - `boleto`
  - `debit_card`
  - `credit_card`
  - `cash`
  - `bank_transfer`
  - `auto_debit`
  - `other`
- obrigatoriedade: opcional no inicio
- escopo semantico: registrar como a movimentacao aconteceu, sem competir com categoria

## Impacto por area

### Transacoes

- o formulario ganharia um campo extra opcional
- a listagem poderia exibir esse dado como metadado secundario
- edicao e importacao precisariam manter o campo sem promover isso ao centro da tela

### Importacao CSV

- so vale entrar junto quando houver mapeamento assistido ou heuristica confiavel
- para a fase atual, aceitar esse campo sem um contrato estavel aumentaria ambiguidade

### Dashboard

- o dashboard atual nao precisa desse dado para cumprir o valor principal
- eventualmente o campo pode enriquecer filtros e contexto operacional, mas nao deve
  entrar na logica principal de resumo do mes

### Insights e assistencia

- os insights atuais respondem melhor a categoria, vencimentos, saldo e reservas
- `paymentChannel` pode virar contexto complementar, nao base de decisao principal

## Riscos de implementar agora

- dispersar foco enquanto ainda existem entregas mais proximas do valor do mes
- expandir contrato de importacao sem base suficiente de formatos reais
- aumentar friccao no registro manual sem beneficio imediato claro
- induzir o time a usar o novo campo de forma analitica antes de validar a taxonomia

## Decisao de produto

Para a proxima onda curta, o tema fica como backlog futuro de modelagem e nao como
implementacao imediata.

O produto deve continuar comunicando que:

- categoria = motivo financeiro
- meio de pagamento/origem = trilha operacional secundaria

## Gatilhos para abrir a implementacao

Abrir essa frente quando pelo menos duas condicoes forem verdadeiras:

1. houver evidencias reais de uso pedindo esse recorte em homologacao
2. a importacao CSV estiver pronta para contratos mais ricos
3. a equipe quiser usar esse dado em filtros ou explicacoes, sem contaminar orcamento

## Proximo passo recomendado

Se o tema voltar para entrega, abrir uma issue de implementacao separada com este recorte:

- backend: campo opcional controlado em transacoes
- frontend: captura discreta no formulario de movimentacoes
- importacao: sem suporte na primeira entrega, a menos que exista mapeamento estavel
- analytics/insights: apenas exibicao contextual na primeira fase
