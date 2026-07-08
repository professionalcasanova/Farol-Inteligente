$ErrorActionPreference = 'Stop'

# -----------------------------
# CONFIGURE THESE VALUES
# -----------------------------
$OWNER = 'professionalcasanova'
$REPO = 'Farol-Inteligente'
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

Create-Label-If-Missing 'feature' '0e8a16' 'New product or engineering capability'
Create-Label-If-Missing 'bug' 'd73a4a' 'Something is broken'
Create-Label-If-Missing 'improvement' 'a2eeef' 'Improvement to existing behavior'
Create-Label-If-Missing 'priority:high' 'b60205' 'High priority'
Create-Label-If-Missing 'priority:medium' 'fbca04' 'Medium priority'
Create-Label-If-Missing 'backend' '1d76db' 'Backend area'
Create-Label-If-Missing 'frontend' 'c5def5' 'Frontend area'
Create-Label-If-Missing 'tests' 'f9d0c4' 'Automated tests'
Create-Label-If-Missing 'ux' 'fef2c0' 'UX and product copy'
Create-Label-If-Missing 'v1.1' '1f6feb' 'Scope for beta close-out v1.1'
Create-Label-If-Missing 'beta-feedback' 'd4c5f9' 'Feedback consolidated from beta validation'

$issue1 = @'
## Context
Beta feedback exposed a concrete onboarding gap: users can create the first financial account, but the main dashboard flow stops surfacing account creation after that. In practice, this makes the product feel single-account even though the backend already supports multiple accounts.

## Objective
Expose multi-account management in the main web flow so users can create and choose more than one account without leaving the MVP path.

## Acceptance Criteria
- [ ] User can create an additional financial account even after the first one already exists
- [ ] Dashboard keeps a visible path to account management after onboarding is complete
- [ ] Quick entry makes account selection explicit when more than one account exists
- [ ] Import flow continues to work with more than one account
- [ ] Existing account API is reused instead of rebuilding account support from scratch
- [ ] Automated tests cover multiple-account creation and account selection behavior in the web flow

## Technical Notes
Important correction:
- Backend support already exists for multiple accounts and account update
- Current gap is mainly frontend exposure and workflow clarity

## Codex Execution
- Arquivos envolvidos:
  - web/app/dashboard/page.tsx
  - web/lib/api.ts
  - tests do dashboard e contas
- Tipo de mudanca:
  - UX and frontend flow over existing backend capability
- Restricoes:
  - keep the MVP navigation simple
  - preserve existing account contracts unless a gap is proven
- O que NAO fazer:
  - Do not open a large account-management subsystem if the existing API already covers the basics
'@
Create-Issue-And-Add-To-Project 'Expose multi-account management in the primary web flow' @('improvement','priority:high','frontend','backend','tests','ux','v1.1','beta-feedback') $issue1

$issue2 = @'
## Context
Bills currently support creation, listing, pay and unpay, but not true editing or deletion. That is not enough for beta validation because mistakes in due date, amount or description are normal and should not force users to recreate data.

## Objective
Add real CRUD for bills, including clear behavior for recurring and installment occurrences.

## Acceptance Criteria
- [ ] Single bills can be edited
- [ ] Single bills can be deleted safely
- [ ] UX explains the difference between editing one occurrence and editing the whole series
- [ ] Recurring/installment flows define what can be changed per occurrence versus per series
- [ ] Pay/unpay remains available as a status action, not a substitute for editing
- [ ] Automated tests cover create, update, delete and the main recurring/installment edge cases

## Technical Notes
Areas provaveis:
- BillsController
- bill domain and series behavior
- bills page

