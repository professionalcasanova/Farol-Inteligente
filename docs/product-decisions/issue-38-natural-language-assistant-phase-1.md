# Issue 38 - Primeira fase do assistente financeiro em linguagem natural

## Status

Decisao tomada: a fase 1 deve existir, mas com recorte estreito e grounded nos dados
estruturados que o Farol ja possui hoje.

Ela entra como candidata para a proxima onda curta somente se mantiver este escopo:

- responder perguntas sobre o mes atual
- explicar alertas e month health
- orientar o proximo passo pratico

Nao entra como chat financeiro geral, conselheiro amplo ou camada aberta de perguntas
fora do contexto do mes.

## Problema prioritario

O problema mais claro para resolver primeiro nao e "conversar livremente com IA".
O problema e este:

> o usuario recebe sinais importantes no dashboard, mas nem sempre entende rapido
> por que aquilo apareceu, o que pesa mais e qual acao vem primeiro

Entao a fase 1 do assistente deve reduzir interpretacao manual do dashboard e dos
insights existentes.

## Recorte recomendado para a fase 1

### Nome funcional

Assistente do mes.

### Perguntas que ele deve responder

1. "Por que meu mes esta em atencao/critico?"
2. "O que esta pesando mais agora?"
3. "Qual deveria ser meu proximo passo?"
4. "Quais contas ou gastos merecem revisao primeiro?"

### O que ele usa como base

- `month health`
- alertas financeiros
- dinheiro livre
- bills summary
- resumo mensal por categoria
- acoes recomendadas ja calculadas

### O que ele nao faz na fase 1

- planejamento de longo prazo
- recomendacao de investimento
- interpretacao juridica ou tributaria
- aconselhamento sobre credito
- comparacoes fora dos dados do proprio usuario
- respostas livres sem ancoragem nos dados do mes

## Forma de produto recomendada

A primeira entrega nao deve abrir como chat totalmente livre logo de saida.

Melhor formato inicial:

- caixa simples de pergunta com intents limitadas
- exemplos de perguntas logo abaixo
- respostas curtas, objetivas e ancoradas em dados do mes
- CTA final levando para a proxima tela operacional

Isso preserva a sensacao de linguagem natural sem abrir uma superficie ampla demais.

## Direcao tecnica

### Fase 1 recomendada

Implementar uma camada de orquestracao em cima dos dados estruturados existentes.

Fluxo sugerido:

1. receber a pergunta
2. classificar a intencao em um conjunto pequeno
3. montar contexto com dados ja calculados do mes
4. gerar resposta curta e controlada
5. devolver acao recomendada com link interno

### Ordem de preferencia tecnica

1. respostas estruturadas e templates com linguagem natural
2. camada de IA limitada apenas para reescrita/explicacao
3. chat generativo amplo apenas em fase posterior

Isso reduz custo, risco de alucinacao e dependencia de provedor logo no inicio.

## Riscos principais

### Privacidade

- perguntas podem incentivar o usuario a compartilhar informacoes alem do necessario
- a fase 1 deve operar apenas com contexto do mes ja existente no Farol

### Alucinacao

- o maior risco e responder alem do que os dados sustentam
- a resposta deve citar apenas causas observaveis no proprio mes

### Custo

- uma superficie aberta aumenta volume e imprevisibilidade
- intent limitada e resposta curta ajudam a controlar custo por chamada

### Dependencia de modelo

- nao faz sentido atrelar a primeira entrega a um provedor especifico
- o contrato interno deve depender de intents, contexto e resposta estruturada

## Limites de seguranca e produto

Na fase 1, o assistente deve:

- falar apenas sobre o mes atual e dados do usuario autenticado
- evitar prescricao definitiva
- nao prometer resultado financeiro
- sempre terminar com uma acao concreta dentro do produto
- degradar para resposta segura quando nao houver dados suficientes

## Relacao com a assistencia atual

O assistente nao substitui dashboard, alertas ou month health.

Ele funciona como camada de explicacao e priorizacao por cima do que ja existe.

Regra de produto:

- dashboard continua sendo a fonte principal de estado
- assistente vira atalho de interpretacao

## Decisao de roadmap

Esta frente pode entrar na proxima onda curta, desde que o escopo continue restrito a:

- explicacao de alertas
- explicacao do month health
- proximo passo sugerido com link interno

Se o escopo crescer para chat aberto, comparacoes amplas ou aconselhamento financeiro
generico, deve sair da onda curta e voltar para discovery.

## Contrato de MVP recomendado

Entrada:

- pergunta do usuario
- mes selecionado
- contexto financeiro consolidado do mes

Saida:

- resposta curta
- causa principal
- proximo passo
- `target` interno opcional

## Proximo passo recomendado

Abrir uma issue de implementacao da fase 1 com este recorte:

- UX simples de pergunta e resposta
- orquestracao baseada em intents
- grounding nos endpoints e insights atuais
- sem chat historico longo
- sem conhecimento externo
