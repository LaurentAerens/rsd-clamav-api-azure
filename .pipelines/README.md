# Azure Pipelines for Container Apps Deployment

This directory contains Azure Pipeline templates for deploying the ClamAV API to Azure Container Apps.

## Files

- **container-app-deploy.yml** - Reusable pipeline template for deployment
- **demo-deploy-pipeline.yml** - Example pipeline showing how to use the template

## Quick Start

### 1. Configure Azure DevOps Service Connection

Create a service connection in Azure DevOps with permissions to deploy to your Azure subscription:

1. Go to **Project Settings** > **Service connections**
2. Create new **Azure Resource Manager** connection
3. Choose **Service Principal (automatic)**
4. Select your subscription and resource group
5. Name it (e.g., "Azure-Production")

### 2. Create Azure AD App Registration

For EasyAuth authentication:

```bash
# Create app registration
az ad app create --display-name "ClamAV API - Production"

# Get the client ID
az ad app list --display-name "ClamAV API - Production" --query "[0].appId" -o tsv
```

### 3. Configure Variable Groups (Recommended)

Create variable groups in **Pipelines** > **Library** to store secrets:

**Variable Group: clamav-api-common**
- `APPLICATION_NAME`: clamav-api
- `AZURE_SERVICE_CONNECTION`: Azure-Production

**Variable Group: clamav-api-dev**
- `AAD_CLIENT_ID_DEV`: (your dev AAD app client ID)
- `RESOURCE_GROUP_DEV`: rg-clamav-dev
- `LOCATION_DEV`: eastus

**Variable Group: clamav-api-prod**
- `AAD_CLIENT_ID_PROD`: (your prod AAD app client ID)
- `RESOURCE_GROUP_PROD`: rg-clamav-prod
- `LOCATION_PROD`: eastus

### 4. Create Azure DevOps Environments

Create environments for deployment approvals:

1. Go to **Pipelines** > **Environments**
2. Create "ClamAV-Dev" environment
3. Create "ClamAV-Production" environment
4. Add **Approvals** to production environment (recommended)

### 5. Create Resource Groups

```bash
# Create resource groups in Azure
az group create --name rg-clamav-dev --location eastus
az group create --name rg-clamav-prod --location eastus
```

### 6. Customize and Run Pipeline

1. Copy `demo-deploy-pipeline.yml` to your repository root or `.pipelines/` directory
2. Update variables in the pipeline:
   - `azureServiceConnection`
   - `devResourceGroup` / `prodResourceGroup`
   - AAD client IDs (or reference variable groups)
3. Commit and push to trigger the pipeline

## Using the Reusable Template

### In Your Own Pipeline

```yaml
# my-custom-pipeline.yml
trigger:
  branches:
    include:
      - main

stages:
  - stage: Deploy
    jobs:
      - deployment: DeployApp
        environment: 'Production'
        pool:
          vmImage: 'ubuntu-latest'
        strategy:
          runOnce:
            deploy:
              steps:
                - checkout: self
                
                - template: .pipelines/container-app-deploy.yml
                  parameters:
                    environmentName: 'prod'
                    azureSubscription: 'Azure-Production'
                    resourceGroupName: 'rg-clamav-prod'
                    location: 'eastus'
                    aadClientId: $(AAD_CLIENT_ID)
                    imageTag: '$(Build.BuildNumber)'
                    minReplicas: 2
                    maxReplicas: 10
```

### Template Parameters

**Required:**
- `environmentName` - Environment name (dev/staging/prod)
- `azureSubscription` - Azure service connection name
- `resourceGroupName` - Target resource group
- `location` - Azure region

**Optional (with defaults):**
- `applicationName` - Application name (default: 'clamav-api')
- `enableAuthentication` - Enable EasyAuth (default: true)
- `aadClientId` - Azure AD client ID (required if auth enabled)
- `imageTag` - Container image tag (default: Build.BuildNumber)
- `minReplicas` / `maxReplicas` - Scaling (default: 1/5)
- `cpuCores` / `memorySize` - Resources (default: 1.0/2.0Gi)
- `maxFileSizeMB` - Max scan file size (default: 200)
- `maxConcurrentWorkers` - Background workers (default: 4)
- `useExistingManagedEnvironment` - Use existing Container Apps env (default: false)
- `deployInfrastructure` - Deploy infra or just update app (default: true)

## Pipeline Stages

### Stage 1: Build & Push
- Builds Docker image from Dockerfile
- Pushes to Azure Container Registry
- Tags with build number and 'latest'

### Stage 2: Deploy Infrastructure
- Validates Bicep template (what-if)
- Deploys/updates Azure resources
- Outputs Container App URL

### Stage 3: Update Container App
- Updates container app with new image
- Verifies deployment health
- Tests health endpoint

## Environment-Specific Configurations

### Development
```yaml
minReplicas: 0          # Scale to zero when idle
maxReplicas: 2
cpuCores: '0.5'
memorySize: '1.0Gi'
```

### Staging
```yaml
minReplicas: 1
maxReplicas: 3
cpuCores: '1.0'
memorySize: '2.0Gi'
```

### Production
```yaml
minReplicas: 2          # Always-on
maxReplicas: 10
cpuCores: '1.0'
memorySize: '2.0Gi'
```

## Using Existing Container Apps Environment

To deploy to a shared Container Apps environment:

```yaml
parameters:
  useExistingManagedEnvironment: true
  existingManagedEnvironmentName: 'cae-shared-prod'
  existingManagedEnvironmentResourceGroup: 'rg-shared-infrastructure'
```

## Deployment Approvals

Configure approval gates in Azure DevOps Environments:

1. Go to **Environments** > **ClamAV-Production**
2. Click **•••** > **Approvals and checks**
3. Add **Approvals** check
4. Select approvers
5. Optionally add **Business hours** restriction

## Monitoring Deployments

After deployment:

1. **Azure Portal**: View Container App in resource group
2. **Logs**: Container Apps > Log stream
3. **Metrics**: Container Apps > Metrics (requests, CPU, memory)
4. **Health**: Test `/healthz` endpoint
5. **Swagger**: Visit `/swagger` for API documentation

## Troubleshooting

**Issue: "Service connection not found"**
- Verify service connection name matches pipeline variable
- Check service connection has permissions to resource group

**Issue: "AAD Client ID required when authentication enabled"**
- Set `aadClientId` parameter or use variable group
- Or set `enableAuthentication: false` (not recommended for production)

**Issue: "Container App not updating"**
- Check image tag is different from previous deployment
- Verify ACR pull permissions for Container App managed identity

**Issue: "Health check fails with 401"**
- Expected if authentication is enabled
- Test with valid Azure AD token or disable auth temporarily

## Best Practices

1. **Use Variable Groups** - Store secrets in variable groups, not pipeline YAML
2. **Enable Approvals** - Require manual approval for production deployments
3. **Tag Images** - Use semantic versioning or build numbers, not 'latest' in production
4. **Monitor Costs** - Set minReplicas=0 for dev to scale to zero when idle
5. **Test Deployments** - Use staging environment before production
6. **Audit Trail** - Azure DevOps tracks all deployments and approvers

## Next Steps

- Set up **Application Insights** for monitoring and telemetry
- Configure **custom domains** for Container Apps
- Add **blue-green deployment** strategy for zero-downtime updates
- Implement **automated testing** in pipeline before deployment

For more information, see [docs/azure-deployment.md](../docs/azure-deployment.md)
