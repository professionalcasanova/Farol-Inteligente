param(
    [Parameter(Mandatory = $true)]
    [string] $ConnectionString
)

$ErrorActionPreference = "Stop"

$sqlScriptPath = Join-Path $PSScriptRoot "sql\normalize-system-categories.sql"

if (-not (Test-Path -LiteralPath $sqlScriptPath)) {
    throw "SQL script not found at '$sqlScriptPath'."
}

if (-not (Get-Command psql -ErrorAction SilentlyContinue)) {
    throw "psql was not found in PATH. Install PostgreSQL client tools before running this script."
}

Write-Host "Running system category normalization against the target database..."
Write-Host "Backup the database before executing this script in homologation."

& psql $ConnectionString -v ON_ERROR_STOP=1 -f $sqlScriptPath