## Codex Execution
- Arquivos envolvidos:
  - src/Farol.Api/Modules/Bills/*
  - dominio de bills/series
  - web/app/bills/page.tsx
  - testes de API e frontend
- Tipo de mudanca:
  - backend and frontend CRUD completion
- Restricoes:
  - preserve recurrence/installment consistency
  - keep user intent explicit when editing generated occurrences
- O que NAO fazer:
  - Do not treat pay/unpay as a replacement for edit/delete
'@
Create-Issue-And-Add-To-Project 'Add true CRUD for bills with series-aware editing rules' @('feature','priority:high','backend','frontend','tests','ux','v1.1','beta-feedback') $issue2

$issue3 = @'
## Context
The product already supports transaction editing on the dedicated Movimentacoes screen and monthly budget replacement as a snapshot, but beta feedback shows the operational edit path is still unclear or incomplete for day-to-day correction.

## Objective
Close the editing gap for monthly planning and dashboard-originated manual entries so correction becomes a normal workflow instead of a hidden workaround.

## Acceptance Criteria
- [ ] Monthly budget editing remains clearly modeled as editing the month snapshot
- [ ] Dashboard-originated quick entries have a visible correction path after creation
- [ ] Users do not need to infer where to fix a mistaken entry
- [ ] UX copy makes it clear what is edited in place versus what redirects to a dedicated screen
- [ ] If delete enters scope, it is supported with simple safeguards for manual transactions
- [ ] Automated tests cover the main edit path for planning and manual entries

## Technical Notes
Important correction:
- Transactions already have update support
- Budget already supports upsert/replace of the monthly snapshot
- The current problem is primarily operational discoverability and CRUD completeness

## Codex Execution
- Arquivos envolvidos:
  - web/app/dashboard/page.tsx
  - web/app/transactions/page.tsx
  - web/app/budget/page.tsx
  - src/Farol.Api/Modules/Budgets/*
  - testes web e API
- Tipo de mudanca:
  - UX clarification and CRUD completion
- Restricoes:
  - keep monthly budget as a month snapshot
  - avoid splitting editing logic across too many disconnected places
- O que NAO fazer:
  - Do not redesign budget into a completely new model just to expose editing better
'@
Create-Issue-And-Add-To-Project 'Close the operational editing gap for budget and dashboard-originated entries' @('improvement','priority:high','frontend','backend','tests','ux','v1.1','beta-feedback') $issue3

$issue4 = @'
## Context
Beta validation on iPhone shows mobile web overflow: navigation and notification areas exceed the useful viewport, hurting readability and basic interaction. Native mobile can stay for a future version, but mobile web must be reliable now.

## Objective
Fix mobile web responsiveness for iPhone-sized screens, with emphasis on shell layout, header composition and overflow control.

## Acceptance Criteria
- [ ] No critical horizontal overflow on iPhone-sized widths
- [ ] Main navigation remains usable without content escaping the viewport
- [ ] Header, session block and utility actions wrap or collapse cleanly
- [ ] Key dashboard sections remain readable and actionable on mobile web
- [ ] Manual validation covers Safari-sized mobile viewports
- [ ] Automated tests cover at least the most fragile responsive layout assumptions where feasible

## Technical Notes
Areas provaveis:
- AppShell
- dashboard layout
- global spacing and overflow rules

## Codex Execution
- Arquivos envolvidos:
  - web/components/app-shell.tsx
  - web/app/globals.css
  - dashboard and other high-density pages if needed
  - frontend tests
- Tipo de mudanca:
  - responsive UI hardening
- Restricoes:
  - preserve existing visual direction
  - mobile web should improve without waiting for native app work
- O que NAO fazer:
  - Do not scope-creep this into a dedicated native mobile project
'@
Create-Issue-And-Add-To-Project 'Fix iPhone mobile web overflow and responsive shell behavior' @('bug','priority:high','frontend','tests','ux','v1.1','beta-feedback') $issue4

$issue5 = @'
## Context
The current system category seed is still narrow for real Brazilian usage. Beta feedback mentioned PIX, but PIX is not a spending category by itself; it is a payment rail or transfer method. Mixing those concepts would weaken budgeting and insights.

## Objective
Expand the Brazilian financial taxonomy while preserving the distinction between category and payment method.

## Acceptance Criteria
- [ ] System categories are reviewed and expanded for common Brazilian income and expense contexts
- [ ] Category choices continue to answer "what was this for?" instead of "how was this paid?"
- [ ] PIX is not introduced as a budgeting category
- [ ] If payment rail tracking is needed, it is modeled as a separate concern from category
- [ ] API and frontend remain backward-compatible for existing transactions
- [ ] Automated tests cover category seeding and category listing expectations

## Technical Notes
Areas provaveis:
- Category seed
- categories endpoint consumption
- transaction entry UX

## Codex Execution
- Arquivos envolvidos:
  - src/Farol.Infrastructure/Seeding/CategorySeed.cs
  - web flows that display categories
  - testes de categorias e seeding
- Tipo de mudanca:
  - taxonomy refinement for Brazilian context
- Restricoes:
  - keep the MVP simple
  - do not collapse category and payment method into one field
- O que NAO fazer:
  - Do not create a misleading category list where PIX, boleto and cartao replace actual expense purpose categories
'@
Create-Issue-And-Add-To-Project 'Expand Brazilian transaction taxonomy without conflating category and payment method' @('improvement','priority:medium','backend','frontend','tests','ux','v1.1','beta-feedback') $issue5

Write-Host ''
Write-Host 'Bootstrap concluido com sucesso.'
Write-Host 'Confira as issues da wave v1.1 beta close-out no GitHub Project.'
