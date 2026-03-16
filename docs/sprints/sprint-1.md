# Sprint 1 - Fundacao Backend e Autenticacao Minima

## Objetivo da sprint

Entregar a base tecnica inicial do Farol para desenvolvimento local e evolucao do MVP:

- solution e projetos organizados
- dominio inicial
- persistencia com EF Core e PostgreSQL
- migration inicial
- seed de categorias de sistema
- autenticacao minima com JWT
- testes unitarios iniciais

## Entregas concluidas

### Estrutura da solution

- `Farol.sln`
- `src/Farol.Api`
- `src/Farol.Domain`
- `src/Farol.Infrastructure`
- `tests/Farol.Tests`

### Dominio implementado

Entidades atuais:

- `User`
- `FinancialAccount`
- `Category`
- `Transaction`

Enums atuais:

- `TransactionType`
- `FinancialAccountType`
- `CategoryType`

Regras de dominio ja implementadas:

- `User` normaliza email para lowercase
- `FinancialAccount` exige `UserId`, nome valido e nasce ativa
- `Category` diferencia categoria de sistema e categoria do usuario
- `Transaction` exige conta financeira valida
- `Transaction` rejeita `Amount <= 0`
- `Transaction` valida compatibilidade entre tipo da transacao e tipo da categoria

### Persistencia implementada

- `FarolDbContext` com `DbSet` para `User`, `FinancialAccount`, `Category` e `Transaction`
- configuracoes Fluent API por entidade
- relacionamento minimo entre entidades
- indices iniciais para consultas e integridade basica
- `FarolDbContextFactory` para design-time do EF Core

### Banco e migration

Migration atual:

- `InitialCore`

Tabelas criadas:

- `users`
- `financial_accounts`
- `categories`
- `transactions`
- `__EFMigrationsHistory`

### Seed implementado

Estrutura:

- `CategorySeed`
- `DatabaseSeeder`

Categorias de sistema atuais:

Despesas:

- Moradia
- Alimentacao
- Transporte
- Saude
- Educacao
- Lazer
- Assinaturas
- Outros
- Sem categoria

Receitas:

- Salario
- Freelance
- Transferencia recebida
- Outros
- Sem categoria

### Autenticacao implementada

Servicos:

- `PasswordService`
- `JwtTokenService`

Endpoints:

- `POST /api/auth/register`
- `POST /api/auth/login`

Comportamento atual:

- registro cria usuario com email normalizado
- email duplicado retorna conflito
- login valida email e senha
- resposta retorna `accessToken`, `userId`, `name` e `email`
- JWT inclui claims `sub` e `email`

### Testes automatizados existentes

Projeto:

- `tests/Farol.Tests`

Cobertura atual:

- `PasswordService`
- `JwtTokenService`
- `User`
- `FinancialAccount`
- `Category`
- `Transaction`

Total validado no fechamento desta sprint:

- 14 testes unitarios passando

## Decisoes tecnicas importantes

- arquitetura inicial em monolito modular
- backend-first em ASP.NET Core
- sem `Application` layer nesta sprint
- sem `CQRS`
- sem repository generico
- sem ASP.NET Identity completo
- persistencia direta com EF Core
- PostgreSQL local via Docker
- autenticacao simples com JWT e senha com `PasswordHasher<User>`
- seed disparado no startup da API, com verificacao defensiva de conectividade e existencia da tabela

## Estrutura atual do projeto

```txt
Farol.sln
src/
  Farol.Api/
    Modules/
      Auth/
    Program.cs
    appsettings.json
    appsettings.Development.json
  Farol.Domain/
    Users/
    Ledger/
    Categories/
  Farol.Infrastructure/
    Auth/
    Persistence/
      Configurations/
      Migrations/
    Seeding/
tests/
  Farol.Tests/
    Auth/
    Domain/
docs/
  sprints/
    sprint-1.md
```

## Banco e migrations

Ambiente local:

- PostgreSQL em Docker
- container `farol-postgres`
- banco `farol_dev`

Arquivo de infraestrutura local:

- `docker-compose.yml`

Connection string de desenvolvimento:

```text
Host=localhost;Port=5432;Database=farol_dev;Username=postgres;Password=postgres
```

Comandos usados:

```powershell
docker compose up -d
dotnet build Farol.sln --no-restore -m:1 -v minimal
& "$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe" database update `
  --project src/Farol.Infrastructure/Farol.Infrastructure.csproj `
  --startup-project src/Farol.Api/Farol.Api.csproj `
  --context FarolDbContext `
  --no-build
```

## Como rodar o projeto localmente

### 1. Subir o PostgreSQL

```powershell
docker compose up -d
```

### 2. Restaurar dependencias

```powershell
$env:DOTNET_CLI_HOME='c:\Users\masuc\Desktop\PensarNoNome\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
dotnet restore Farol.sln
```

### 3. Compilar

```powershell
dotnet build Farol.sln --no-restore -m:1 -v minimal
```

### 4. Aplicar migration

```powershell
& "$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe" database update `
  --project src/Farol.Infrastructure/Farol.Infrastructure.csproj `
  --startup-project src/Farol.Api/Farol.Api.csproj `
  --context FarolDbContext `
  --no-build
```

### 5. Subir a API

```powershell
dotnet run --project src/Farol.Api/Farol.Api.csproj --no-build
```

Por padrao local:

- `http://localhost:5258`
- Swagger em `http://localhost:5258/swagger`

### 6. Rodar os testes

```powershell
dotnet test tests/Farol.Tests/Farol.Tests.csproj -m:1 -v minimal
```

## Como validar a Sprint 1 manualmente

### Banco

Validar tabelas:

```powershell
docker exec farol-postgres psql -U postgres -d farol_dev -c "\dt"
```

Resultado esperado:

- `users`
- `financial_accounts`
- `categories`
- `transactions`
- `__EFMigrationsHistory`

### Seed

Validar seed das categorias:

```powershell
docker exec farol-postgres psql -U postgres -d farol_dev -c "SELECT COUNT(*) AS total FROM categories;"
```

Resultado esperado:

- `14`

### Autenticacao

No Swagger:

1. chamar `POST /api/auth/register`
2. registrar um usuario novo
3. confirmar retorno com `accessToken`
4. chamar `POST /api/auth/login`
5. confirmar retorno com novo `accessToken`

Validacoes manuais minimas:

- email duplicado retorna conflito
- senha incorreta retorna `401`
- email e retornado normalizado

## O que ficou explicitamente fora da Sprint 1

- endpoints de contas financeiras
- endpoints de transacoes
- endpoint de categorias
- dashboard mensal
- monthly summary
- bills
- recorrencia
- importacao CSV
- alertas
- refresh token
- confirmacao de email
- recuperacao de senha
- integracoes bancarias
- assistente em linguagem natural
- testes de integracao

## Proximos passos para Sprint 2

Foco recomendado para a Sprint 2:

- endpoints de `FinancialAccount`
- endpoints de `Transaction`
- listagem de categorias
- primeiras validacoes de ownership na API
- resumo mensal simples
- testes adicionais para os novos fluxos

Sprint 2 ainda nao foi iniciada neste documento.
