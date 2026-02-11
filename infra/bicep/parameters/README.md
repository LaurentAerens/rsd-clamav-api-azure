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

## Application Insights and Monitoring

Application Insights is enabled by default and provides:
- **Distributed tracing** - Track requests across services
- **Custom metrics** - Scan performance, malware detections, queue depth
- **Live metrics** - Real-time monitoring dashboard
- **Application Map** - Visualize dependencies and performance
- **Kusto queries** - Advanced log analysis

### Enable Application Insights (Default)
```bicep
param enableApplicationInsights = true
param appInsightsRetentionDays = 90
```

### Disable Application Insights
To disable telemetry completely (app runs normally with console logging):
```bicep
param enableApplicationInsights = false
```

When disabled:
- ✅ Application still runs normally
- ✅ Logs go to console (captured by Container Apps logs)
- ❌ No custom metrics or distributed tracing
- ❌ No Application Insights dashboards

### Cost Control
Set a daily cap to prevent unexpected charges:
```bicep
param appInsightsDailyCapGB = 5  // Stop ingestion after 5GB per day
```

### Querying Telemetry
Once deployed, query logs in Azure Portal:
```kusto
// Find malware detections
traces
| where message contains "Malware detected"
| project timestamp, message, customDimensions
| order by timestamp desc

// Scan performance metrics
customMetrics
| where name == "ScanDuration"
| summarize avg(value), percentile(value, 95) by bin(timestamp, 1h)
```

## Malware Detection Alerts

Get notified immediately when malware is detected using Azure Monitor Action Groups. This is optional but highly recommended for production.

### Prerequisites

Create an Action Group first in Azure:

```bash
# Create Action Group for security team
az monitor action-group create \
  --resource-group <your-resource-group> \
  --name SecurityTeamAlerts \
  --short-name "SecAlert"

# Add email notification
az monitor action-group update \
  --resource-group <your-resource-group> \
  --name SecurityTeamAlerts \
  --add-action-group-receiver SecTeamEmail email \
  --receiver-name SecTeamEmail \
  --receiver-email-address security-team@yourcompany.com

# Get the Action Group ID
az monitor action-group show \
  --resource-group <your-resource-group> \
  --name SecurityTeamAlerts \
  --query id -o tsv
```

### Enable Malware Alerts

Add to your parameter file:

```bicep
param enableMalwareAlerts = true
param malwareAlertActionGroupId = '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Insights/actionGroups/SecurityTeamAlerts'
```

### Configure Alert Behavior

Control when alerts trigger:

```bicep
// Alert on any detection (default)
param malwareAlertThreshold = 1

// Or alert only after multiple detections in 5 minutes
param malwareAlertThreshold = 3
param malwareAlertEvaluationMinutes = 5

// Increase evaluation window to 15 minutes
param malwareAlertEvaluationMinutes = 15
```

### Disable Alerts

To disable malware alerts:

```bicep
param enableMalwareAlerts = false
```

Or leave the action group ID empty:

```bicep
param malwareAlertActionGroupId = ''  // Alerts won't be created
```

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
