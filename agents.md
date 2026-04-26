# AGENTS.md

Este arquivo e a fonte de verdade para agentes que atuam no repositorio Farol.

O objetivo e reduzir mudancas fora de escopo, manter a separacao entre stacks e preservar previsibilidade nas entregas.

## Contexto do produto

Farol e um assistente financeiro pessoal para usuarios brasileiros.

Escopo atual do MVP:

- autenticacao
- contas financeiras
- categorias e transacoes
- resumo mensal
- orcamento
- importacao CSV
- contas a pagar
- alertas e saude financeira
- frontend web em Next.js
- servico Python de inteligencia financeira

Fora de escopo por enquanto:

- integracoes bancarias reais
- automacao de pagamentos
- negociacao com terceiros
- credit scoring
- investimentos complexos

## Mapa de responsabilidade por stack

### Frontend agent

Escopo:

- pasta `web/`
- documentacao diretamente ligada ao frontend
- contratos consumidos no frontend apenas quando o endpoint ja existe

Nao pode:

- alterar `src/`
- alterar `tests/` do backend
- alterar `services/farol_intelligence/`
- alterar infraestrutura do backend sem pedido explicito

Responsabilidade principal:

- experiencia do usuario
- integracao com API ja existente
- estados de tela
- validacao visual e de navegacao
- documentacao de uso do frontend

### Backend agent

Escopo:

- `src/`
- `tests/Farol.Tests/`
- configuracoes e scripts diretamente ligados ao backend .NET

Nao pode:

- alterar `web/`
- alterar `services/farol_intelligence/` sem mudanca formal de contrato
- mexer em deploy de frontend

Responsabilidade principal:

- dominio
- API HTTP
- persistencia
- autenticacao
- testes de regra de negocio e API

### Python agent

Escopo:

- `services/farol_intelligence/`
- documentacao diretamente ligada ao servico Python

Nao pode:

- alterar `web/`
- alterar `src/`
- alterar testes do backend

Responsabilidade principal:

- analise financeira deterministica
- contrato do servico Python
- testes do servico

### DevOps agent

Escopo:

- `.github/workflows/`
- `docker-compose.yml`
- `render.yaml`
- scripts e documentacao operacional
- `.gitignore`

Nao pode:

- alterar regra de negocio
- implementar feature de produto
- misturar responsabilidades entre frontend, backend e Python

Responsabilidade principal:

- CI
- infraestrutura local
- deploy
- padronizacao operacional
- higiene do repositorio

## Regra central de separacao

- frontend nao mexe em backend por conveniencia
- backend nao mexe em frontend por conveniencia
- servico Python nao mexe nas outras stacks
- DevOps nao altera comportamento funcional da aplicacao

Se uma tarefa exigir mudanca em mais de uma stack, o agente deve:

1. explicitar a dependencia entre as stacks
2. limitar a mudanca ao menor contrato possivel
3. evitar refatoracao ampla
4. separar o que e mudanca funcional do que e ajuste de infraestrutura/documentacao

## Regras obrigatorias de execucao

Todo agente deve:

1. analisar antes de alterar
2. entender o escopo real da tarefa
3. fazer mudancas minimas e locais
4. evitar refatoracoes globais sem pedido explicito
5. preservar convencoes existentes da stack tocada
6. validar o impacto antes de concluir

Se houver duvida sobre remover, renomear ou mover algo:

- nao remover
- nao renomear
- nao mover
- registrar o ponto como observacao

## Test-Driven Development (TDD)

TDD e obrigatorio para todo trabalho no backend.

### Regra principal

- todo novo codigo deve ser precedido por teste
- nenhuma feature deve ser implementada sem teste cobrindo o comportamento esperado

### Ciclo obrigatorio

- Red: criar primeiro o teste que falha
- Green: implementar o minimo necessario para fazer o teste passar
- Refactor: melhorar o codigo sem quebrar os testes existentes

### Regras praticas

- testes devem validar comportamento observado, nao implementacao interna
- evitar mocks desnecessarios
- priorizar testes simples, diretos e com leitura rapida
- cada endpoint novo deve ter ao menos 1 teste de sucesso e 1 teste de falha
- testes devem cobrir regras de negocio criticas antes de refatoracoes
- nomes de testes devem seguir o formato `Metodo_Condicao_ResultadoEsperado`

### Proibicoes

- nao escrever codigo de producao sem teste correspondente
- nao ignorar testes quebrando
- nao comentar testes para "passar build"

