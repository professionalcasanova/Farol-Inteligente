# Farol Web

Frontend do Farol em Next.js 15 com App Router.

Este workspace e independente no sentido de instalacao e execucao, mas depende da API do Farol para os fluxos reais de autenticacao e dados.

## O que existe em `web/`

- aplicacao Next.js
- telas do MVP
- integracao HTTP com a API do backend
- testes de interface com Vitest

## Stack

- Next.js 15
- React 19
- TypeScript
- Tailwind CSS 4
- Vitest + Testing Library

## Requisitos

- Node.js 22+
- npm
- API do Farol acessivel pela URL configurada em `NEXT_PUBLIC_API_BASE_URL`

## Variaveis de ambiente

### Desenvolvimento local

Crie `web/.env.local`:

```env
NEXT_PUBLIC_API_BASE_URL=http://localhost:5258
```

Exemplo disponivel em [`./.env.local.example`](./.env.local.example).

### Deploy

Exemplo para ambiente publicado:

```env
NEXT_PUBLIC_API_BASE_URL=https://replace-with-farol-api.onrender.com
```

Exemplo disponivel em [`./.env.production.example`](./.env.production.example).

## Como rodar o frontend standalone

O frontend pode ser iniciado sem iniciar o backend no mesmo terminal, mas a navegacao com dados reais depende da API configurada.

No diretorio `web/`:

```powershell
npm install
npm run dev
```

Aplicacao:

- `http://localhost:3000`

## Integracao com a API

O frontend usa `NEXT_PUBLIC_API_BASE_URL` como base para chamadas HTTP.

Comportamento atual:

- remove barra final automaticamente
- usa `http://localhost:5258` como fallback local se a variavel nao estiver definida
- consome endpoints HTTP do backend, nao o servico Python diretamente

Arquivo de referencia:

- [`lib/api.ts`](./lib/api.ts)

## Contrato esperado da API

Os principais grupos de endpoints consumidos pelo frontend hoje sao:

- `POST /api/auth/login`
- `POST /api/auth/register`
- `GET/POST/PUT /api/accounts`
- `GET /api/categories`
- `GET/POST/PUT/DELETE /api/transactions`
- `GET /api/transactions/history`
- `GET /api/dashboard/monthly-summary`
- `GET /api/dashboard/bills-summary`
- `GET/POST /api/budgets/monthly`
- `GET/POST /api/budgets/template`
- `POST /api/budgets/template/apply`
- `POST /api/imports/transactions/csv`
- `GET/POST/PUT/DELETE/PATCH /api/bills`
- `GET /api/insights/free-money`
- `GET /api/insights/alerts`
- `GET /api/insights/month-health`

## Fluxo recomendado para desenvolvimento local

1. Na raiz do repositorio, suba o banco:

```powershell
docker compose up -d
```

2. Na raiz do repositorio, rode a API .NET:

```powershell
$env:DOTNET_CLI_HOME='c:\Users\masuc\Desktop\PensarNoNome\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
dotnet restore Farol.sln
dotnet build Farol.sln --no-restore -c Release -m:1 -v minimal
dotnet run --project src/Farol.Api/Farol.Api.csproj -c Release --no-build
```

3. Em outro terminal, dentro de `web/`, rode o frontend:

```powershell
npm install
npm run dev
```

Opcional:

4. Para fluxos de saude financeira, rode tambem o servico Python em `http://127.0.0.1:8000`.

## Scripts disponiveis

```powershell
npm run dev
npm run lint
npm run test
npm run build
npm run start
npm run test:watch
```

## Limites deste workspace

- este diretorio nao contem regras de negocio do backend
- este diretorio nao substitui o servico Python
- alteracoes em `web/` nao devem mexer em `src/` ou `services/` sem necessidade explicita de contrato

## Pontos de atencao

- a autenticacao mantem o access token apenas em memoria no frontend
- o refresh token e enviado pela API em cookie `HttpOnly`, `Secure` e `SameSite=Lax`
- o backend local aceita hoje `http://localhost:3000` e `http://localhost:3001` no CORS
- nao existe modo de mocks em runtime para navegacao manual; os mocks atuais existem apenas nos testes
- se a API nao estiver acessivel, o frontend sobe, mas chamadas de dados falham
