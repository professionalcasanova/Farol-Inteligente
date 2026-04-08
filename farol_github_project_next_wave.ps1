$ErrorActionPreference = 'Stop'

# -----------------------------
# CONFIGURE THESE VALUES
# -----------------------------
$OWNER = 'professionalcasanova'
$REPO = 'PensarNoNome'
$PROJECT_NUMBER = '1'

# Optional defaults
$ASSIGNEE = '@me'
$MILESTONE = ''

function Need-Cmd {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Comando obrigatorio nao encontrado: $Name"
    }
}

function Ensure-GhAuth {
    try {
        gh auth status | Out-Null
    }
    catch {
        throw "Voce nao esta autenticado no GitHub CLI. Rode: gh auth login"
    }

    $authStatus = gh auth status -t 2>&1 | Out-String
    if ($authStatus -notmatch 'project') {
        Write-Host 'Adicionando scope project ao token...'
        gh auth refresh -s project
    }
}

function Create-Label-If-Missing {
    param(
        [string]$Name,
        [string]$Color,
        [string]$Description
    )

    $labelsJson = gh label list --repo "$OWNER/$REPO" --limit 200 --json name | ConvertFrom-Json
    $exists = $labelsJson | Where-Object { $_.name -eq $Name }

    if ($exists) {
        Write-Host "Label ja existe: $Name"
    }
    else {
        gh label create $Name --repo "$OWNER/$REPO" --color $Color --description $Description | Out-Null
        Write-Host "Label criada: $Name"
    }
}

function Get-Issue-Url-By-Exact-Title {
    param([string]$Title)

    $issues = gh issue list --repo "$OWNER/$REPO" --state all --limit 200 --json title,url | ConvertFrom-Json
    $match = $issues | Where-Object { $_.title -eq $Title } | Select-Object -First 1

    if ($null -ne $match) {
        return $match.url
    }

    return $null
}

function Add-Issue-To-Project {
    param([string]$IssueUrl)

    gh project item-add $PROJECT_NUMBER --owner $OWNER --url $IssueUrl 2>$null | Out-Null

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Adicionada ao Project #$PROJECT_NUMBER"
        return
    }

    Write-Host "Nao foi possivel adicionar ao Project automaticamente. Verifique se a issue ja esta no board: $IssueUrl"
}

function Create-Issue-And-Add-To-Project {
    param(
        [string]$Title,
        [string[]]$Labels,
        [string]$Body
    )

    $existingUrl = Get-Issue-Url-By-Exact-Title -Title $Title
    if ($existingUrl) {
        Write-Host "Issue ja existe: $existingUrl"
        Add-Issue-To-Project -IssueUrl $existingUrl
        return
    }

    $tempFile = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.md')
    Set-Content -Path $tempFile -Value $Body -Encoding UTF8

    try {
        $args = @(
            'issue', 'create',
            '--repo', "$OWNER/$REPO",
            '--title', $Title,
            '--body-file', $tempFile,
            '--assignee', $ASSIGNEE
        )

        foreach ($label in $Labels) {
            $args += @('--label', $label)
        }

        if ($MILESTONE -ne '') {
            $args += @('--milestone', $MILESTONE)
        }

        $issueUrl = (gh @args).Trim()
        Write-Host "Issue criada: $issueUrl"
        Add-Issue-To-Project -IssueUrl $issueUrl
    }
    finally {
        if (Test-Path $tempFile) {
            Remove-Item $tempFile -Force
        }
    }
}

Need-Cmd gh
Ensure-GhAuth

Create-Label-If-Missing 'feature' '0e8a16' 'New product or engineering capability'
Create-Label-If-Missing 'bug' 'd73a4a' 'Something is broken'
Create-Label-If-Missing 'improvement' 'a2eeef' 'Improvement to existing behavior'
Create-Label-If-Missing 'tech-debt' '5319e7' 'Technical debt or refactor'
Create-Label-If-Missing 'priority:high' 'b60205' 'High priority'
Create-Label-If-Missing 'priority:medium' 'fbca04' 'Medium priority'
Create-Label-If-Missing 'priority:low' '0e8a16' 'Low priority'
Create-Label-If-Missing 'backend' '1d76db' 'Backend area'
Create-Label-If-Missing 'frontend' 'c5def5' 'Frontend area'
Create-Label-If-Missing 'intelligence' '7057ff' 'Python intelligence area'
Create-Label-If-Missing 'infra' 'bfd4f2' 'Infrastructure area'
Create-Label-If-Missing 'tests' 'f9d0c4' 'Automated tests'
Create-Label-If-Missing 'ux' 'fef2c0' 'UX and product copy'

