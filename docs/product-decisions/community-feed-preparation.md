# Preparacao futura para comunidade e feed no Farol

## Status

Proposta tecnica registrada. Nao implementar agora.

Esta decisao documenta uma direcao futura para comunidade no Farol sem criar forum,
endpoints, tabelas, filas, moderacao automatica ou alteracoes no frontend neste momento.

## Contexto

O Farol e um assistente financeiro pessoal. Qualquer superficie publica ou
semipublica dentro do produto aumenta risco operacional, juridico e de qualidade do
conteudo. Em especial, uma comunidade financeira pode rapidamente virar canal de:

- recomendacao financeira informal
- exposicao de dados sensiveis
- spam
- captacao indevida de usuarios
- discussoes sobre impostos, investimentos e dividas sem contexto profissional
- moderacao manual custosa para uma equipe pequena

Por isso, a evolucao comunitaria deve partir de objetos estruturados e de baixo risco,
como orcamentos compartilhaveis, antes de abrir conversas livres.

## Por que nao implementar forum completo agora

Um forum completo exige muito mais que endpoints de posts e respostas. O produto teria
que operar:

- identidade publica e politicas de perfil
- regras de conduta
- ferramentas de denuncia, bloqueio, remocao e auditoria
- revisao de conteudo e resposta a abuso
- deteccao e tratamento de spam
- separacao clara entre experiencia de produto e aconselhamento financeiro
- governanca juridica para conteudo gerado por usuario

No estagio atual, isso deslocaria o foco do MVP financeiro para operacao de comunidade.
O custo de moderacao e responsabilidade cresce antes de haver sinal suficiente de que a
base ativa precisa de forum aberto.

Decisao: nao construir forum no MVP. A base minima ja criada para orcamentos da
comunidade deve ser o primeiro degrau de validacao.

## Diferencas de produto

### Forum

Espaco de discussao aberto ou semiestruturado, com topicos, respostas, historico de
conversa, busca, reputacao e moderacao ativa.

Caracteristicas:

- alto volume de texto livre
- maior risco de recomendacoes indevidas
- maior risco de conflito entre usuarios
- exige moderacao e regras publicas maduras
- so deve ser considerado com base ativa e capacidade operacional

### Feed de comunidade

Superficie curada para exibicao de conteudo comunitario controlado. Pode mostrar
orcamentos publicados, exemplos editoriais, desafios financeiros, perguntas
pre-aprovadas ou posts de baixo risco.

Caracteristicas:

- fluxo mais simples que forum
- conteudo pode ser filtrado, ordenado e limitado
- permite moderacao preventiva ou curadoria
- reduz liberdade de publicacao em troca de seguranca

### Orcamentos compartilhaveis

Modelo estruturado de orcamento que outro usuario pode visualizar, importar e adaptar
na propria conta.

Caracteristicas:

- dado estruturado, com campos conhecidos
- sem conversa livre por padrao
- mais facil validar dados sensiveis
- mais facil esconder/remover quando denunciado
- melhor ponto de partida para comunidade no Farol

## Riscos principais

### Moderacao

Conteudo financeiro exige avaliacao de contexto. Mesmo mensagens bem-intencionadas podem
ser inadequadas para outro usuario. A moderacao deve priorizar remocao de dados
sensiveis, fraude, assedio, spam e falsas promessas.

### Spam

Qualquer superficie publica pode ser usada para divulgar links, canais externos,
consultorias, promessas de renda, golpes ou captacao de clientes. O MVP deve evitar
texto livre amplo e impedir dados de contato em campos publicos.

### Recomendacoes financeiras indevidas

O Farol nao deve permitir que usuarios apresentem conteudo comunitario como conselho
financeiro oficial. Textos de feed devem ter linguagem de exemplo, experiencia ou
modelo, nunca recomendacao personalizada.

### Impostos e investimentos

Topicos de impostos, investimentos, previdencia, cripto, trading, dividas complexas ou
renegociacao podem depender de situacao pessoal e regulacao. O produto deve evitar
abrir espacos onde respostas parecam consultoria financeira, contabil ou juridica.

### Responsabilidade juridica

Conteudo gerado por usuario pode gerar pedidos de remocao, denuncia, auditoria,
retencao, contestacao e suporte. Antes de forum ou comentarios abertos, o Farol deve ter
politicas, logs e processo operacional definidos.

## Proposta futura em fases

### 1. Orcamentos compartilhaveis

Estado desejado:

- usuarios criam modelos estruturados
- modelos publicados aparecem em listagem publica
- outros usuarios importam copias independentes
- denuncia basica remove item da listagem publica
- sem comentarios, curtidas ou ranking complexo

Objetivo da fase:

- validar se existe interesse em aprender por modelos
- manter risco baixo com dados estruturados
- criar base inicial de governanca

### 2. Feed curado

Estado desejado:

- feed exibe orcamentos publicados e conteudos selecionados
- ordenacao simples por data, tema ou curadoria
- tags controladas, sem hashtags livres no inicio
- conteudo denunciado, oculto ou em revisao nao aparece

Objetivo da fase:

- melhorar descoberta sem abrir discussao livre
- permitir curadoria editorial ou operacional
- medir consumo antes de permitir interacao textual

