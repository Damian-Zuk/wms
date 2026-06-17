#requires -Version 7
<#
.SYNOPSIS
  One-time Azure infrastructure setup for the WMS API.

.DESCRIPTION
  Provisions:
    - Resource group
    - PostgreSQL Flexible Server (Burstable B1ms) + a 'wms' database, reachable from Azure services
    - Container Apps environment + the 'wms-api' container app

  The container *image* is built and pushed by the GitHub Actions workflow
  (.github/workflows/deploy-api.yml) to GitHub Container Registry, which then
  runs `az containerapp update --image ...` on every push to main. This script
  creates the app with a placeholder image; the first CI run replaces it.

  Secrets (DB connection string, JWT key, admin password) are stored as
  Container App secrets, so the values committed in appsettings.json are never
  used in production.

.PREREQUISITES
  - Azure CLI (`az`), logged in:  az login
  - A Postgres admin password and a WMS admin-account password (avoid ';' and
    '=' in the Postgres password so it stays valid inside the connection string)

.EXAMPLE
  ./deploy/deploy-azure.ps1 -PgAdminPassword 'S0me-Strong-Pwd' -AdminAccountPassword 'Admin1234!'
#>

[CmdletBinding()]
param(
    [string]$ResourceGroup = "wms-rg",
    [string]$Location      = "northeurope",
    [string]$EnvName       = "wms-env",
    [string]$AppName       = "wms-api",
    # Postgres server name must be globally unique -> random suffix by default.
    [string]$PgServer      = "wms-pg-9d20f9",#$((New-Guid).ToString('N').Substring(0,6))",
    [string]$PgAdminUser   = "wmsadmin",

    [Parameter(Mandatory)] [string]$PgAdminPassword,      # Postgres admin password
    [Parameter(Mandatory)] [string]$AdminAccountPassword, # seeds the WMS 'admin' user on startup

    # Set once the Static Web App exists; can be updated later (see notes at the end).
    [string]$SwaOrigin = "https://CHANGE-ME.azurestaticapps.net"
)

$ErrorActionPreference = "Stop"
# `az` is a native command, so its non-zero exit codes don't trip
# $ErrorActionPreference on their own. This makes a failed `az` call stop the script.
$PSNativeCommandUseErrorActionPreference = $true

# Fresh JWT signing key so the placeholder in appsettings.json is never used in prod.
$JwtSecret = [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }))

Write-Host "==> Ensuring containerapp CLI extension + resource providers..." -ForegroundColor Cyan
az extension add --name containerapp --upgrade --only-show-errors | Out-Null
az provider register --namespace Microsoft.App --wait | Out-Null
az provider register --namespace Microsoft.OperationalInsights --wait | Out-Null
az provider register --namespace Microsoft.DBforPostgreSQL --wait | Out-Null

Write-Host "==> Creating resource group '$ResourceGroup' ($Location)..." -ForegroundColor Cyan
$rgExists = az group exists -n $ResourceGroup
if ($rgExists -eq "false") {
az group create -n $ResourceGroup -l $Location --only-show-errors | Out-Null
} else { Write-Host "    (already exists, skipping)" -ForegroundColor DarkGray }

