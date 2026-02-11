# Azure Container Apps Deployment Parameters Template

This directory contains Bicep parameter files for deploying the ClamAV API to different environments.

## Quick Start

1. **Copy the example parameter file:**
   ```bash
   cp example.bicepparam dev.bicepparam
   ```

2. **Edit your parameter file** and configure at minimum:
   - `environmentName` - Your environment name (e.g., 'dev', 'staging', 'prod')
   - `location` - Azure region (e.g., 'eastus', 'westeurope')
   - `aadClientId` - Azure AD application client ID (if using authentication)

3. **Deploy using the parameter file:**
   ```bash
   az deployment group create \
     --resource-group <your-resource-group> \
     --template-file ../main.bicep \
     --parameters dev.bicepparam
   ```

## Parameter Files

- **example.bicepparam** - Fully documented example with all available parameters
- Create your own: `<environment>.bicepparam` (e.g., `dev.bicepparam`, `prod.bicepparam`)

## Essential Parameters

### Authentication (Required for secure deployment)

Before deployment, create an Azure AD app registration:

```bash
# Create AAD app registration
az ad app create --display-name "ClamAV API - Dev"

# Get the application (client) ID
az ad app list --display-name "ClamAV API - Dev" --query "[0].appId" -o tsv
```

Use the client ID in your parameter file:
```bicep
param aadClientId = '<your-client-id>'
param enableAuthentication = true
```

### Container Image

The first deployment requires building and pushing the container image:

```bash
# Build and push to ACR (after ACR is created by Bicep)
az acr build --registry <acr-name> --image clamav-api:latest .
```

Then update your parameter file:
```bicep
param containerImageTag = 'latest'  // or specific version like '1.0.0'
```

## Using Existing Container Apps Environment

If you have a shared Container Apps environment, use it instead of creating a new one:

```bicep
param useExistingManagedEnvironment = true
param existingManagedEnvironmentName = 'cae-shared-prod'
param existingManagedEnvironmentResourceGroup = 'rg-shared-infrastructure'
```

This saves costs and centralizes management.

## Environment-Specific Configurations

### Development
```bicep
param environmentName = 'dev'
param aspNetCoreEnvironment = 'Development'
param minReplicas = 0  // Scale to zero when idle
param maxReplicas = 2
param enableAcrAdminUser = true  // For easy local testing
```

### Staging
```bicep
param environmentName = 'staging'
param aspNetCoreEnvironment = 'Staging'
param minReplicas = 1
param maxReplicas = 3
param containerImageTag = 'v1.2.3'  // Use specific versions
```

### Production
```bicep
param environmentName = 'prod'
param aspNetCoreEnvironment = 'Production'
param minReplicas = 2  // Always-on for best response time
param maxReplicas = 10
param containerRegistrySku = 'Premium'  // For geo-replication
param enableAcrAdminUser = false  // Security: disable admin user
param logRetentionDays = 90  // Longer retention for compliance
```

## Security Best Practices

1. **Never commit secrets** - Use Azure Key Vault references or pipeline variables
2. **Enable authentication** - Always use `enableAuthentication = true` in production
3. **Disable admin user** - Set `enableAcrAdminUser = false` in production
4. **Use specific image tags** - Avoid `latest` tag in production
5. **Configure RBAC** - Assign least-privilege roles to deployment principals

## Validation

Before deploying, validate your parameter file:

```bash
# Validate Bicep syntax
az bicep build --file ../main.bicep

# Preview changes (what-if)
az deployment group what-if \
  --resource-group <your-resource-group> \
  --template-file ../main.bicep \
  --parameters dev.bicepparam
```

## Troubleshooting

**Error: "The parameter aadClientId is required when enableAuthentication is true"**
- Ensure you've set `aadClientId` in your parameter file with a valid Azure AD app client ID

**Error: "Container image not found"**
- Build and push your container image to ACR before first deployment
- Verify the image tag matches your parameter file

**Error: "Storage mount failed"**
- Check storage account firewall settings
- Ensure Container Apps environment can access the storage account

For more detailed guidance, see [docs/azure-deployment.md](../../docs/azure-deployment.md)
