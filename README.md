# Farol

Farol e um assistente financeiro pessoal para usuarios brasileiros.

O repositorio esta organizado como um monorepo com quatro partes bem separadas:

- `web/`: frontend em Next.js/React para a experiencia do usuario
- `src/`: backend .NET com API HTTP e regras de dominio
- `services/farol_intelligence/`: servico Python para analise financeira deterministica
- `docker-compose.yml`: infraestrutura local do PostgreSQL para desenvolvimento

Este README descreve o estado atual do projeto e como trabalhar em cada parte sem misturar responsabilidades.

## Estado atual

O projeto cobre hoje o MVP local de demonstracao com:

- autenticacao basica
- contas financeiras
- categorias e transacoes
- resumo mensal
- orcamento mensal e template base
- importacao CSV
- contas a pagar
- alertas e leitura de saude financeira do mes
- frontend web consumindo a API
- servico Python responsavel pela analise deterministica

## Estrutura do repositorio

```txt
Farol.sln
.github/
docs/
scripts/
services/
  farol_intelligence/
src/
  Farol.Api/
  Farol.Domain/
  Farol.Infrastructure/
tests/
  Farol.Tests/
web/
docker-compose.yml
render.yaml
```

## Separacao entre frontend, backend e servicos

- O frontend em `web/` pode ser executado isoladamente como workspace proprio.
- O backend em `src/` e os testes em `tests/` formam a aplicacao .NET.
- O servico Python em `services/farol_intelligence/` e independente do frontend e exposto por HTTP.
- O banco local e provisionado via `docker compose`.

Importante:

- frontend nao deve conter regra de dominio do backend
- backend nao deve incorporar detalhes de UI do frontend
- servico Python nao deve assumir responsabilidade de API web ou tela
- integracao entre stacks deve acontecer por contrato HTTP e configuracao

## Requisitos locais

### Backend e banco

- .NET 10 SDK
- Docker Desktop ou compatível com `docker compose`
- PostgreSQL local via Compose

### Frontend

- Node.js 22+
- npm

### Servico Python

- Python 3.11+

## Variaveis de ambiente principais

### Frontend

Arquivo local: `web/.env.local`

```env
NEXT_PUBLIC_API_BASE_URL=http://localhost:5258
```

Arquivos de exemplo:

- [`web/.env.local.example`](web/.env.local.example)
- [`web/.env.production.example`](web/.env.production.example)

### Backend

Nao existe um `.env` obrigatorio para o fluxo local padrao. A configuracao base fica em:

- [`src/Farol.Api/appsettings.json`](src/Farol.Api/appsettings.json)
- [`src/Farol.Api/appsettings.Development.json`](src/Farol.Api/appsettings.Development.json)

Pontos principais:

- `ConnectionStrings:DefaultConnection`
- `Jwt:*`
- `Cors:AllowedOrigins`
- `FinancialIntelligence:*`
- `Database:MigrateOnStartup`

Valores locais atuais:

- API HTTP local: `http://localhost:5258`
- PostgreSQL local: `localhost:5432`
- servico Python local: `http://127.0.0.1:8000`

### Servico Python

Hoje o servico usa apenas configuracao empacotada no projeto para o fluxo local basico.
Nao ha variavel de ambiente obrigatoria documentada para subir o servico localmente.

## Como rodar o frontend isoladamente

O frontend pode ser iniciado sozinho, mas as telas reais dependem da API configurada em `NEXT_PUBLIC_API_BASE_URL`.

1. Configure o arquivo `web/.env.local`

```env
NEXT_PUBLIC_API_BASE_URL=http://localhost:5258
```

2. Instale dependencias

```powershell
Set-Location web
npm install
```

3. Rode o frontend

```powershell
npm run dev
```

Aplicacao web:

- `http://localhost:3000`

Comandos uteis do frontend:

```powershell
npm run dev
npm run lint
npm run test
npm run build
```

Observacao:

- o frontend sobe sozinho, mas login, dashboard e fluxos de dados dependem da API estar disponivel