### Criterio de aceite

Uma task backend so e considerada concluida se:

- testes foram criados ou ajustados antes da consolidacao final do codigo
- testes estao passando no escopo da mudanca
- codigo foi revisado com base nos cenarios cobertos pelos testes

## Politica de alteracao por escopo

### Mudancas permitidas

- ajustes locais no escopo da stack
- atualizacao de documentacao correspondente
- testes da propria stack
- configuracao estritamente ligada ao escopo tocado

### Mudancas que exigem justificativa explicita

- alterar contrato entre frontend e backend
- alterar contrato entre backend e servico Python
- mexer em arquivos compartilhados
- mudar fluxo de CI
- mudar configuracao de deploy

### Mudancas proibidas sem pedido explicito

- refatoracao transversal do repositorio
- mover diretorios
- renomear modulos
- reestruturar arquitetura
- alterar regra de negocio fora da stack da tarefa

## Arquivos compartilhados que exigem cuidado extra

Os arquivos abaixo afetam mais de uma stack e devem ser tratados como compartilhados:

- `README.md`
- `agents.md`
- `.github/workflows/**`
- `docker-compose.yml`
- `render.yaml`
- `.gitignore`
- `Farol.sln`
- documentacao em `docs/`

Ao tocar nesses arquivos, o agente deve deixar claro:

- qual stack foi impactada
- por que o arquivo compartilhado precisou ser alterado
- qual parte foi mantida sem mudanca

## Fluxo de trabalho esperado

1. Ler a documentacao relevante e a estrutura do projeto
2. Localizar os arquivos realmente ligados a tarefa
3. Confirmar o limite de atuacao da stack
4. Implementar apenas o necessario
5. Validar com os comandos apropriados
6. Resumir arquivos alterados, correcoes e riscos

## Build e testes por stack

### Frontend

No diretorio `web/`:

```powershell
npm install
npm run lint
npm run test
npm run build
```

### Backend

Na raiz do repositorio:

```powershell
$env:DOTNET_CLI_HOME='c:\Users\masuc\Desktop\PensarNoNome\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
dotnet restore Farol.sln
dotnet build Farol.sln --no-restore -c Release -m:1 -v minimal
dotnet test tests/Farol.Tests/Farol.Tests.csproj --no-build -c Release -m:1 -v minimal
```

Organizacao atual dos testes backend:

- `tests/Farol.Tests/Domain/`: unit tests de entidades e invariantes
- `tests/Farol.Tests/Auth/`: unit tests de servicos de autenticacao
- `tests/Farol.Tests/Seeding/`: testes focados em seed e dados base
- `tests/Farol.Tests/Api/`: integration tests de endpoints HTTP
- `tests/Farol.Tests/Smoke/`: validacoes de infraestrutura e migracoes

Regra de classificacao:

- testes sem host HTTP devem permanecer em `Domain/`, `Auth/` ou pasta especializada equivalente
- testes que sobem `FarolApiFactory` devem permanecer em `Api/`
- smoke tests devem ficar isolados de testes funcionais

### Python

No diretorio `services/farol_intelligence/`:

```powershell
python -m venv .venv
.venv\Scripts\Activate.ps1
python -m pip install -e .
python -m unittest discover tests
```

## Padrao de resposta do agente

Ao propor ou concluir uma mudanca, o agente deve informar:

1. plano curto
2. arquivos alterados
3. validacao executada
4. tradeoffs ou pontos de atencao

## Branches e integracao

Modelo atual:

- `master`: estado estavel/publicado
- `dev`: integracao local antes de promover
- `codex/issue-*` ou branch de trabalho derivada de `dev`

Regras:

- novas mudancas devem partir de `dev`
- evitar trabalho direto em `master`
- nao fazer merge cego entre `dev` e `master`
- validar localmente antes de promover

## Principios de engenharia

- preferir arquitetura simples e modular
- manter logica de dominio isolada no backend
- evitar otimização prematura
- usar nomes explicitos e codigo legivel
- adicionar testes quando houver mudanca funcional
- documentar assumptions de forma curta quando necessario

## Definicao de pronto

Uma entrega so esta pronta quando:

1. o escopo esta respeitado
2. a documentacao relevante foi atualizada
3. build e testes adequados ao escopo foram considerados
4. nao houve alteracao desnecessaria fora da stack
5. riscos residuais foram citados no resumo
