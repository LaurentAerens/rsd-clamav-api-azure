# Azure Container Apps Deployment Guide

Complete guide for deploying the ClamAV API to Azure Container Apps with Bicep and Azure Pipelines.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Architecture Overview](#architecture-overview)
- [Step-by-Step Deployment](#step-by-step-deployment)
  - [1. Azure AD App Registration](#1-azure-ad-app-registration)
  - [2. Prepare Azure Environment](#2-prepare-azure-environment)
  - [3. Configure Deployment Parameters](#3-configure-deployment-parameters)
  - [4. Deploy Infrastructure with Bicep](#4-deploy-infrastructure-with-bicep)
  - [5. Build and Push Container Image](#5-build-and-push-container-image)
  - [6. Verify Deployment](#6-verify-deployment)
- [Using Existing Container Apps Environment](#using-existing-container-apps-environment)
- [CI/CD with Azure Pipelines](#cicd-with-azure-pipelines)
- [Configuration Reference](#configuration-reference)
- [Monitoring and Operations](#monitoring-and-operations)
- [Scaling and Performance](#scaling-and-performance)
- [Security Best Practices](#security-best-practices)
- [Troubleshooting](#troubleshooting)
- [Cost Optimization](#cost-optimization)
- [Advanced Scenarios](#advanced-scenarios)

---

## Prerequisites

### Required Tools

1. **Azure CLI** (v2.50.0 or later)
   ```bash
   az --version
   az upgrade  # Update if needed
   ```

2. **Bicep CLI** (bundled with Azure CLI)
   ```bash
   az bicep version
   az bicep upgrade
   ```

3. **Azure Subscription**
   - Must have permissions to create resources
   - Contributor or Owner role on subscription or resource group

4. **Docker** (for local testing)
   ```bash
   docker --version
   ```

### Azure Permissions

Ensure you have the following permissions:

- **Microsoft.Authorization/roleAssignments/write** - For assigning managed identity roles
- **Microsoft.App/\*** - For Container Apps resources
- **Microsoft.ContainerRegistry/registries/\*** - For Azure Container Registry
- **Microsoft.Storage/storageAccounts/\*** - For Azure Files storage
- **Microsoft.OperationalInsights/workspaces/\*** - For Log Analytics

### Access Required

- Azure Active Directory permissions to create app registrations (optional, required only if enabling authentication)

---

## Architecture Overview

The deployment creates the following infrastructure:

```
┌─────────────────────────────────────────────────────────────┐
│                    Azure Resource Group                      │
│                                                               │
│  ┌────────────────────────────────────────────────────────┐ │
│  │           Azure Container Apps Environment             │ │
│  │                                                          │ │
│  │  ┌──────────────────────────────────────────────────┐  │ │
│  │  │          ClamAV API Container App                │  │ │
│  │  │  - Auto-scaling (1-5 replicas)                   │  │ │
│  │  │  - EasyAuth (Azure AD)                           │  │ │
│  │  │  - Health probes                                 │  │ │
│  │  │  - Volume mount: /var/lib/clamav                 │  │ │
│  │  └──────────────────────────────────────────────────┘  │ │
│  │                                                          │ │
│  │  Storage: Azure Files (clamav-database)                 │ │
│  │  Logging: Log Analytics Workspace                       │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                               │
│  ┌────────────────────┐   ┌──────────────────────────────┐  │
│  │ Container Registry │   │ Storage Account              │  │
│  │ - clamav-api:tag   │   │ - File Share: clamav-db      │  │
│  │ - Managed Identity │   │ - 5GB quota                  │  │
│  └────────────────────┘   └──────────────────────────────┘  │
│                                                               │
│  ┌─────────────────────────────────────────────────────────┐│
│  │         Log Analytics Workspace                         ││
│  │         - Container logs                                ││
│  │         - Metrics                                       ││
│  └─────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────┘
```

---

## Step-by-Step Deployment

### 1. Azure AD App Registration

Create an Azure AD app registration for authentication (skip if `enableAuthentication=false`).

#### Option A: Using Azure Portal

1. Navigate to **Azure Active Directory** > **App registrations**
2. Click **New registration**
3. Configure:
   - **Name**: `ClamAV API - Production`
   - **Supported account types**: Accounts in this organizational directory only
   - **Redirect URI**: Leave empty (EasyAuth handles this)
4. Click **Register**
5. Copy the **Application (client) ID** from the Overview page
6. Copy the **Directory (tenant) ID** from the Overview page

#### Option B: Using Azure CLI

```bash
# Create app registration
az ad app create \
  --display-name "ClamAV API - Production" \
  --sign-in-audience AzureADMyOrg

# Get the client ID
CLIENT_ID=$(az ad app list \
  --display-name "ClamAV API - Production" \
  --query "[0].appId" -o tsv)

# Get tenant ID
TENANT_ID=$(az account show --query tenantId -o tsv)

echo "Client ID: $CLIENT_ID"
echo "Tenant ID: $TENANT_ID"

# Save these values!
```

#### Configure API Permissions (Optional)

If your app needs to call other APIs:

```bash
# Example: Add Microsoft Graph API permissions
APP_OBJECT_ID=$(az ad app list \
  --display-name "ClamAV API - Production" \
  --query "[0].id" -o tsv)

az ad app permission add \
  --id $APP_OBJECT_ID \
  --api 00000003-0000-0000-c000-000000000000 \  # Microsoft Graph
  --api-permissions e1fe6dd8-ba31-4d61-89e7-88639da4683d=Scope  # User.Read
```

---

### 2. Prepare Azure Environment

#### Login to Azure

```bash
# Login to Azure
az login

# Set subscription (if you have multiple)
az account set --subscription "Your Subscription Name"

# Verify current subscription
az account show --query "{Name:name, SubscriptionId:id, TenantId:tenantId}" -o table
```

#### Create Resource Group

```bash
# Define variables
RESOURCE_GROUP="rg-clamav-prod"
LOCATION="eastus"
ENVIRONMENT="prod"

# Create resource group
az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION \
  --tags Environment=$ENVIRONMENT Application=clamav-api ManagedBy=Bicep
```

---

### 3. Configure Deployment Parameters

#### Option A: Using Inline Parameters (Quick Start)

Deploy directly with command-line parameters:

```bash
az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file infra/bicep/main.bicep \
  --parameters environmentName=$ENVIRONMENT \
  --parameters location=$LOCATION \
  --parameters applicationName="clamav-api" \
  --parameters aadClientId=$CLIENT_ID \
  --parameters enableAuthentication=true \
  --parameters minReplicas=2 \
  --parameters maxReplicas=10
```

#### Option B: Using Parameter File (Recommended)

**Step 1: Create parameter file**

```bash
# Copy example parameter file
cp infra/bicep/parameters/example.bicepparam infra/bicep/parameters/prod.bicepparam
```

**Step 2: Edit parameter file**

```bicep
using './main.bicep'

// Required parameters
param environmentName = 'prod'
param location = 'eastus'
param applicationName = 'clamav-api'

// Authentication
param enableAuthentication = true
param aadClientId = '12345678-1234-1234-1234-123456789abc'  // YOUR CLIENT ID
// param aadTenantId = ''  // Leave empty to use subscription tenant

// Container configuration
param containerImageTag = 'latest'
param containerCpuCores = '1.0'
param containerMemory = '2.0Gi'

// Scaling
param minReplicas = 2  // Production: always-on
param maxReplicas = 10

// Application settings
param maxFileSizeMB = 200
param maxConcurrentWorkers = 4
param aspNetCoreEnvironment = 'Production'

// Azure Container Registry
param containerRegistrySku = 'Standard'
param enableAcrAdminUser = false

// Logging
param logRetentionDays = 90  // Production: longer retention

// Tags
param tags = {
  Environment: 'prod'
  Application: 'clamav-api'
  ManagedBy: 'Bicep'
  CostCenter: 'IT-Security'
  Owner: 'platform-team@example.com'
}
```

**Step 3: Deploy with parameter file**

```bash
az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/parameters/prod.bicepparam
```

---

### 4. Deploy Infrastructure with Bicep

#### Validate Before Deployment (What-If)

Preview changes without actually deploying:

```bash
az deployment group what-if \
  --resource-group $RESOURCE_GROUP \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/parameters/prod.bicepparam
```

Review the output to see what resources will be created/modified.

#### Deploy Infrastructure

```bash
# Deploy with progress output
az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/parameters/prod.bicepparam \
  --verbose

# Or deploy and save outputs to file
az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/parameters/prod.bicepparam \
  --output json > deployment-output.json
```

**Expected deployment time**: 3-5 minutes

#### Capture Deployment Outputs

```bash
# Get Container App URL
CONTAINER_APP_URL=$(az deployment group show \
  --resource-group $RESOURCE_GROUP \
  --name main \
  --query properties.outputs.containerAppUrl.value -o tsv)

# Get ACR name
ACR_NAME=$(az deployment group show \
  --resource-group $RESOURCE_GROUP \
  --name main \
  --query properties.outputs.containerRegistryName.value -o tsv)

# Get Storage Account name
STORAGE_NAME=$(az deployment group show \
  --resource-group $RESOURCE_GROUP \
  --name main \
  --query properties.outputs.storageAccountName.value -o tsv)

echo "Container App URL: $CONTAINER_APP_URL"
echo "ACR Name: $ACR_NAME"
echo "Storage Account: $STORAGE_NAME"
```

---

### 5. Build and Push Container Image

#### Build and Push to ACR

```bash
# Login to ACR (if not using managed identity)
az acr login --name $ACR_NAME

# Build and push using ACR build task (recommended - builds in Azure)
az acr build \
  --registry $ACR_NAME \
  --image clamav-api:latest \
  --image clamav-api:1.0.0 \
  --file Dockerfile \
  .

# Alternative: Build locally and push
# docker build -t $ACR_NAME.azurecr.io/clamav-api:latest .
# docker push $ACR_NAME.azurecr.io/clamav-api:latest
```

**Build time**: 5-10 minutes (first build), 2-3 minutes (subsequent)

#### Update Container App with New Image

If the Container App is already running with a different image tag:

```bash
CONTAINER_APP_NAME=$(az deployment group show \
  --resource-group $RESOURCE_GROUP \
  --name main \
  --query properties.outputs.containerAppName.value -o tsv)

az containerapp update \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --image $ACR_NAME.azurecr.io/clamav-api:latest
```

---

### 6. Verify Deployment

#### Check Container App Status

```bash
# Get Container App status
az containerapp show \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query "{Name:name, Status:properties.runningStatus, FQDN:properties.configuration.ingress.fqdn}" -o table
```

Expected status: `Running`

#### Test Health Endpoint

```bash
# If authentication is DISABLED
curl https://$CONTAINER_APP_URL/healthz

# If authentication is ENABLED
# Get access token
TOKEN=$(az account get-access-token --resource $CLIENT_ID --query accessToken -o tsv)

# Call health endpoint with token
curl -H "Authorization: Bearer $TOKEN" https://$CONTAINER_APP_URL/healthz
```

Expected response: `{"status": "healthy"}`

#### Test Swagger UI

Open in browser:
```bash
echo "Swagger UI: https://$CONTAINER_APP_URL/swagger"
```

If authentication is enabled, you'll be redirected to Azure AD login.

#### View Logs

```bash
# Stream logs
az containerapp logs show \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --follow

# Get recent logs
az containerapp logs show \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --tail 100
```

#### Verify ClamAV Database

Check if the ClamAV database is persisted on Azure Files:

```bash
# List files in the file share
az storage file list \
  --account-name $STORAGE_NAME \
  --share-name clamav-database \
  --output table

# Expected files: main.cvd, daily.cvd, bytecode.cvd, etc.
```

---

## Using Existing Container Apps Environment

If you have a shared Container Apps environment across multiple applications:

### Step 1: Check Existing Environment

```bash
# List existing environments
az containerapp env list --output table

# Get environment details
az containerapp env show \
  --name cae-shared-prod \
  --resource-group rg-shared-infrastructure
```

### Step 2: Deploy with Existing Environment

Update your parameter file:

```bicep
param useExistingManagedEnvironment = true
param existingManagedEnvironmentName = 'cae-shared-prod'
param existingManagedEnvironmentResourceGroup = 'rg-shared-infrastructure'
```

Or use inline parameters:

```bash
az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file infra/bicep/main.bicep \
  --parameters environmentName=$ENVIRONMENT \
  --parameters location=$LOCATION \
  --parameters aadClientId=$CLIENT_ID \
  --parameters useExistingManagedEnvironment=true \
  --parameters existingManagedEnvironmentName="cae-shared-prod" \
  --parameters existingManagedEnvironmentResourceGroup="rg-shared-infrastructure"
```

### Benefits

- **Cost savings** - Share environment infrastructure across apps
- **Centralized management** - Single environment to monitor
- **Consistent networking** - Shared VNet configuration
- **Simplified governance** - Unified policies

---

## CI/CD with Azure Pipelines

### Prerequisites

1. **Azure DevOps organization** with a project
2. **Service connection** to Azure subscription
3. **Variable groups** for secrets (recommended)

### Step 1: Create Azure Service Connection

1. Go to **Project Settings** > **Service connections**
2. Click **New service connection** > **Azure Resource Manager**
3. Choose **Service principal (automatic)**
4. Select your subscription
5. Set resource group scope (recommended) or subscription scope
6. Name it: `Azure-Production`
7. Grant access to all pipelines (or specific pipelines)

### Step 2: Create Variable Groups

Go to **Pipelines** > **Library** > **+ Variable group**:

**Group: clamav-api-prod**
- `AAD_CLIENT_ID_PROD`: Your production Azure AD client ID (secret)
- `RESOURCE_GROUP_PROD`: rg-clamav-prod
- `LOCATION_PROD`: eastus

Repeat for dev/staging environments.

### Step 3: Configure Pipeline

Copy the demo pipeline:

```bash
cp .pipelines/demo-deploy-pipeline.yml azure-pipelines.yml
```

Update variables:

```yaml
variables:
  azureServiceConnection: 'Azure-Production'
  prodResourceGroup: 'rg-clamav-prod'
  prodLocation: 'eastus'
```

### Step 4: Create Environments

Go to **Pipelines** > **Environments**:

1. Create `ClamAV-Production` environment
2. Add **Approvals and checks**:
   - Select approvers
   - Set timeout (24 hours)
   - Add approval instructions
3. (Optional) Add business hours restriction

### Step 5: Commit and Run

```bash
git add azure-pipelines.yml
git commit -m "Add Azure Pipeline for Container Apps deployment"
git push
```

Pipeline will trigger automatically on push to `main` branch.

For detailed pipeline documentation, see [`.pipelines/README.md`](../.pipelines/README.md).

---

## Configuration Reference

### Container Resources

| Configuration | Development | Production | Max Values |
|---------------|-------------|------------|------------|
| CPU | 0.5 cores | 1.0-2.0 cores | 4.0 cores |
| Memory | 1.0 Gi | 2.0-4.0 Gi | 8.0 Gi |
| Min Replicas | 0 (scale to zero) | 2 (always-on) | 0-30 |
| Max Replicas | 2 | 10 | 1-30 |

**Memory must be 2x CPU** (e.g., 1.0 CPU = 2.0 Gi memory)

### Application Settings

| Setting | Default | Description | Range |
|---------|---------|-------------|-------|
| MAX_FILE_SIZE_MB | 200 | Max upload size | 1-1000 MB |
| BackgroundProcessing__MaxConcurrentWorkers | 4 | Parallel scan workers | 1-20 |
| ASPNETCORE_ENVIRONMENT | Production | .NET environment | Development, Staging, Production |

### Scaling Rules

**HTTP Scaling Rule**:
- Trigger: 20 concurrent HTTP requests per replica
- Scale up: Add replica when threshold exceeded
- Scale down: Remove replica when below threshold for 5 minutes

**CPU Scaling Rule**:
- Trigger: 70% CPU utilization
- Aggregation: Average across all replicas
- Cool down: 5 minutes

### Azure Files Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| File Share Name | clamav-database | Name of the Azure Files share |
| Quota | 5 GB | Maximum size |
| Access Mode | ReadWrite | Shared across replicas |
| Mount Path | /var/lib/clamav | ClamAV database location |

---

## Monitoring and Operations

### Azure Portal Monitoring

1. **Container App Metrics**:
   - Go to Container App > Monitoring > Metrics
   - View: Requests, Response time, CPU, Memory, Replica count

2. **Log Stream**:
   - Go to Container App > Monitoring > Log stream
   - Real-time application logs

3. **Application Map** (requires App Insights):
   - Visualize dependencies and performance

### Query Logs with Azure CLI

```bash
# Get Log Analytics workspace ID
WORKSPACE_ID=$(az monitor log-analytics workspace show \
  --resource-group $RESOURCE_GROUP \
  --workspace-name log-clamav-api-prod-* \
  --query customerId -o tsv)

# Query container logs
az monitor log-analytics query \
  --workspace $WORKSPACE_ID \
  --analytics-query "ContainerAppConsoleLogs_CL 
    | where ContainerAppName_s == '$CONTAINER_APP_NAME' 
    | where TimeGenerated > ago(1h)
    | order by TimeGenerated desc 
    | take 100" \
  --output table
```

### Common Log Queries

**Error logs**:
```kusto
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == 'clamav-api-prod'
| where Log_s contains "error" or Log_s contains "exception"
| order by TimeGenerated desc
| take 50
```

**Scan activity**:
```kusto
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == 'clamav-api-prod'
| where Log_s contains "ScanFile" or Log_s contains "infected"
| order by TimeGenerated desc
| take 100
```

**Replica scaling events**:
```kusto
ContainerAppSystemLogs_CL
| where ContainerAppName_s == 'clamav-api-prod'
| where Log_s contains "scaling" or Log_s contains "replica"
| order by TimeGenerated desc
```

### Alerts

Create alerts for critical metrics:

```bash
# Create alert for high error rate
az monitor metrics alert create \
  --name "ClamAV-API-HighErrorRate" \
  --resource-group $RESOURCE_GROUP \
  --scopes /subscriptions/{sub-id}/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.App/containerApps/$CONTAINER_APP_NAME \
  --condition "count HttpFailed > 10" \
  --window-size 5m \
  --evaluation-frequency 1m \
  --action email admin@example.com
```

---

## Scaling and Performance

### Manual Scaling

Temporarily override auto-scaling:

```bash
# Scale to specific replica count
az containerapp update \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --min-replicas 5 \
  --max-replicas 5
```

### Performance Tuning

**Increase concurrent workers**:

```bash
az containerapp update \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --set-env-vars BackgroundProcessing__MaxConcurrentWorkers=8
```

**Increase resources**:

```bash
az containerapp update \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --cpu 2.0 \
  --memory 4.0Gi
```

### Load Testing

Use Azure Load Testing or Apache JMeter:

```bash
# Install Azure Load Testing extension
az extension add --name load

# Create load test
az load test create \
  --name clamav-load-test \
  --resource-group $RESOURCE_GROUP \
  --test-plan ./load-test.jmx
```

---

## Security Best Practices

### 1. Enable Authentication

Always enable Azure AD authentication in production:

```bicep
param enableAuthentication = true
param aadClientId = 'your-client-id'
```

### 2. Disable ACR Admin User

Use managed identity instead of admin credentials:

```bicep
param enableAcrAdminUser = false
```

### 3. Use Specific Image Tags

Avoid `latest` tag in production:

```bash
az containerapp update \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --image $ACR_NAME.azurecr.io/clamav-api:1.2.3
```

### 4. Implement Network Security

**VNet Integration**:

```bash
# Create subnet
az network vnet subnet create \
  --resource-group $RESOURCE_GROUP \
  --vnet-name my-vnet \
  --name container-apps-subnet \
  --address-prefixes 10.0.0.0/23

# Update Container Apps environment
az containerapp env update \
  --name $CONTAINER_ENVIRONMENT_NAME \
  --resource-group $RESOURCE_GROUP \
  --infrastructure-subnet-resource-id /subscriptions/{sub-id}/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Network/virtualNetworks/my-vnet/subnets/container-apps-subnet
```

### 5. Restrict IP Access

Add firewall rules:

```bash
# Restrict to specific IPs (if not using VNet)
az containerapp ingress access-restriction set \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --rule-name "office-network" \
  --ip-address "203.0.113.0/24" \
  --action Allow
```

### 6. Enable Container Scanning

Scan container images for vulnerabilities:

```bash
# Enable Microsoft Defender for Containers
az security pricing create \
  --name Containers \
  --tier Standard
```

---

## Troubleshooting

### Issue: Container App Not Starting

**Symptoms**: Container App shows "Provisioning" indefinitely

**Causes**:
- Image not found in ACR
- ACR pull permissions missing
- Invalid configuration

**Resolution**:

```bash
# Check Container App provisioning state
az containerapp show \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query "{Name:name, ProvisioningState:properties.provisioningState, RunningStatus:properties.runningStatus}"

# Check revision status
az containerapp revision list \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query "[].{Name:name, Active:properties.active, ProvisioningState:properties.provisioningState}" -o table

# View system logs
az containerapp logs show \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --type system
```

### Issue: Authentication Failures (401)

**Symptoms**: API returns 401 Unauthorized

**Causes**:
- Invalid or expired token
- Incorrect audience configuration
- EasyAuth misconfiguration

**Resolution**:

```bash
# Verify EasyAuth configuration
az containerapp auth show \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP

# Test with valid token
TOKEN=$(az account get-access-token --resource $CLIENT_ID --query accessToken -o tsv)
curl -v -H "Authorization: Bearer $TOKEN" https://$CONTAINER_APP_URL/healthz

# Check token claims (decode at jwt.ms)
echo $TOKEN
```

### Issue: ClamAV Database Not Persisting

**Symptoms**: Long startup times, database re-downloaded on each scale event

**Causes**:
- Azure Files mount failed
- Storage account access denied
- Incorrect mount path

**Resolution**:

```bash
# Verify storage mount
az containerapp show \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query "properties.template.volumes" -o json

# Check file share contents
az storage file list \
  --account-name $STORAGE_NAME \
  --share-name clamav-database \
  --output table

# View mount logs
az containerapp logs show \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --tail 100 | grep -i "mount\|volume\|storage"
```

### Issue: High Memory Usage / OOM Kills

**Symptoms**: Containers restarting frequently, "OOMKilled" status

**Causes**:
- ClamAV database loaded into memory (~300MB)
- Large file scans
- Insufficient memory allocation

**Resolution**:

```bash
# Increase memory allocation
az containerapp update \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --cpu 1.5 \
  --memory 3.0Gi

# Reduce max file size
az containerapp update \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --set-env-vars MAX_FILE_SIZE_MB=100
```

### Issue: Slow Performance

**Symptoms**: High response times, timeouts

**Causes**:
- Insufficient replicas
- Under-resourced containers
- Too few concurrent workers

**Resolution**:

```bash
# Increase minimum replicas
az containerapp update \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --min-replicas 3

# Increase resources
az containerapp update \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --cpu 2.0 \
  --memory 4.0Gi

# Increase concurrent workers
az containerapp update \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --set-env-vars BackgroundProcessing__MaxConcurrentWorkers=8
```

### Getting Help

If issues persist:

1. **Check Azure Status**: [status.azure.com](https://status.azure.com)
2. **Review Documentation**: [Azure Container Apps docs](https://learn.microsoft.com/azure/container-apps/)
3. **Support Ticket**: Open ticket in Azure Portal
4. **Community**: [Stack Overflow](https://stackoverflow.com/questions/tagged/azure-container-apps)

---

## Cost Optimization

### Development Environment

```bicep
// Scale to zero when idle
param minReplicas = 0
param maxReplicas = 2

// Smaller resources
param containerCpuCores = '0.5'
param containerMemory = '1.0Gi'

// Shorter log retention
param logRetentionDays = 30
```

**Estimated cost**: $5-15/month

### Production Environment

```bicep
// Always-on for best performance
param minReplicas = 2
param maxReplicas = 10

// Production resources
param containerCpuCores = '1.0'
param containerMemory = '2.0Gi'

// Longer retention for compliance
param logRetentionDays = 90
```

**Estimated cost**: $50-150/month

### Cost Monitoring

```bash
# View cost analysis
az consumption usage list \
  --start-date 2024-01-01 \
  --end-date 2024-01-31 \
  --query "[?contains(instanceName, 'clamav')].{Name:instanceName, Cost:pretaxCost, Currency:currency}" \
  --output table
```

### Cost-Saving Tips

1. **Use existing Container Apps environment** - Share infrastructure
2. **Scale to zero in dev** - Save costs when idle
3. **Right-size containers** - Don't over-provision
4. **Use Standard ACR** - Premium only if needed for geo-replication
5. **Optimize log retention** - 30 days for dev, 90 for prod
6. **Use Azure Reservations** - Save up to 20% with 1-year commitment

---

## Advanced Scenarios

### Custom Domain with SSL

```bash
# Add custom domain
az containerapp hostname add \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --hostname scan.yourdomain.com

# Bind certificate
az containerapp hostname bind \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --hostname scan.yourdomain.com \
  --certificate-identity /subscriptions/{sub-id}/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.App/managedEnvironments/$CONTAINER_ENVIRONMENT_NAME/managedCertificates/scan-yourdomain-com
```

### Blue-Green Deployment

```bash
# Create new revision with label
az containerapp revision copy \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --image $ACR_NAME.azurecr.io/clamav-api:2.0.0 \
  --revision-suffix v2

# Split traffic (10% to new version)
az containerapp ingress traffic set \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --revision-weight latest=90 v2=10

# Full cutover
az containerapp ingress traffic set \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --revision-weight latest=0 v2=100
```

### Multi-Region Deployment

Deploy to multiple regions for high availability:

```bash
# Deploy to West Europe
az deployment group create \
  --resource-group rg-clamav-westeurope \
  --template-file infra/bicep/main.bicep \
  --parameters location=westeurope \
  --parameters environmentName=prod-we

# Deploy to East US
az deployment group create \
  --resource-group rg-clamav-eastus \
  --template-file infra/bicep/main.bicep \
  --parameters location=eastus \
  --parameters environmentName=prod-eus

# Use Azure Front Door for global load balancing
az afd create \
  --resource-group rg-clamav-global \
  --name clamav-global-fd \
  --sku Premium_AzureFrontDoor
```

### Application Insights Integration

```bash
# Create Application Insights
az monitor app-insights component create \
  --app clamav-api-insights \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION

# Get instrumentation key
INSTRUMENTATION_KEY=$(az monitor app-insights component show \
  --app clamav-api-insights \
  --resource-group $RESOURCE_GROUP \
  --query instrumentationKey -o tsv)

# Update Container App
az containerapp update \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --set-env-vars APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=$INSTRUMENTATION_KEY"
```

---

## Next Steps

- ✅ **Monitor your deployment** - Set up alerts and dashboards
- ✅ **Configure CI/CD** - Automate deployments with Azure Pipelines
- ✅ **Test at scale** - Run load tests to validate capacity
- ✅ **Secure further** - Add VNet integration, private endpoints
- ✅ **Optimize costs** - Review and adjust scaling parameters
- ✅ **Document operations** - Create runbooks for your team

## Additional Resources

- [Azure Container Apps Documentation](https://learn.microsoft.com/azure/container-apps/)
- [Bicep Documentation](https://learn.microsoft.com/azure/azure-resource-manager/bicep/)
- [Azure Verified Modules](https://azure.github.io/Azure-Verified-Modules/)
- [ClamAV Documentation](https://docs.clamav.net/)
- [Azure Architecture Center](https://learn.microsoft.com/azure/architecture/)

---

**Questions or issues?** Please open an issue in the repository or contact the platform team.
