# Farol Web

Front-end MVP do Farol para a Sprint 6.

Stack:

- Next.js
- TypeScript
- Tailwind CSS

## Requisitos

- Node.js 22+
- backend do Farol rodando localmente

## Configuracao

Crie um arquivo `.env.local` com:

```env
NEXT_PUBLIC_API_BASE_URL=http://localhost:5258
```

Existe um exemplo em [`.env.local.example`](./.env.local.example).

## Rodando localmente

1. Suba o backend do Farol na raiz do repositorio:

```powershell
docker compose up -d
$env:DOTNET_CLI_HOME='c:\Users\masuc\Desktop\PensarNoNome\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
dotnet build Farol.sln --no-restore -c Release -m:1 -v minimal
dotnet run --project src/Farol.Api/Farol.Api.csproj -c Release --no-build
```

2. Em outro terminal, dentro de `web`:

```powershell
npm install
npm run dev
```

3. Abra:

- `http://localhost:3000`

## Comandos uteis

```powershell
npm run dev
npm run lint
npm run build
```

## Fluxos cobertos no MVP

- login com `POST /api/auth/login`
- dashboard com resumo mensal, orcamento e dinheiro livre
- listagem e criacao de transacoes
- visualizacao e substituicao do orcamento mensal
- importacao CSV de transacoes
