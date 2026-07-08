# Deploy beta fechado do Farol

Este guia sobe o Farol em um formato pragmático para testers:

- `web` no Vercel
- `api`, `farol-intelligence` e PostgreSQL no Render

Objetivo:

- publicar rapido
- manter custo baixo
- evitar acoplamento manual entre servicos no primeiro beta

## Antes de comecar

Voce precisa ter:

- repositorio no GitHub com a branch `master` atualizada
- conta no Vercel
- conta no Render
- acesso para configurar variaveis de ambiente

## Arquivos importantes deste pacote

- [render.yaml](/c:/Users/masuc/Desktop/PensarNoNome/render.yaml)
- [src/Farol.Api/Dockerfile](/c:/Users/masuc/Desktop/PensarNoNome/src/Farol.Api/Dockerfile)
- [services/farol_intelligence/Dockerfile](/c:/Users/masuc/Desktop/PensarNoNome/services/farol_intelligence/Dockerfile)
- [web/.env.production.example](/c:/Users/masuc/Desktop/PensarNoNome/web/.env.production.example)

## Passo 1. Subir backend e banco no Render

### Opcao recomendada: Blueprint

1. No Render, clique em `New +`.
2. Escolha `Blueprint`.
3. Selecione este repositorio.
4. Confirme o arquivo `render.yaml`.
5. Crie os servicos.

Isso vai preparar:

- `farol-postgres`
- `farol-intelligence` como servico privado
- `farol-api`

### Ajustes manuais no Render apos criar o Blueprint

Abra o servico privado `farol-intelligence` e copie a URL interna gerada pelo Render.

Depois abra o servico `farol-api` e configure:

- `FinancialIntelligence__BaseUrl`
  Exemplo: URL interna do `farol-intelligence`, sem exposicao publica
- `Cors__AllowedOrigins__0`
  Exemplo: `https://seu-projeto-web.vercel.app`
- `FAROL_INTERNAL_API_KEY`
  Mesmo valor configurado no `farol-intelligence`

Os outros envs principais ja ficam previstos no `render.yaml`:

- `ConnectionStrings__DefaultConnection`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__SigningKey`
- `Database__MigrateOnStartup`
- `FAROL_ENVIRONMENT=Production` no `farol-intelligence`

`Jwt__SigningKey` deve ser um segredo forte de producao. A API bloqueia startup em
producao se a chave estiver vazia, curta ou contiver termos como `default`, `local`,
`development` ou `change-in-production`.

### Validar o backend

Teste estas URLs no navegador:

- `https://sua-api.onrender.com/health`

A API deve responder JSON com `status = ok`. O `farol-intelligence` nao deve ter URL publica; o `/health` dele exige `X-Internal-API-Key` e deve ser validado pela rede interna.

## Passo 2. Subir o frontend no Vercel

1. No Vercel, clique em `Add New...`.
2. Escolha `Project`.
3. Importe este repositorio.
4. Quando o Vercel pedir o diretório do projeto, selecione `web`.
5. Configure a variavel:

```env
NEXT_PUBLIC_API_BASE_URL=https://sua-api.onrender.com
```

6. Faça o deploy.

Depois copie a URL do Vercel.

### Deploy manual

O arquivo `web/vercel.json` mantém os deploys automáticos por Git desativados. Para publicar uma versão, use `Create Deployment` no painel do Vercel ou execute o Vercel CLI explicitamente.

No Render, os serviços definidos em `render.yaml` usam `autoDeployTrigger: off`. Novas versões devem ser publicadas manualmente no painel do serviço.

## Passo 3. Fechar o circuito entre web e API

Volte no Render, abra o servico `farol-api` e ajuste:

- `Cors__AllowedOrigins__0 = https://sua-url-do-vercel.vercel.app`

Depois redeploye o servico `farol-api`.

## Passo 4. Smoke test de beta

Valide em producao:

1. abrir a pagina de login
2. registrar um usuario
3. criar uma conta
4. registrar uma transacao
5. criar uma conta a pagar
6. criar uma serie recorrente
7. salvar planejamento do mes
8. aplicar planejamento base
9. abrir dashboard
10. validar `month health`, alertas e dinheiro livre

## Passo 5. O que monitorar nos testers

- dificuldade para entender alertas
- falhas de login ou sessao
- lentidao no dashboard
- erro de CORS
- falha do servico de inteligencia
- problemas ao importar CSV

## Observacoes importantes

- este deploy e para beta fechado, nao para producao ampla
- o access token do frontend fica apenas em memoria; o refresh token e enviado por cookie `HttpOnly`
- o Render free pode hibernar servicos sem trafego
- se a API estiver no ar e os insights falharem, confira `FinancialIntelligence__BaseUrl` e `FAROL_INTERNAL_API_KEY`

## Quando usar Railway em vez de Render

Use Railway apenas se voce quiser concentrar todos os servicos no mesmo painel e aceitar um fluxo diferente de custo e trial.

Para este beta, `Vercel + Render` continua o caminho mais simples.
