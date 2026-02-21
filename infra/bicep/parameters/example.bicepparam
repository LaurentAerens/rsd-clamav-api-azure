using '../main.bicep'

// ========================================
// Example Parameter File for ClamAV API Deployment
// ========================================
// Copy this file and customize for your environment (dev/staging/prod)
// Rename to: <environment>.bicepparam (e.g., dev.bicepparam, prod.bicepparam)

// ========================================
// REQUIRED PARAMETERS
// ========================================

// Environment name - used for resource naming and tagging
// Examples: 'dev', 'staging', 'prod'
param environmentName = 'dev'

// Azure region for all resources
// Examples: 'eastus', 'westeurope', 'westus2'
param location = 'eastus'

// Application name - used as prefix for resource names
// Must be 3-20 characters, alphanumeric only
param applicationName = 'clamav-api'

// ========================================
// AUTHENTICATION PARAMETERS
// ========================================
// If enableAuthentication is true, you must provide aadClientId
// See docs/azure-deployment.md for AAD app registration steps

// Enable Azure AD authentication via EasyAuth
param enableAuthentication = true

// Azure AD Client ID (Application ID from your app registration)
// REQUIRED if enableAuthentication = true
// Example: '12345678-1234-1234-1234-123456789abc'
param aadClientId = ''  // TODO: Replace with your Azure AD App Client ID

// Azure AD Tenant ID (defaults to current tenant)
// Can be found in Azure Portal > Azure Active Directory > Overview
// Example: '87654321-4321-4321-4321-cba987654321'
// param aadTenantId = '00000000-0000-0000-0000-000000000000'  // Uncomment and set if different from deployment tenant

// Azure AD Audience for token validation
// Usually same as aadClientId or 'api://{clientId}'
// Leave empty to default to aadClientId
// param aadAudience = 'api://12345678-1234-1234-1234-123456789abc'  // Uncomment if using custom audience

// ========================================
// EXISTING ENVIRONMENT (OPTIONAL)
// ========================================
// Set existingContainerEnvironmentId to use an existing Container Apps environment
// instead of creating a new one. Use full resource ID for cross-subscription scenarios.

// Existing Container Apps environment resource ID (leave empty to create new)
// Format: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.App/managedEnvironments/{environmentName}
// param existingContainerEnvironmentId = '/subscriptions/12345678-1234-1234-1234-123456789abc/resourceGroups/rg-shared/providers/Microsoft.App/managedEnvironments/cae-shared-prod'

// DEPRECATED: Use existingContainerEnvironmentId instead
// Legacy parameters kept for backward compatibility
param useExistingManagedEnvironment = false
// param existingManagedEnvironmentName = 'cae-shared-prod'
// param existingManagedEnvironmentResourceGroup = 'rg-shared-infrastructure'

// ========================================
// BRING YOUR OWN RESOURCES (OPTIONAL)
// ========================================
// Provide resource IDs of existing resources to avoid creating new ones
// Useful for shared infrastructure, cost savings, or organizational policies

// ---------- Existing Log Analytics Workspace ----------
// If provided, uses existing workspace instead of creating new
// Format: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.OperationalInsights/workspaces/{workspaceName}
// param existingLogAnalyticsWorkspaceId = '/subscriptions/12345678-1234-1234-1234-123456789abc/resourceGroups/rg-monitoring/providers/Microsoft.OperationalInsights/workspaces/log-shared-prod'

// ---------- Existing Application Insights ----------
// If provided, uses existing Application Insights instead of creating new
// Format: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Insights/components/{appInsightsName}
// param existingApplicationInsightsId = '/subscriptions/12345678-1234-1234-1234-123456789abc/resourceGroups/rg-monitoring/providers/Microsoft.Insights/components/appi-shared-prod'

// ---------- Existing Storage Account ----------
// If provided, uses existing storage account and creates file share in it
// Format: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Storage/storageAccounts/{accountName}
// param existingStorageAccountId = '/subscriptions/12345678-1234-1234-1234-123456789abc/resourceGroups/rg-storage/providers/Microsoft.Storage/storageAccounts/stsharedprod'

// Storage account key (REQUIRED only if using existing storage in different subscription)
// Leave empty if storage is in same subscription (key retrieved automatically)
// param existingStorageAccountKey = 'your-storage-key-here'  // Use secure parameter in production

// ---------- Existing Azure Container Registry ----------
// If provided, uses existing ACR instead of creating new (requires useLocalAcr = true)
// Format: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.ContainerRegistry/registries/{registryName}
// param existingContainerRegistryId = '/subscriptions/12345678-1234-1234-1234-123456789abc/resourceGroups/rg-containers/providers/Microsoft.ContainerRegistry/registries/acrsharedprod'

// ========================================
// CONTAINER APP CONFIGURATION
// ========================================

// Container App name (defaults to '{applicationName}-{environmentName}')
// Uncomment to override
// param containerAppName = 'clamav-api-custom-name'

// Container image tag to deploy
// Set to specific version in production (e.g., '1.0.0', 'v2.3.1')
param containerImageTag = 'latest'

