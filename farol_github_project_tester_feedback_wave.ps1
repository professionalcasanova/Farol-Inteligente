$ErrorActionPreference = 'Stop'

$OWNER = 'professionalcasanova'
$REPO = 'PensarNoNome'
$PROJECT_NUMBER = '1'

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
        return
    }

    gh label create $Name --repo "$OWNER/$REPO" --color $Color --description $Description | Out-Null
    Write-Host "Label criada: $Name"
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

    try {
        gh project item-add $PROJECT_NUMBER --owner $OWNER --url $IssueUrl 2>$null | Out-Null

        if ($LASTEXITCODE -eq 0) {
            Write-Host "Adicionada ao Project #$PROJECT_NUMBER"
            return
        }
    }
    catch {
        Write-Host "Nao foi possivel adicionar ao Project automaticamente. Verifique se a issue ja esta no board: $IssueUrl"
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

Create-Label-If-Missing 'improvement' 'a2eeef' 'Improvement to existing behavior'
Create-Label-If-Missing 'priority:high' 'b60205' 'High priority'
Create-Label-If-Missing 'priority:medium' 'fbca04' 'Medium priority'
Create-Label-If-Missing 'frontend' 'c5def5' 'Frontend area'
Create-Label-If-Missing 'ux' 'fef2c0' 'UX and product copy'
Create-Label-If-Missing 'v1' '0e8a16' 'Scope approved for current release'

$issue1 = @'
## Context
Durante os testes, o sino do dashboard mostra quantidade de focos ativos, mas ainda nao se comporta como uma central simples de alertas. Em alguns cenarios o clique parece nao fazer nada, porque o usuario nao ve a mensagem nem entende para onde deve ir.

## Objective
Transformar o sino em uma lista simples de alertas visiveis e clicaveis, com redirecionamento para o local certo em cada mensagem.

## Acceptance Criteria
- [ ] Clicar no sino abre uma lista de alertas/focos do mes
- [ ] Cada item mostra mensagem clara e destino da acao
- [ ] Clicar em um item leva o usuario para a tela correta
- [ ] Nunca existe clique sem efeito perceptivel quando houver notificacao
- [ ] Existe estado vazio coerente quando nao houver alertas
- [ ] Testes cobrem a abertura da lista e a navegacao principal

## Technical Notes
Areas provaveis:
- Dashboard web
- Contrato de alerts e month health no frontend

## Codex Execution
- Arquivos envolvidos:
  - web/app/dashboard/page.tsx
  - testes do dashboard
- Tipo de mudanca:
  - UX e navegacao
- Restricoes:
  - manter a experiencia simples para o MVP
  - usar o contexto do alerta como destino principal
- O que NAO fazer:
  - Nao manter badge com clique que aparenta nao fazer nada
'@
Create-Issue-And-Add-To-Project 'Turn dashboard bell into an actionable alert list' @('improvement','priority:high','frontend','ux','v1') $issue1

$issue2 = @'
## Context
A tela de planejamento ainda transmite termos e encaixes visuais pouco naturais para o usuario brasileiro, como "template recorrente", botao apertado e elementos saindo do enquadramento. Isso reduz confianca no fluxo.

## Objective
Polir a linguagem e o layout do planejamento para deixar a base recorrente mais clara e mais confiavel visualmente.

## Acceptance Criteria
- [ ] "Template" e substituido por texto mais claro para o usuario brasileiro
- [ ] Botao "Aplicar ao mes" ganha espacamento e hierarquia melhores
- [ ] Elementos deixam de ultrapassar o enquadramento visual
- [ ] A base recorrente continua separada da edicao do mes atual
- [ ] Testes cobrem os textos e a estrutura principal quando relevante

## Technical Notes
Areas provaveis:
- Planejamento web

## Codex Execution
- Arquivos envolvidos:
  - web/app/budget/page.tsx
  - testes da tela de planejamento
- Tipo de mudanca:
  - UX, copy e layout
- Restricoes:
  - manter o fluxo de aplicar base ao mes
  - nao misturar base recorrente com snapshot do mes
- O que NAO fazer:
  - Nao usar "template" como termo principal da experiencia
'@
Create-Issue-And-Add-To-Project 'Polish budget planning language and recurring-base layout' @('improvement','priority:medium','frontend','ux','v1') $issue2

Write-Host ''
Write-Host 'Bootstrap concluido com sucesso.'
Write-Host 'Confira as novas issues e o GitHub Project no navegador.'
