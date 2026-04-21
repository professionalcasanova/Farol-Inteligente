# Farol Web

Frontend standalone do Farol, pronto para rodar como repositorio independente.

## Stack

- Next.js 15
- React 19
- TypeScript
- Tailwind CSS 4
- Vitest + Testing Library

## Requisitos

- Node.js 22+
- npm 10+
- uma API Farol acessivel por HTTP

## Configuracao

Crie um arquivo `.env.local` a partir de [`.env.local.example`](./.env.local.example):

```env
NEXT_PUBLIC_API_BASE_URL=http://localhost:5258
```

`NEXT_PUBLIC_API_BASE_URL` deve apontar para a API HTTP do Farol. O frontend nao depende do codigo do backend nem do servico Python para compilar ou iniciar, mas depende de uma API acessivel para executar os fluxos da aplicacao.

## Rodando localmente

```powershell
npm install
Copy-Item .env.local.example .env.local
npm run dev
```

Abra:

- `http://localhost:3000`

## Scripts

```powershell
npm run dev
npm run build
npm run start
npm run test
npm run test:watch
```

## Contrato esperado da API

Este frontend consome uma API externa via `fetch` usando `NEXT_PUBLIC_API_BASE_URL` como base. Os principais grupos de endpoints esperados sao:

- `POST /api/auth/login`
- `POST /api/auth/register`
- `GET/POST/PUT /api/accounts`
- `GET/POST/PUT/DELETE /api/transactions`
- `GET /api/transactions/history`
- `GET /api/categories`
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

## Observacoes

- A autenticacao do frontend usa `localStorage` como decisao temporaria de MVP.
- Nao existe modo de mocks em runtime para navegacao manual; os mocks atuais existem apenas nos testes.
- Se a API estiver no ar, `npm install` + `npm run dev` sao suficientes para subir o frontend de forma independente.
