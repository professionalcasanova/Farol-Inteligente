param(
    [int[]]$IssueNumbers = @(),

    [int]$IssueNumber = 0,

    [string]$Base = "master",
    [string]$Head = "",
    [string]$Title = "",
    [string[]]$Summary = @(),
    [string[]]$Validation = @()
)

$ErrorActionPreference = "Stop"

function Need-Cmd {
    param([string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Comando obrigatorio nao encontrado: $Name"
    }
}

function Get-RepoSlug {
    $remoteUrl = (git config --get remote.origin.url).Trim()

    if ($remoteUrl -match "github\.com[:/](.+?)(?:\.git)?$") {
        return $Matches[1]
    }

    throw "Nao foi possivel resolver owner/repo a partir do remote origin."
}

function Ensure-GhAuth {
    try {
        & gh auth status | Out-Null
    }
    catch {
        throw "Voce nao esta autenticado no GitHub CLI. Rode: gh auth login"
    }
}

function Resolve-HeadBranch {
    if ($Head) {
        return $Head
    }

    $currentBranch = (git branch --show-current).Trim()

    if (-not $currentBranch) {
        throw "Nao foi possivel detectar a branch atual."
    }

    return $currentBranch
}

function Resolve-PrTitle {
    param([string]$ResolvedHead)

    if ($Title) {
        return $Title
    }

    $lastCommitTitle = (git log -1 --pretty=%s).Trim()

    if ($lastCommitTitle) {
        return $lastCommitTitle
    }

    return $ResolvedHead
}

function Add-Bullets {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [string[]]$Items,
        [string]$Fallback
    )

    if ($Items.Count -eq 0) {
        $Lines.Add("- $Fallback")
        return
    }

    foreach ($item in $Items) {
        if (-not [string]::IsNullOrWhiteSpace($item)) {
            $Lines.Add("- $item")
        }
    }
}

Need-Cmd git
Need-Cmd gh
Ensure-GhAuth

if ($IssueNumbers.Count -eq 0) {
    if ($IssueNumber -le 0) {
        throw "Informe -IssueNumber ou -IssueNumbers."
    }

    $IssueNumbers = @($IssueNumber)
}

$repo = Get-RepoSlug
$resolvedHead = Resolve-HeadBranch
$resolvedTitle = Resolve-PrTitle -ResolvedHead $resolvedHead

$bodyLines = [System.Collections.Generic.List[string]]::new()
foreach ($resolvedIssueNumber in $IssueNumbers) {
    if ($resolvedIssueNumber -le 0) {
        throw "Os numeros de issue devem ser maiores que zero."
    }

    $bodyLines.Add("Closes #$resolvedIssueNumber")
}
$bodyLines.Add("")
$bodyLines.Add("## Summary")
Add-Bullets -Lines $bodyLines -Items $Summary -Fallback "update implementation"
$bodyLines.Add("")
$bodyLines.Add("## Validation")
Add-Bullets -Lines $bodyLines -Items $Validation -Fallback "not run"

$tempFile = Join-Path $env:TEMP ("farol-pr-" + [System.Guid]::NewGuid().ToString("N") + ".md")
$bodyLines | Set-Content -Path $tempFile -Encoding UTF8

try {
    $existingPr = & gh pr list --repo $repo --head $resolvedHead --state open --json number | ConvertFrom-Json

    if ($existingPr.Count -gt 0) {
        $prNumber = $existingPr[0].number
        & gh pr edit $prNumber --repo $repo --title $resolvedTitle --body-file $tempFile | Out-Null
        Write-Host "PR atualizada: #$prNumber"
        return
    }

    $prUrl = (& gh pr create --repo $repo --base $Base --head $resolvedHead --title $resolvedTitle --body-file $tempFile).Trim()
    Write-Host "PR criada: $prUrl"
}
finally {
    if (Test-Path $tempFile) {
        Remove-Item $tempFile -Force
    }
}
