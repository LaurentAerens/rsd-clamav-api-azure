# Create Azure AD App Registration for ClamAV API Authentication
# This script creates an app registration that enables Entra ID authentication
# for your Container App. Other Azure resources (APIM, Logic Apps, etc.) will
# request tokens for this app registration to authenticate.

param(
    [Parameter(Mandatory=$false)]
    [string]$DisplayName,
    
    [Parameter(Mandatory=$false)]
    [string]$EnvironmentName = "dev"
)

# If no display name provided, use default
if ([string]::IsNullOrEmpty($DisplayName)) {
    $DisplayName = "ClamAV API - $EnvironmentName"
}

Write-Host "Creating Azure AD App Registration..." -ForegroundColor Cyan
Write-Host "Display Name: $DisplayName" -ForegroundColor Gray

# Check if app registration already exists
$existingApp = az ad app list --display-name $DisplayName --query "[0].appId" -o tsv 2>$null

if ($existingApp) {
    Write-Host "`nApp registration already exists!" -ForegroundColor Yellow
    Write-Host "Client ID: $existingApp" -ForegroundColor Green
    Write-Host "`nTo use this in your deployment, set:" -ForegroundColor Cyan
    Write-Host "  param aadClientId = '$existingApp'" -ForegroundColor White
    exit 0
}

# Create the app registration
$clientId = az ad app create `
    --display-name $DisplayName `
    --sign-in-audience AzureADMyOrg `
    --query appId -o tsv

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nFailed to create app registration" -ForegroundColor Red
    Write-Host "Make sure you have permissions to create app registrations in Azure AD" -ForegroundColor Yellow
    exit 1
}

# Get tenant ID
$tenantId = az account show --query tenantId -o tsv

Write-Host "`n✓ App Registration Created Successfully!" -ForegroundColor Green
Write-Host "`nDetails:" -ForegroundColor Cyan
Write-Host "  Display Name: $DisplayName" -ForegroundColor White
Write-Host "  Client ID:    $clientId" -ForegroundColor White
Write-Host "  Tenant ID:    $tenantId" -ForegroundColor White

Write-Host "`nNext Steps:" -ForegroundColor Cyan
Write-Host "1. Update your bicepparam file with:" -ForegroundColor White
Write-Host "   param aadClientId = '$clientId'" -ForegroundColor Gray
Write-Host "`n2. Deploy your infrastructure" -ForegroundColor White
Write-Host "   az deployment group create --resource-group <rg> --template-file main.bicep --parameters <yourfile>.bicepparam" -ForegroundColor Gray
Write-Host "`n3. Calling resources (APIM, Logic Apps, etc.) request tokens with:" -ForegroundColor White
Write-Host "   resource/scope: $clientId" -ForegroundColor Gray
Write-Host "   or: api://$clientId" -ForegroundColor Gray

# Optionally set API URI (recommended)
Write-Host "`nOptional: Set Application ID URI for cleaner token requests" -ForegroundColor Cyan
$setUri = Read-Host "Do you want to set Application ID URI to 'api://$clientId'? (y/N)"
if ($setUri -eq 'y' -or $setUri -eq 'Y') {
    $appObjectId = az ad app list --display-name $DisplayName --query "[0].id" -o tsv
    az ad app update --id $appObjectId --identifier-uris "api://$clientId"
    Write-Host "✓ Application ID URI set to: api://$clientId" -ForegroundColor Green
}

Write-Host "`n✓ Setup Complete!" -ForegroundColor Green
