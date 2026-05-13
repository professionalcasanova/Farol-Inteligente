# Integracao com WhatsApp no Farol

## Status

Decisao tomada: adiar WhatsApp no MVP.

No curto prazo, o Farol deve manter comunicacao por email transacional e notificacoes
internas. Quando houver necessidade real de producao, usar WhatsApp Business Platform
oficial, diretamente pela Cloud API ou por um BSP confiavel.

Nao usar automacao nao oficial de WhatsApp Web para dados financeiros.

## Problema

WhatsApp pode ser util futuramente para:

- alertas
- lembretes
- avisos operacionais
- reengajamento leve

Mas o Farol lida com dados financeiros. Uma integracao instavel, baseada em QR Code,
sessao de navegador ou protocolo nao oficial pode gerar:

- bloqueio do numero
- perda de canal com usuarios
- vazamento de historico/midia/sessao
- dependencia dificil de auditar
- custo operacional maior que o ganho no MVP

## Alternativas avaliadas

### 1. WhatsApp Business Platform oficial

Decisao: usar somente quando houver necessidade real de producao.

Pros:

- caminho oficial da Meta/WhatsApp
- adequado para producao e escala
- suporta templates aprovados, webhooks e categorias de mensagem
- menor risco de bloqueio por uso de canal nao autorizado
- custo previsivel por categoria/mercado quando bem modelado

Contras:

- exige configuracao de Business Manager, numero, templates e webhooks
- tem custo por mensagem entregue
- templates podem ser recusados/reclassificados
- exige opt-in e governanca de conteudo
- nao deve ser usado para mandar dado financeiro sensivel em texto claro

Uso recomendado:

- apenas mensagens genericas e de baixo risco
- exemplo: "Voce tem um lembrete pendente no Farol. Acesse o app para ver detalhes."

### 2. WPPConnect

Decisao: nao usar em producao para dados financeiros.

Pros:

- open source
- facilita prototipos e automacoes com WhatsApp Web
- possui comunidade e ferramentas auxiliares

Contras:

- baseado em automacao/exportacao de funcoes do WhatsApp Web
- depende de sessao/pareamento e mudancas internas do WhatsApp Web
- risco de quebra sem aviso
- risco de bloqueio do numero
- superficie sensivel para armazenamento de sessao

Uso recomendado:

- no maximo laboratorio isolado, sem usuario real e sem dado financeiro
- nao usar no MVP/producao do Farol

### 3. open-wa / wa-automate

Decisao: nao usar em producao para dados financeiros.

Pros:

- ecossistema open source
- pode acelerar testes locais de automacao
- baixo custo direto de ferramenta

Contras:

- historicamente ligado a automacao de WhatsApp Web
- manutencao/compatibilidade dependem de mudancas do WhatsApp
- risco operacional alto para canal critico
- nao substitui compliance da API oficial

Uso recomendado:

- apenas avaliacao tecnica offline
- nao usar com usuarios reais do Farol

### 4. Evolution API e similares

Decisao: nao usar como atalho para WhatsApp Web no MVP.

Pros:

- oferece API pronta, Docker, webhooks e integracoes
- pode suportar Cloud API oficial em alguns modos
- reduz trabalho de integracao em cenarios nao financeiros

Contras:

- modos baseados em Baileys/WhatsApp Web continuam sendo nao oficiais
- pode persistir sessoes, mensagens e midias dependendo da configuracao
- adiciona um intermediario grande para um canal ainda nao validado no produto
- licenciamento, telemetria, storage e operacao precisam ser revisados

Uso recomendado:

- considerar apenas se configurado exclusivamente com WhatsApp Cloud API oficial
- ainda assim, avaliar se nao e mais simples integrar direto ou via BSP

## Matriz de risco

| Opcao | Bloqueio de numero | Vazamento de dados | Estabilidade | Custo inicial | Recomendacao |
|---|---:|---:|---:|---:|---|
| WhatsApp Business Platform oficial | Baixo | Medio | Alta | Medio | Usar quando houver necessidade real |
| BSP oficial sobre Cloud API | Baixo | Medio | Alta | Medio/alto | Avaliar se reduzir operacao |
| WPPConnect | Alto | Alto | Baixa/media | Baixo | Nao usar em producao |
| open-wa / wa-automate | Alto | Alto | Baixa/media | Baixo | Nao usar em producao |
| Evolution API via Baileys/Web | Alto | Alto | Media | Baixo/medio | Nao usar no MVP |
| Evolution API via Cloud API oficial | Baixo/medio | Medio | Media/alta | Medio | Avaliar depois, com due diligence |

## Decisao tecnica

Para o MVP:

1. Nao implementar WhatsApp.
2. Usar email transacional para autenticacao e comunicacoes sensiveis.
3. Usar notificacoes internas para alertas financeiros dentro do produto.
4. Nao enviar saldo, valores detalhados, nomes completos, emails ou dados de conta por
   WhatsApp.

Quando a demanda justificar:

1. Usar WhatsApp Business Platform oficial.
2. Criar opt-in explicito por usuario.
3. Criar opt-out simples.
4. Enviar apenas mensagens genericas.
5. Manter detalhe financeiro dentro do Farol, atras de autenticacao.

## Conteudo permitido

Permitido:

- lembrete generico
- aviso de acao pendente
- notificacao de seguranca sem dado sensivel
- link para abrir o Farol

Nao permitido:

- saldo
- valor de conta
- categoria com valor
- nome completo de terceiros
- comprovantes
- tokens de autenticacao
- links com token sensivel
- resumo financeiro detalhado

## Fluxo futuro recomendado

1. Usuario ativa WhatsApp no Farol.
2. Backend registra consentimento, telefone verificado e preferencias.
3. Backend gera evento de notificacao sem dado sensivel.
4. Worker envia template oficial aprovado.
5. Logs registram somente `UserId`, tipo de evento, timestamp e provider message id.
6. Usuario acessa o app autenticado para ver detalhes.

## Variaveis futuras

- `WhatsApp__Enabled`
- `WhatsApp__Provider`
- `WhatsApp__AccessToken`
- `WhatsApp__PhoneNumberId`
- `WhatsApp__BusinessAccountId`
- `WhatsApp__WebhookVerifyToken`
- `WhatsApp__WebhookAppSecret`
- `WhatsApp__DefaultLanguage`
- `WhatsApp__TemplateReminder`
- `WhatsApp__TemplateSecurityNotice`

## Criterios de aceite para abrir implementacao

- caso de uso validado com usuarios
- opt-in/opt-out definido
- template sem dado financeiro sensivel
- custo mensal estimado
- politica de logs revisada
- webhook com assinatura validada
- plano de tratamento de erro/retry
- decisao formal de API oficial ou BSP oficial

## Fontes consultadas

- [WhatsApp Business Platform Pricing](https://whatsappbusiness.com/products/platform-pricing): cobranca por mensagem entregue, categorias e variacao por mercado.
- [WPPConnect](https://wppconnect.io/): projeto open source de automacao com WhatsApp Web.
- [WPPConnect GitHub](https://github.com/wppconnect-team/wppconnect): biblioteca exporta funcoes do WhatsApp Web para Node.
- [open-wa GitHub](https://github.com/open-wa): ferramentas open source para automacao de WhatsApp.
- [Evolution API GitHub](https://github.com/EvolutionAPI/evolution-api): API open source com modos Baileys/WhatsApp Web e Cloud API oficial.