Write-Host "==> Creating PostgreSQL flexible server '$PgServer' (this takes a few minutes)..." -ForegroundColor Cyan
# Idempotent: skip if a server with this name already exists, so a re-run that
# passes -PgServer <existing-name> reuses it instead of provisioning a duplicate.
$pgExists = az postgres flexible-server list -g $ResourceGroup --query "[?name=='$PgServer'] | length(@)" -o tsv
if ($pgExists -eq "0") {
az postgres flexible-server create `
    --resource-group $ResourceGroup `
    --name $PgServer `
    --location $Location `
    --tier Burstable `
    --sku-name Standard_B1ms `
    --storage-size 32 `
    --version 17 `
    --admin-user $PgAdminUser `
    --admin-password $PgAdminPassword `
    --public-access 0.0.0.0 `
    --yes --only-show-errors | Out-Null
} else { Write-Host "    (server already exists, skipping creation)" -ForegroundColor DarkGray }

# The DB is created separately. Two CLI quirks: on `flexible-server create` the
# --database-name flag is reserved for elastic clusters, and on `db create` the
# database-name flag is --name (not --database-name).
Write-Host "==> Creating database 'wms'..." -ForegroundColor Cyan
$dbExists = az postgres flexible-server db list -g $ResourceGroup --server-name $PgServer --query "[?name=='wms'] | length(@)" -o tsv
if ($dbExists -eq "0") {
az postgres flexible-server db create `
    --resource-group $ResourceGroup `
    --server-name $PgServer `
    --name wms `
    --only-show-errors | Out-Null
} else { Write-Host "    (database already exists, skipping)" -ForegroundColor DarkGray }

$PgFqdn     = "$PgServer.postgres.database.azure.com"
$ConnString = "Host=$PgFqdn;Port=5432;Database=wms;Username=$PgAdminUser;Password=$PgAdminPassword;SSL Mode=Require;Trust Server Certificate=true"

Write-Host "==> Creating Container Apps environment '$EnvName'..." -ForegroundColor Cyan
$envExists = az containerapp env list -g $ResourceGroup --query "[?name=='$EnvName'] | length(@)" -o tsv
if ($envExists -eq "0") {
az containerapp env create `
    --name $EnvName `
    --resource-group $ResourceGroup `
    --location $Location `
    --only-show-errors | Out-Null
} else { Write-Host "    (already exists, skipping)" -ForegroundColor DarkGray }

Write-Host "==> Creating container app '$AppName' (placeholder image; CI replaces it)..." -ForegroundColor Cyan
# Placeholder listens on :80 so its revision shows unhealthy until the first CI
# deploy pushes the real image (which listens on :8080). The public FQDN is
# assigned immediately regardless, which is all we need to wire up CORS + client.
$appExists = az containerapp list -g $ResourceGroup --query "[?name=='$AppName'] | length(@)" -o tsv
if ($appExists -eq "0") {
az containerapp create `
    --name $AppName `
    --resource-group $ResourceGroup `
    --environment $EnvName `
    --image mcr.microsoft.com/k8se/quickstart:latest `
    --target-port 8080 `
    --ingress external `
    --min-replicas 0 `
    --max-replicas 1 `
    --secrets "db-conn=$ConnString" "jwt-secret=$JwtSecret" "admin-pwd=$AdminAccountPassword" `
    --env-vars `
        "ASPNETCORE_ENVIRONMENT=Production" `
        "ConnectionStrings__DefaultConnection=secretref:db-conn" `
        "JwtSettings__SecretKey=secretref:jwt-secret" `
        "AdminAccount__Password=secretref:admin-pwd" `
        "Cors__AllowedOrigins=$SwaOrigin" `
    --only-show-errors | Out-Null
} else { Write-Host "    (app already exists, skipping; delete it to recreate with fresh secrets)" -ForegroundColor DarkGray }

$ApiFqdn = az containerapp show -n $AppName -g $ResourceGroup `
    --query "properties.configuration.ingress.fqdn" -o tsv
$SubId = az account show --query id -o tsv

Write-Host ""
Write-Host "===================================================================" -ForegroundColor Green
Write-Host " Infrastructure ready." -ForegroundColor Green
Write-Host "   API URL      : https://$ApiFqdn"
Write-Host "   API base URL : https://$ApiFqdn/api"
Write-Host "                   -> set as VITE_API_BASE_URL in Client.Vue/wms-client/.env.production"
Write-Host "   Postgres     : $PgFqdn"
Write-Host "===================================================================" -ForegroundColor Green
Write-Host ""
Write-Host " Next steps:" -ForegroundColor Yellow
Write-Host " 1. Create a deploy identity and store it as the GitHub secret AZURE_CREDENTIALS:"
Write-Host "      az ad sp create-for-rbac --name wms-deploy --role contributor \"
Write-Host "        --scopes /subscriptions/$SubId/resourceGroups/$ResourceGroup --sdk-auth"
Write-Host " 2. Push to main -> GitHub Actions builds the image (GHCR) and deploys it."
Write-Host "    After the first run, make the GHCR package public so Container Apps can pull it"
Write-Host "    (GitHub > Packages > wms-api > Package settings > Change visibility), or run:"
Write-Host "      az containerapp registry set -n $AppName -g $ResourceGroup --server ghcr.io \"
Write-Host "        --username <github-user> --password <PAT-with-read:packages>"
Write-Host " 3. Once the Static Web App exists, point CORS at it:"
Write-Host "      az containerapp update -n $AppName -g $ResourceGroup \"
Write-Host "        --set-env-vars Cors__AllowedOrigins=https://<your-swa>.azurestaticapps.net"
Write-Host ""
