# Sprint 6 - Front-end MVP Web

## Objetivo da sprint

Entregar o primeiro front-end do Farol com valor real de demonstracao, consumindo a API existente e cobrindo os fluxos principais do MVP sem exagero de arquitetura.

## Entregas concluidas

### Modulo entregue

- `web`

### Fluxos entregues

- login web com `POST /api/auth/login`
- protecao minima das rotas internas
- dashboard com resumo mensal, orcamento e dinheiro livre
- listagem e criacao de transacoes
- visualizacao e substituicao do orcamento mensal
- importacao CSV de transacoes com resumo da importacao

## Abordagem implementada

- aplicacao separada em `web/`
- stack com `Next.js`, `TypeScript` e `Tailwind CSS`
- consumo direto da API existente do Farol
- sem estado global sofisticado
- sem design system complexo
- sessao persistida em `localStorage` apenas para MVP local
- tratamento minimo de `401` com limpeza da sessao e redirecionamento para `/login`
- estados de carregamento e erro ajustados para evitar telas quebradas ou vazias em falhas de carregamento

## Autenticacao e navegacao

Implementacao atual:

- tela de login em `/login`
- home `/` redireciona para `/dashboard` ou `/login`
- paginas internas usam protecao por hook client-side
- logout remove a sessao local e redireciona corretamente

Decisao temporaria de MVP:

- o `accessToken` fica em `localStorage`
- essa escolha foi mantida apenas para demo local e simplicidade da sprint
- nao existe refresh token nesta etapa

## Integracao com o backend

Endpoints consumidos no front:

- `POST /api/auth/login`
- `GET /api/accounts`
- `POST /api/accounts`
- `GET /api/categories`
- `GET /api/transactions`
- `POST /api/transactions`
- `GET /api/dashboard/monthly-summary`
- `GET /api/insights/free-money`
- `GET /api/budgets/monthly`
- `POST /api/budgets/monthly`
- `POST /api/imports/transactions/csv`

## Decisoes tecnicas importantes

- mantido o monolito modular atual
- mantido uso direto de `FarolDbContext` no backend
- CORS restrito para `http://localhost:3000` e `http://localhost:3001`
- cliente HTTP web configurado sem cache para evitar leitura stale na demo
- sessao local saneada antes do uso para evitar JSON invalido ou incompleto no `localStorage`
- sem SSR autenticado e sem cookies nesta sprint

## Qualidade validada no fechamento

Validacoes executadas:

- `npm run lint`
- `npm run build`
- `dotnet build Farol.sln`
- `dotnet test tests/Farol.Tests/Farol.Tests.csproj`

Estado validado no fechamento:

- build do front passando
- build do backend passando
- `49` testes backend passando na suite atual

## Como validar manualmente a Sprint 6

### 1. Subir banco e backend

```powershell
docker compose up -d
$env:DOTNET_CLI_HOME='c:\Users\masuc\Desktop\PensarNoNome\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
dotnet build Farol.sln --no-restore -c Release -m:1 -v minimal
dotnet run --project src/Farol.Api/Farol.Api.csproj -c Release --no-build
```

### 2. Subir o front

Crie `web/.env.local`:

```env
NEXT_PUBLIC_API_BASE_URL=http://localhost:5258
```

Depois:

```powershell
Set-Location web
npm install
npm run dev
```

Aplicacao:

- `http://localhost:3000`

### 3. Validar fluxo principal

1. acessar `/`
2. confirmar redirecionamento para `/login` quando nao houver sessao
3. fazer login com um usuario existente
4. confirmar redirecionamento para `/dashboard`
5. navegar por `Dashboard`, `Transacoes`, `Orcamento` e `Importar CSV`
6. criar uma transacao e validar atualizacao no dashboard
7. salvar um orcamento mensal e validar os totais
8. importar um CSV e conferir o resumo retornado
9. clicar em `Sair` e confirmar limpeza da sessao local

### 4. Validacoes manuais minimas

- paginas protegidas nao quebram sem token
- sessao invalida em `localStorage` nao deve manter o usuario autenticado
- falha de carregamento mostra erro com opcao de nova tentativa
- logout remove a sessao e retorna para `/login`
- CORS permite apenas `localhost:3000` e `localhost:3001`

## O que ficou explicitamente fora da Sprint 6

- refresh token
- autenticacao por cookie
- cadastro web completo
- recuperacao de senha
- edicao de transacoes no front
- exclusao de transacoes no front
- design system dedicado
- testes automatizados do front
- deploy em ambiente remoto

## Proximos passos recomendados para a Sprint 7

- evoluir bills e vencimentos no produto
- adicionar alertas iniciais no front
- melhorar feedbacks de formularios com validacao client-side minima
- considerar uma estrategia de sessao mais robusta quando sair do modo demo local
