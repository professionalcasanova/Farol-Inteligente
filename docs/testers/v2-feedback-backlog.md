# Backlog V2 a partir da rodada inicial de testers

Data de consolidacao: 2026-04-09

## Planejamento

### Extrato visual na edicao do mes
- Adicionar um extrato/resumo abaixo da edicao do mes para mostrar como as categorias estao ficando no snapshot atual.
- Objetivo: deixar mais claro o que pertence ao mes atual sem obrigar o usuario a interpretar apenas os campos.

### Separacao mais forte entre base recorrente e mes atual
- O fluxo ja separa a base recorrente da edicao mensal, mas a experiencia ainda nao comunica isso com forca suficiente.
- Na V2, revisar o comportamento para que o usuario nao confunda "base para meses seguintes" com "o que esta salvo neste mes".

## Importacao de dados

### CSV mais generico e adaptavel
- Evoluir a importacao para aceitar planilhas proprias e exports mais variados.
- Possiveis direcoes:
  - reconhecimento de colunas por nome aproximado
  - mapeamento assistido antes da importacao
  - suporte melhor a formatos de bancos diferentes

### Preparacao para Open Finance
- Nao entra na V1.
- Na V2, aprofundar descoberta tecnica e de produto para leitura automatica de movimentacoes a partir de integracoes reais.

## Movimentacoes

### Paginacao no historico
- Adicionar paginacao e controle de quantidade por pagina na lista de transacoes.
- O comportamento deve mudar apenas dentro do bloco de historico, sem transformar a tela em uma navegacao pesada.

## Observacao

Esses itens vieram de feedback direto da rodada inicial de validacao. Eles nao bloqueiam a V1 de testers, mas devem entrar cedo no planejamento da V2 por impacto claro na compreensao e na escalabilidade do produto.