$issue1 = @'
## Context
Os alertas criticos ainda explicam pouco a situacao real do usuario. Hoje o usuario pode ver um problema, mas nao entende claramente o impacto no resto do mes, o que precisa priorizar agora e por que aquela decisao vem antes das outras.

## Objective
Transformar alertas criticos em orientacao pratica, clara e acolhedora para decisoes financeiras reais.

## Acceptance Criteria
- [ ] Alerta critico explica a situacao com linguagem simples
- [ ] Alerta deixa claro o impacto no restante do mes
- [ ] Alerta orienta o que priorizar agora
- [ ] Alerta explica por que a prioridade faz sentido
- [ ] Tom continua firme, sem culpabilizar o usuario
- [ ] Cobertura automatizada para os principais cenarios criticos

## Technical Notes
Areas provaveis:
- Dashboard web
- Alertas financeiros
- Month health / intelligence

## Codex Execution
- Arquivos envolvidos:
  - dashboard frontend
  - alertas e month health
  - servico de inteligencia, se necessario
- Tipo de mudanca:
  - UX, copy e encadeamento de resposta
- Restricoes:
  - Nao inventar dados fora do snapshot financeiro
  - Nao deslocar regra financeira para o frontend
- O que NAO fazer:
  - Nao responder com CTA generico sem explicar o problema
'@
Create-Issue-And-Add-To-Project 'Clarify critical financial alerts with practical decision guidance' @('improvement','priority:high','frontend','backend','intelligence','ux') $issue1

$issue2 = @'
## Context
Hoje o sino e alguns estados de alerta podem redirecionar o usuario para uma tela generica de contas a pagar, mesmo quando o problema principal esta em gastos, planejamento ou falta de folga no mes.

## Objective
Fazer a navegacao de alertas e month health seguir a melhor acao contextual para cada cenario.

## Acceptance Criteria
- [ ] CTA principal do estado critico usa a primeira recommendedAction quando existir
- [ ] Navegacao do sino respeita o contexto do alerta
- [ ] Overdue bills continua levando para contas vencidas quando fizer sentido
- [ ] Cenarios de gasto excessivo podem levar para movimentacoes ou planejamento
- [ ] Existe fallback seguro apenas quando nao houver acao contextual
- [ ] Testes cobrem os principais redirecionamentos

## Technical Notes
Areas provaveis:
- Dashboard web
- Alertas financeiros
- Month health response

## Codex Execution
- Arquivos envolvidos:
  - DashboardPage
  - contratos da API do frontend
  - endpoints/respostas de insights, se necessario
- Tipo de mudanca:
  - navegacao contextual e refinamento de UX
- Restricoes:
  - manter rotas simples do MVP
  - respeitar recommendedActions como fonte principal
- O que NAO fazer:
  - Nao deixar CTA critico fixo em /bills para todos os casos
'@
Create-Issue-And-Add-To-Project 'Make alert and month-health CTAs follow contextual actions' @('improvement','priority:high','frontend','backend','intelligence','ux') $issue2

$issue3 = @'
## Context
Contas previsiveis como internet, aluguel, escola e academia ainda precisam ser cadastradas mes a mes, o que gera retrabalho e fragiliza a confianca do usuario no acompanhamento do mes.

## Objective
Adicionar suporte a series recorrentes de contas a pagar com repeticao mensal e duracao configuravel.

## Acceptance Criteria
- [ ] Existe modelo de serie recorrente para bills
- [ ] Serie suporta recorrencia mensal
- [ ] Serie suporta sem fim, ate data e quantidade de ocorrencias
- [ ] Ocorrencias mensais continuam existindo como bills reais do mes
- [ ] Marcar uma ocorrencia como paga nao marca a serie inteira
- [ ] Testes cobrem criacao, expansao e listagem da serie

## Technical Notes
Areas provaveis:
- Dominio Bills
- Persistence / migrations
- BillsController

## Codex Execution
- Arquivos envolvidos:
  - dominio de bills
  - EF configurations e migrations
  - endpoints de bills
  - testes de API e dominio
- Tipo de mudanca:
  - backend estrutural incremental
- Restricoes:
  - manter compatibilidade com bills avulsas
  - evitar regras complexas alem de recorrencia mensal no MVP
- O que NAO fazer:
  - Nao reescrever dashboard e insights para trabalhar apenas com series abstratas
'@
Create-Issue-And-Add-To-Project 'Add recurring bill series for monthly obligations' @('feature','priority:high','backend','tests') $issue3

$issue4 = @'
## Context
Parcelamentos sao muito comuns no contexto brasileiro e o usuario precisa enxergar com clareza por quanto tempo aquela despesa vai continuar existindo no mes, por exemplo 3 de 12 ou 10 de 24.

