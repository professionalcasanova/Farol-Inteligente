# Farol

Farol e um assistente financeiro pessoal para o mercado brasileiro, construido com foco em simplicidade, clareza e evolucao incremental do backend.

## Estado atual

Sprint 1 concluida com:

- estrutura base da solution em .NET
- dominio inicial
- persistencia com EF Core e PostgreSQL
- migration inicial aplicada
- seed de categorias de sistema
- autenticacao minima com JWT
- testes unitarios para dominio e autenticacao

O projeto ainda nao possui endpoints de contas financeiras, transacoes ou dashboard mensal.

## Stack atual

- .NET 10
- ASP.NET Core Web API
- EF Core
- PostgreSQL
- xUnit
- Docker Compose

## Estrutura

```txt
Farol.sln
src/
  Farol.Api/
  Farol.Domain/
  Farol.Infrastructure/
tests/
  Farol.Tests/
docs/
  sprints/
```

## Rodando localmente

### 1. Subir o banco

```powershell
docker compose up -d
```

### 2. Restaurar e compilar

```powershell
$env:DOTNET_CLI_HOME='c:\Users\masuc\Desktop\PensarNoNome\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
dotnet restore Farol.sln
dotnet build Farol.sln --no-restore -m:1 -v minimal
```

### 3. Aplicar migration

```powershell
& "$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe" database update `
  --project src/Farol.Infrastructure/Farol.Infrastructure.csproj `
  --startup-project src/Farol.Api/Farol.Api.csproj `
  --context FarolDbContext `
  --no-build
```

### 4. Subir a API

```powershell
dotnet run --project src/Farol.Api/Farol.Api.csproj --no-build
```

Swagger:

- `http://localhost:5258/swagger`

### 5. Rodar testes

```powershell
dotnet test tests/Farol.Tests/Farol.Tests.csproj -m:1 -v minimal
```

## Autenticacao atual

Endpoints disponiveis:

- `POST /api/auth/register`
- `POST /api/auth/login`

Resposta atual:

- `accessToken`
- `userId`
- `name`
- `email`

## Banco local

`docker-compose.yml` sobe um PostgreSQL local com:

- database: `farol_dev`
- user: `postgres`
- password: `postgres`
- porta: `5432`

## Documentacao

Fechamento detalhado da Sprint 1:

- [docs/sprints/sprint-1.md](docs/sprints/sprint-1.md)
