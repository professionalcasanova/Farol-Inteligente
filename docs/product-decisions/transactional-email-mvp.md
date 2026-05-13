# Emails transacionais no MVP

## Status

Decisao tomada: usar Mailpit em desenvolvimento e homologacao tecnica, e usar um
provider transacional gerenciado em producao MVP.

Provider recomendado para iniciar: Resend.

Brevo fica como alternativa valida se o produto precisar concentrar email transacional,
campanhas simples e CRM no mesmo fornecedor. Postal nao deve ser usado em producao no
MVP sem uma decisao explicita de operacao de email.

## Problema

O Farol precisa enviar emails sensiveis ligados a autenticacao:

- confirmacao de cadastro
- recuperacao de senha

Esses fluxos nao podem depender de token em log, token retornado em resposta publica ou
exposicao de detalhes sobre existencia de email.

## Alternativas avaliadas

### 1. Mailpit

Decisao: usar em desenvolvimento e homologacao tecnica.

Pros:

- captura emails localmente sem envio real
- tem SMTP, UI web e API para testes automatizados
- reduz risco de disparo acidental para usuarios reais
- simples para Docker/local

Contras:

- nao valida entregabilidade real em caixas de email externas
- nao resolve reputacao, bounce, SPF, DKIM ou DMARC de producao
- se exposto em homologacao, pode vazar conteudo de emails sensiveis

Uso recomendado:

- desenvolvimento local
- testes automatizados de template/link
- homologacao fechada e protegida por rede/autenticacao

### 2. Postal self-hosted

Decisao: nao usar em producao no MVP.

Pros:

- open source
- oferece UI, logs, SMTP/API, webhooks e controle operacional
- pode fazer sentido em escala ou por requisito forte de soberania

Contras:

- exige manutencao de servidor de email
- exige DNS correto: SPF, DKIM, DMARC, MX, return-path e reverse DNS
- exige cuidado com reputacao de IP, blacklist, warm-up e monitoramento
- aumenta superficie operacional para um produto que ainda esta em MVP

Uso recomendado:

- apenas em estudo posterior, se houver motivo operacional forte
- nao como padrao inicial de producao

### 3. Resend

Decisao: provider recomendado para producao MVP.

Pros:

- foco claro em emails transacionais
- API/SMTP simples para integrar no backend
- suporta dominio proprio e autenticacao DNS
- plano inicial baixo e suficiente para MVP pequeno

Contras:

- fornecedor externo para um fluxo critico
- limites do plano gratuito podem bloquear volume ou testes amplos
- requer configuracao de dominio e chaves em ambiente seguro

Uso recomendado:

- producao MVP
- homologacao com usuarios reais, se for necessario entregar emails fora do Mailpit

### 4. Brevo

Decisao: alternativa de producao, nao primeira escolha tecnica.

Pros:

- plano gratuito com limite diario maior
- combina transacional, campanhas e recursos comerciais
- boa opcao se o Farol quiser centralizar comunicacao em uma suite mais ampla

Contras:

- produto mais amplo que o necessario para o primeiro caso transacional
- pode trazer complexidade de configuracao/operacao alem do reset e confirmacao
- menos enxuto para um backend que precisa apenas enviar eventos de autenticacao

Uso recomendado:

- alternativa se Resend nao atender custo, limite ou preferencia operacional

## Decisao tecnica

Criar uma abstracao simples de envio no backend quando a implementacao entrar:

- `IEmailSender`
- implementacao SMTP para Mailpit
- implementacao provider para producao
- templates versionados para `EmailConfirmation` e `PasswordReset`

Nao implementar envio direto no controller. Controllers devem apenas disparar o caso de
uso, e o caso de uso decide se deve enviar email.

## Fluxo proposto

### Desenvolvimento

1. Backend gera token opaco de uso unico.
2. Token e persistido de forma segura com expiracao curta.
3. Backend envia email via SMTP para Mailpit.
4. Desenvolvedor abre a UI do Mailpit e valida o link.
5. Nenhum token aparece em log ou resposta HTTP publica.

### Homologacao tecnica

1. Usar Mailpit em ambiente fechado.
2. Proteger UI do Mailpit por rede interna ou autenticacao.
3. Usar dados de teste e emails nao reais sempre que possivel.
4. Validar conteudo, link e expiracao sem disparo externo.

Se a homologacao envolver usuarios reais, usar o mesmo provider de producao com allowlist
ou dominio de homologacao. Mailpit nao valida entrega real.

### Producao MVP

1. Usar provider transacional gerenciado.
2. Configurar dominio proprio com SPF, DKIM e DMARC.
3. Enviar apenas link com token opaco.
4. Registrar logs somente com `UserId`, timestamp, tipo de evento e provider message id.
5. Tratar falha de envio sem revelar se o email existe.

## Regras de seguranca

- nunca logar token de confirmacao ou reset
- nunca logar senha
- evitar email completo em log; preferir `UserId`
- nao retornar token em resposta publica
- resposta de esqueci senha deve continuar generica
- token deve ser opaco, de uso unico e com expiracao curta
- reset de senha recomendado: 15 minutos
- confirmacao de cadastro recomendado: 24 horas
- em producao, exigir HTTPS no link publico
- nao aceitar link de callback vindo livremente do request sem allowlist de origem

## Variaveis de ambiente

Comuns:

- `Email__Mode`: `Smtp` ou `Provider`
- `Email__FromAddress`
- `Email__FromName`
- `Email__PublicBaseUrl`

Mailpit/dev/homologacao tecnica:

- `Email__Smtp__Host`
- `Email__Smtp__Port`
- `Email__Smtp__UseTls`
- `Email__Smtp__Username`
- `Email__Smtp__Password`

Provider/producao:

- `Email__Provider`
- `Email__ProviderApiKey`
- `Email__ProviderWebhookSecret`

Operacao:

- `Email__ResetTokenMinutes`
- `Email__ConfirmationTokenHours`

## Quick wins antes da implementacao

- adicionar Mailpit ao `docker-compose.yml`
- criar contrato `IEmailSender`
- mover montagem de link para servico do backend
- adicionar testes garantindo que reset/confirmacao nao retornam token
- adicionar testes garantindo que logs nao contem token, senha ou email completo

## Fontes consultadas

- [Mailpit](https://mailpit.axllent.org/): SMTP local, UI web e API para testes.
- [Postal](https://docs.postalserver.io/): plataforma open source de entrega de email.
- [Postal FAQ](https://docs.postalserver.io/welcome/faqs/): riscos de entregabilidade, DNS e reputacao.
- [Resend Pricing](https://resend.com/pricing): plano inicial e limites atuais.
- [Resend Transactional Emails](https://resend.com/products/transactional-emails): API/SMTP para emails transacionais.
- [Brevo Pricing](https://help.brevo.com/hc/en-us/articles/208589409): plano gratuito e recursos transacionais.