### 3. Comentarios controlados

Estado desejado:

- comentarios somente em objetos especificos, como orcamentos publicados
- limites de tamanho e frequencia
- denuncia por comentario
- ocultacao por status
- sem mensagens privadas
- sem links externos inicialmente

Objetivo da fase:

- testar interacao social com superficie limitada
- manter moderacao granular
- evitar que comentarios virem consultoria financeira aberta

### 4. Forum apenas se houver base ativa

Estado desejado:

- forum so e considerado se feed e comentarios tiverem uso saudavel
- categorias limitadas e moderadas
- topicos sensiveis podem ser bloqueados ou redirecionados para conteudo educativo
- ferramentas de moderacao e auditoria ja devem existir

Objetivo da fase:

- abrir discussao mais ampla somente com demanda comprovada e capacidade operacional

## Possivel modelo de dominio futuro

Modelo conceitual, sem compromisso de implementacao imediata:

```text
CommunityPost
- Id
- AuthorUserId
- SourceType: community_budget | editorial | prompt | user_post
- SourceId nullable
- Title
- Body nullable
- Status: draft | published | hidden | reported | archived
- Visibility: public | authenticated | private
- CreatedAt
- UpdatedAt
- PublishedAt nullable

CommunityPostTag
- Id
- PostId
- Name

CommunityReaction
- Id
- PostId
- UserId
- Type: save | helpful
- CreatedAt

CommunityComment
- Id
- PostId
- AuthorUserId
- Body
- Status: published | hidden | reported
- CreatedAt
- UpdatedAt

CommunityReport
- Id
- TargetType: post | comment | community_budget
- TargetId
- ReporterUserId
- Reason: sensitive_data | offensive_content | spam | misleading | financial_advice | other
- Description nullable
- CreatedAt

ModerationAction
- Id
- TargetType
- TargetId
- ActorUserId nullable
- Action: hide | restore | archive | reject | mark_reviewed
- Reason nullable
- CreatedAt
```

Observacoes:

- `CommunityBudgetReport` ja cobre denuncias de orcamentos compartilhaveis no MVP.
- Um modelo futuro pode unificar denuncias em `CommunityReport`, mas isso nao deve ser
  feito antes de existir mais de um tipo real de alvo.
- Evitar generalizar moderacao cedo demais; consolidar apenas quando houver segundo
  caso de uso.

## Possiveis endpoints futuros

Nao implementar agora. Lista apenas para orientar contratos futuros:

```http
GET /api/community/feed
GET /api/community/posts/{id}
POST /api/community/posts
PUT /api/community/posts/{id}
DELETE /api/community/posts/{id}
POST /api/community/posts/{id}/reports
GET /api/community/posts/{id}/comments
POST /api/community/posts/{id}/comments
POST /api/community/comments/{id}/reports
DELETE /api/community/comments/{id}
POST /api/community/posts/{id}/save
DELETE /api/community/posts/{id}/save
```

Endpoints administrativos ou operacionais so devem ser considerados quando houver
processo de moderacao definido. Nao criar painel administrativo completo antes disso.

## Regras minimas de seguranca e moderacao

Obrigatorias antes de qualquer expansao alem de orcamentos compartilhaveis:

- conteudo publico nao pode conter email, telefone, CPF, CNPJ, token, senha, dados de
  cartao ou identificadores financeiros sensiveis
- conteudo denunciado deve sair da listagem publica enquanto aguarda avaliacao
- um usuario nao pode denunciar o mesmo alvo repetidas vezes
- autor nao pode denunciar o proprio conteudo
- endpoints devem retornar `404` para recurso inexistente ou sem permissao quando esse
  for o padrao de seguranca do Farol
- logs nao devem guardar corpo completo de comentarios, tokens, dados de contato ou
  dados financeiros sensiveis
- limites de tamanho e frequencia devem existir antes de texto livre
- links externos devem ser bloqueados ou moderados no inicio
- conteudo deve deixar claro que modelos sao exemplos, nao recomendacoes financeiras
  oficiais
- conteudo sobre impostos, investimentos e dividas complexas deve ser tratado com
  cautela e, se necessario, bloqueado ou redirecionado para material educativo

## O que nao deve ser feito no MVP

- forum aberto
- mensagens privadas entre usuarios
- comentarios livres em qualquer tela
- ranking complexo de usuarios
- gamificacao competitiva de desempenho financeiro
- recomendacoes financeiras oficiais geradas por usuarios
- categorias abertas para investimentos, impostos ou consultoria
- publicacao de links externos sem moderacao
- automacao de moderacao por IA como unica barreira
- painel administrativo completo antes de haver processo operacional
- migrar todos os conceitos para um modelo generico de comunidade sem necessidade real

## Criterios para abrir implementacao futura

Antes de iniciar uma nova fase, validar:

- uso real de orcamentos compartilhaveis
- volume de denuncias e tipos de abuso observados
- capacidade minima de revisar conteudo reportado
- politica de conteudo escrita
- limites de dados pessoais e financeiros definidos
- contrato de frontend revisado
- testes de autorizacao, visibilidade, denuncia e ocultacao planejados
- decisao explicita sobre o que continua fora de escopo