## Como rodar backend e infraestrutura

### 1. Subir o banco

```powershell
docker compose up -d
```

Banco local atual:

- database: `farol_dev`
- user: `postgres`
- password: `postgres`
- porta: `5432`

### 2. Restaurar e compilar a solution

```powershell
$env:DOTNET_CLI_HOME='c:\Users\masuc\Desktop\PensarNoNome\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
dotnet restore Farol.sln
dotnet build Farol.sln --no-restore -c Release -m:1 -v minimal
```

### 3. Aplicar migrations manualmente, se necessario

Em desenvolvimento, `appsettings.Development.json` esta com `Database:MigrateOnStartup=true`, mas o comando abaixo continua sendo o caminho explicito e seguro quando for necessario controlar a atualizacao:

```powershell
& "$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe" database update `
  --project src/Farol.Infrastructure/Farol.Infrastructure.csproj `
  --startup-project src/Farol.Api `
  --context FarolDbContext `
  --no-build
```

### 4. Rodar a API

```powershell
dotnet run --project src/Farol.Api/Farol.Api.csproj -c Release --no-build
```

Endpoints uteis:

- Swagger: `http://localhost:5258/swagger`
- Health: `http://localhost:5258/health`

### 5. Rodar testes do backend

```powershell
dotnet test tests/Farol.Tests/Farol.Tests.csproj --no-build -c Release -m:1 -v minimal
```

## Como rodar o servico Python

No diretorio `services/farol_intelligence`:

```powershell
python -m venv .venv
.venv\Scripts\Activate.ps1
python -m pip install -e .
python -m uvicorn app.main:app --reload --port 8000
```

Endpoints uteis:

- Health: `http://127.0.0.1:8000/health`
- Analise: `POST http://127.0.0.1:8000/analyze/v1`

Para rodar testes:

```powershell
python -m unittest discover tests
```

## Fluxo basico de desenvolvimento

### Quando a tarefa for de frontend

1. Trabalhe em `web/`
2. Aponte `NEXT_PUBLIC_API_BASE_URL` para a API correta
3. Rode `npm run lint`, `npm run test` e `npm run build`
4. Nao altere backend ou servico Python sem necessidade explicita

### Quando a tarefa for de backend

1. Trabalhe em `src/` e `tests/`
2. Suba o banco local
3. Rode restore, build e testes .NET
4. Nao altere `web/` nem o servico Python sem contrato explicito

### Quando a tarefa for do servico Python

1. Trabalhe em `services/farol_intelligence/`
2. Valide o contrato HTTP consumido pelo backend
3. Rode os testes Python existentes
4. Nao altere frontend ou backend sem motivo de integracao formal

## Fluxo recomendado para rodar tudo localmente

1. `docker compose up -d`
2. Subir a API .NET em `http://localhost:5258`
3. Subir o servico Python em `http://127.0.0.1:8000`
4. Subir o frontend em `http://localhost:3000`

## Documentacao adicional

- [`agents.md`](agents.md): governanca e escopo dos agentes
- [`web/README.md`](web/README.md): documentacao do frontend
- [`services/farol_intelligence/README.md`](services/farol_intelligence/README.md): documentacao do servico Python
- [`docs/demo-scenarios.md`](docs/demo-scenarios.md): usuarios de demonstracao local
- [`docs/deployment/vercel-render-beta.md`](docs/deployment/vercel-render-beta.md): deploy beta
- [`docs/product-decisions/issue-37-payment-trail-modeling.md`](docs/product-decisions/issue-37-payment-trail-modeling.md)
- [`docs/product-decisions/issue-38-natural-language-assistant-phase-1.md`](docs/product-decisions/issue-38-natural-language-assistant-phase-1.md)

## Observacoes importantes

- o frontend usa `localStorage` apenas como decisao temporaria de MVP
- nao existe refresh token neste estagio
- o backend local aceita `http://localhost:3000` e `http://localhost:3001` no CORS atual
- o servico Python e consumido pelo backend, nao diretamente pelo navegador