// Container resources
// Note: ClamAV requires minimum 4GB memory
param containerCpuCores = '2.0'        // CPU cores: 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0
param containerMemory = '4.0Gi'        // Memory: must be 2x CPU (e.g., 0.5Gi, 1.0Gi, 2.0Gi, 4.0Gi)

// ========================================
// CONTAINER IMAGE SOURCE (DOCKER HUB vs ACR)
// ========================================
// By default, images are pulled from Docker Hub (laurentaerenscodit/clamav-api)
// Set useLocalAcr = true to build and use images from a local Azure Container Registry instead

// Use local Azure Container Registry instead of Docker Hub
// Default: false (use Docker Hub image)
param useLocalAcr = false

// Docker Hub image name (only used if useLocalAcr is false)
// Default: 'laurentaerenscodit/clamav-api'
param dockerHubImage = 'laurentaerenscodit/clamav-api'

// Scaling configuration
param minReplicas = 1                  // Minimum replicas (0 = scale to zero)
param maxReplicas = 5                  // Maximum replicas

// ========================================
// APPLICATION SETTINGS
// ========================================

// Maximum file size for virus scanning (in MB)
param maxFileSizeMB = 200

// Maximum concurrent background workers for async scanning
param maxConcurrentWorkers = 4

// ASP.NET Core environment
// Use 'Production' for prod, 'Staging' or 'Development' for non-prod
param aspNetCoreEnvironment = 'Development'

// ========================================
// AZURE CONTAINER REGISTRY (OPTIONAL)
// ========================================
// Only used if useLocalAcr is true

// Container Registry name (leave empty for auto-generated name)
// Uncomment to use custom name (and set useLocalAcr = true)
// param containerRegistryName = 'myacrname'

// Container Registry SKU (only used if useLocalAcr = true)
// Basic: Dev/test, Standard: Production (recommended), Premium: Geo-replication
param containerRegistrySku = 'Standard'

// Enable admin user for ACR (useful for initial setup, disable in production)
param enableAcrAdminUser = false

// ========================================
// STORAGE CONFIGURATION
// ========================================

// Storage account name for ClamAV database (leave empty for auto-generated)
// param storageAccountName = 'stclamavprod'  // Uncomment to use custom name

// File share name for ClamAV virus database
param clamavFileShareName = 'clamav-database'

// File share quota in GB
param fileShareQuotaGiB = 5

// ========================================
// LOG ANALYTICS
// ========================================

// Log retention in days (only applies if creating new environment)
param logRetentionDays = 30

// ========================================
// APPLICATION INSIGHTS (MONITORING)
// ========================================

// Enable Application Insights for telemetry, distributed tracing, and custom metrics
// Set to false to disable all telemetry (app will run normally with console logging only)
param enableApplicationInsights = true

// Application Insights name (leave empty for auto-generated)
// param applicationInsightsName = 'appi-clamav-api-dev'  // Uncomment to use custom name

// Application Insights data retention in days
// Longer retention allows historical analysis but increases costs
param appInsightsRetentionDays = 90

// Daily data cap in GB (0 = no cap)
// Set a cap to control costs in high-traffic scenarios
// param appInsightsDailyCapGB = 5  // Uncomment to set daily cap

// Disable IP masking for detailed telemetry (enable for troubleshooting)
// When false (default), IP addresses are masked for privacy
param appInsightsDisableIpMasking = false

// ========================================
// MALWARE DETECTION ALERTS (OPTIONAL)
// ========================================
// Configure alerts to be notified when malware is detected
// Two options:
//   1. Provide existing Action Group ID (malwareAlertActionGroupId)
//   2. Provide email address to create new Action Group (malwareAlertEmail)

// Enable alerts for malware detection events
param enableMalwareAlerts = true

// Option 1: Use existing Action Group
// Resource ID of an existing Action Group to send alerts to
// Format: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Insights/actionGroups/{actionGroupName}
// param malwareAlertActionGroupId = '/subscriptions/12345678-1234-1234-1234-123456789abc/resourceGroups/rg-monitoring/providers/Microsoft.Insights/actionGroups/Security-Team'
param malwareAlertActionGroupId = ''

// Option 2: Create new Action Group from email address
// If malwareAlertActionGroupId is empty, provide email to create new Action Group
// Example: 'security-team@example.com'
// param malwareAlertEmail = 'security-team@example.com'
param malwareAlertEmail = ''

// Alert threshold: number of malware detections required to trigger alert
// Set to 1 to alert on any detection, higher values require multiple detections in the time window
// param malwareAlertThreshold = 1  // Uncomment and modify as needed

// Time window for evaluating malware detections (in minutes, 1-1440)
// Alert counts detections within this window before triggering
// param malwareAlertEvaluationMinutes = 5  // Uncomment to customize (default: 5 minutes)

// ========================================
// ADVANCED OPTIONS
// ========================================

// Enable zone redundancy for Container Apps environment
// Requires Premium SKU and specific regions
// param enableZoneRedundancy = false  // Uncomment for production with HA requirements

// ========================================
// RESOURCE TAGS
// ========================================

param tags = {
  Environment: 'dev'
  Application: 'clamav-api'
  ManagedBy: 'Bicep'
  CostCenter: 'IT-Security'
  Owner: 'platform-team@example.com'
}
