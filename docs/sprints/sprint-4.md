# Sprint 4 - Importacao CSV de Transacoes

## Objetivo da sprint

Reduzir a friccao de entrada manual no Farol com um fluxo simples de importacao CSV de transacoes, incluindo categorizacao automatica basica por regras explicitas.

## Entregas concluidas

### Modulo entregue

- `Imports`

### Fluxo entregue

- upload de arquivo CSV de transacoes
- importacao para uma conta financeira do usuario autenticado
- uso de categoria informada no arquivo quando valida
- categorizacao automatica simples por palavra-chave quando a categoria vier vazia
- importacao parcial: linhas invalidas sao ignoradas sem derrubar o arquivo inteiro
- retorno de resumo com total, importadas, ignoradas e erros

## Abordagem implementada

- endpoint `multipart/form-data`
- `financialAccountId` informado no request
- leitura do CSV linha a linha
- formato unico e explicito de arquivo
- validacao de ownership da conta financeira
- resolucao de categorias visiveis ao usuario
- priorizacao de categorias de sistema no matching por nome
- regras simples de categorizacao baseadas em descricao normalizada

Persistencia:

- nenhuma entidade nova foi criada nesta sprint
- nenhuma migration nova foi necessaria
- as transacoes importadas usam a estrutura de persistencia que ja existia no sistema

## Endpoint implementado

- `POST /api/imports/transactions/csv`

Formato minimo esperado:

```csv
occurredOn,description,amount,type,categoryName
2026-03-01,Salario,3000.00,Income,Salario
2026-03-02,Mercado,120.50,Expense,Alimentacao
```

Resposta minima entregue:

- `totalRows`
- `importedRows`
- `skippedRows`
- `errors`

## Regras de categorizacao simples

Regras implementadas:

- `uber`, `99`, `combustivel` -> `Transporte`
- `mercado`, `ifood`, `restaurante` -> `Alimentacao`
- `netflix`, `spotify` -> `Assinaturas`
- `farmacia` -> `Saude`
- `salario`, `pagamento` -> `Salario`

Comportamento:

- se `categoryName` vier preenchido e valido, ele e respeitado
- se `categoryName` vier vazio, o sistema tenta categorizar pela regra
- se nao encontrar regra valida, a transacao fica sem categoria
- se houver incompatibilidade entre tipo e categoria, a linha e ignorada com erro

## Decisoes tecnicas importantes

- mantido o monolito modular atual
- mantido uso direto de `FarolDbContext` no controller
- sem `Application` layer, sem `CQRS` e sem repository generico
- parser CSV simples, sem suporte a multiplos layouts
- sem historico persistido de importacao
- normalizacao simples de texto para matching de descricao e categoria
- linhas invalidas sao registradas no resumo, sem abortar a importacao valida restante

## Testes automatizados existentes nesta sprint

Cobertura adicionada:

- `TransactionCsvImportsEndpointsTests`

Casos cobertos:

- autenticacao obrigatoria no endpoint
- importacao valida cria transacoes
- conta financeira de outro usuario e rejeitada
- linha invalida e ignorada com erro registrado
- `categoryName` valido e respeitado
- categorizacao por regra funciona quando `categoryName` vem vazio
- incompatibilidade entre tipo e categoria e tratada corretamente
- importacao nao mistura dados entre usuarios

Estado validado no fechamento desta sprint:

- `43` testes passando na suite atual

## Como validar manualmente a Sprint 4

### 1. Subir banco, compilar e aplicar schema atual

```powershell
docker compose up -d
$env:DOTNET_CLI_HOME='c:\Users\masuc\Desktop\PensarNoNome\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
dotnet build Farol.sln --no-restore -c Release -m:1 -v minimal
& "$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe" database update `
  --project src/Farol.Infrastructure/Farol.Infrastructure.csproj `
  --startup-project src/Farol.Api/Farol.Api.csproj `
  --context FarolDbContext `
  --configuration Release `
  --no-build
```

### 2. Subir a API

```powershell
dotnet run --project src/Farol.Api/Farol.Api.csproj -c Release --no-build
```

Swagger:

- `http://localhost:5258/swagger`

### 3. Validar fluxo principal

1. registrar usuario em `POST /api/auth/register`
2. fazer login em `POST /api/auth/login`
3. autorizar o token no Swagger
4. criar conta financeira em `POST /api/accounts`
5. preparar um arquivo CSV no formato suportado
6. enviar o arquivo em `POST /api/imports/transactions/csv`
7. consultar `GET /api/transactions` para confirmar a importacao

### 4. Validacoes manuais minimas

- sem token, o endpoint retorna `401`
- conta de outro usuario e rejeitada
- arquivo vazio e rejeitado
- header invalido e rejeitado
- `categoryName` valido e usado na transacao
- `categoryName` vazio com descricao conhecida aciona a categorizacao por regra
- linha invalida aparece em `errors` e nao derruba o restante do arquivo

## O que ficou explicitamente fora da Sprint 4

- multiplos layouts de CSV
- historico persistido de importacao
- desfazer importacao
- matching complexo de categorias
- IA para categorizacao
- modelo treinado
- importacao de bills
- importacao bancaria automatica
- regras customizadas por usuario

## Proximos passos recomendados para a Sprint 5

- evoluir bills e vencimentos
- preparar primeiros alertas baseados em dados importados
- adicionar importacao CSV com mais robustez quando o layout do produto estiver estavel
- considerar regras customizadas de categorizacao por usuario
- ampliar cobertura de testes conforme os proximos fluxos entrarem
