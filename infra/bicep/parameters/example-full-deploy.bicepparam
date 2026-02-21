using '../main.bicep'

// ========================================
// FULL DEPLOYMENT - Create All New Resources
// ========================================
// This example shows deploying the ClamAV API with all new resources.
// All infrastructure (Log Analytics, App Insights, Storage, ACR, etc.) will be created fresh.

// ========================================
// REQUIRED PARAMETERS
// ========================================

param environmentName = 'dev'
param location = 'eastus'
param applicationName = 'clamav-api'

// ========================================
// AUTHENTICATION PARAMETERS
// ========================================

param enableAuthentication = true
param aadClientId = ''  // TODO: Replace with your Azure AD App Client ID

// ========================================
// CONTAINER APP CONFIGURATION
// ========================================

param containerImageTag = 'latest'
param containerCpuCores = '2.0'
param containerMemory = '4.0Gi'
param minReplicas = 1
param maxReplicas = 5

// ========================================
// CONTAINER IMAGE SOURCE
// ========================================

// Using Docker Hub (default)
param useLocalAcr = false
param dockerHubImage = 'laurentaerenscodit/clamav-api'

// Alternative: Use local ACR (uncomment to enable)
// param useLocalAcr = true
// param containerRegistrySku = 'Standard'
// param enableAcrAdminUser = false

// ========================================
// APPLICATION SETTINGS
// ========================================

param maxFileSizeMB = 200
param maxConcurrentWorkers = 4
param aspNetCoreEnvironment = 'Development'

// ========================================
// STORAGE CONFIGURATION
// ========================================

param clamavFileShareName = 'clamav-database'
param fileShareQuotaGiB = 5

// ========================================
// LOG ANALYTICS & MONITORING
// ========================================

param logRetentionDays = 30
param enableApplicationInsights = true
param appInsightsRetentionDays = 90
param appInsightsDisableIpMasking = false

// ========================================
// MALWARE DETECTION ALERTS
// ========================================

param enableMalwareAlerts = true
param malwareAlertEmail = 'security-team@example.com'  // TODO: Replace with your email

// Alternative: Use existing Action Group (uncomment to use)
// param malwareAlertActionGroupId = '/subscriptions/{sub-id}/resourceGroups/{rg}/providers/Microsoft.Insights/actionGroups/{name}'

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

// ========================================
// BRING YOUR OWN RESOURCES
// ========================================
// Leave all empty to create new resources (this is the default)

param useExistingManagedEnvironment = false
// param existingContainerEnvironmentId = ''
// param existingLogAnalyticsWorkspaceId = ''
// param existingApplicationInsightsId = ''
// param existingStorageAccountId = ''
// param existingContainerRegistryId = ''