## Objective
Permitir criar e acompanhar parcelamentos como recorrencias mensais finitas com progresso visivel.

## Acceptance Criteria
- [ ] Usuario pode criar conta parcelada com quantidade total de parcelas
- [ ] Ocorrencias mostram progresso atual e total, por exemplo 3/12
- [ ] Parcelamento entra naturalmente na listagem mensal de contas
- [ ] UI diferencia conta avulsa, recorrente e parcelada
- [ ] Validacoes impedem quantidade invalida de parcelas
- [ ] Testes cobrem contrato de API e exibicao principal no frontend

## Technical Notes
Areas provaveis:
- API de bills
- Tela de contas a pagar

## Codex Execution
- Arquivos envolvidos:
  - contratos e endpoints de bills
  - page.tsx de bills
  - testes web e API
- Tipo de mudanca:
  - produto e UX sobre a base de recorrencia
- Restricoes:
  - parcelamento deve reaproveitar a base da serie mensal
  - manter o fluxo simples para o usuario leigo
- O que NAO fazer:
  - Nao criar varios caminhos desconectados para recorrencia e parcelamento
'@
Create-Issue-And-Add-To-Project 'Add installment tracking and progress for billed obligations' @('feature','priority:high','backend','frontend','tests','ux') $issue4

$issue5 = @'
## Context
O orcamento mensal atual e um snapshot manual. Isso funciona, mas nao ajuda o usuario a manter categorias previsiveis ao longo do tempo, como moradia, internet, escola e transporte.

## Objective
Criar templates recorrentes de orcamento que possam preencher ou sugerir o planejamento de meses futuros sem substituir a edicao mensal.

## Acceptance Criteria
- [ ] Existe modelo de template recorrente para orcamento
- [ ] Template suporta categorias de despesa com valor planejado recorrente
- [ ] Usuario pode aplicar o template ao mes atual
- [ ] Orcamento mensal continua editavel depois da aplicacao
- [ ] Template nao substitui o snapshot mensal atual de forma automatica e irreversivel
- [ ] Testes cobrem aplicacao do template no orcamento do mes

## Technical Notes
Areas provaveis:
- BudgetsController
- Dominio/Persistence de budget
- Tela de planejamento

## Codex Execution
- Arquivos envolvidos:
  - API de budgets
  - frontend de budget
  - migrations e testes
- Tipo de mudanca:
  - extensao do fluxo de planejamento
- Restricoes:
  - manter MonthlyBudget como snapshot do mes
  - template recorrente deve ser complementar
- O que NAO fazer:
  - Nao acoplar template e snapshot de forma que editar um mes mude todos os outros
'@
Create-Issue-And-Add-To-Project 'Add recurring budget templates with apply-to-month flow' @('feature','priority:medium','backend','frontend','tests') $issue5

$issue6 = @'
## Context
Ao introduzir recorrencia e parcelamento, dashboard, free money e insights precisam continuar confiaveis. O usuario deve entender que compromissos previsiveis ja estao pressionando seu mes antes de ser pego de surpresa.

## Objective
Refletir contas recorrentes e parceladas nos resumos, alertas e orientacoes do dashboard.

## Acceptance Criteria
- [ ] Dashboard considera ocorrencias recorrentes reais do mes
- [ ] Free money continua refletindo contas pendentes e atrasadas corretamente
- [ ] Month health consegue citar pressao de compromissos previsiveis quando relevante
- [ ] Alertas continuam coerentes com bills avulsas, recorrentes e parceladas
- [ ] Validacao manual documentada para fluxo principal
- [ ] Testes automatizados cobrem impacto nos insights

## Technical Notes
Areas provaveis:
- MonthlyInsightsService
- Financial alerts
- Month health
- Dashboard web

## Codex Execution
- Arquivos envolvidos:
  - insights backend
  - servico Python, se necessario
  - dashboard frontend
  - testes integrados
- Tipo de mudanca:
  - integracao entre novas obrigacoes e leitura do mes
- Restricoes:
  - manter explicacao simples para o usuario
  - nao duplicar calculos de negocio no frontend
- O que NAO fazer:
  - Nao esconder a pressao recorrente do mes atras de mensagens vagas
'@
Create-Issue-And-Add-To-Project 'Reflect recurring obligations in dashboard and financial insights' @('improvement','priority:high','backend','frontend','intelligence','tests','ux') $issue6

Write-Host ''
Write-Host 'Bootstrap concluido com sucesso.'
Write-Host 'Confira as issues e o GitHub Project no navegador.'
