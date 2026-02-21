using '../main.bicep'

// ========================================
// BRING YOUR OWN RESOURCES - Use Existing Infrastructure
// ========================================
// This example shows deploying the ClamAV API using existing shared resources.
// This is ideal for:
//   - Production environments with centralized monitoring
//   - Cost optimization by reusing existing infrastructure
//   - Organizational compliance requiring shared Log Analytics/App Insights
//   - Multi-tenant scenarios with isolated container apps but shared services

// ========================================
// REQUIRED PARAMETERS
// ========================================

param environmentName = 'prod'
param location = 'eastus'
param applicationName = 'clamav-api'

// ========================================
// AUTHENTICATION PARAMETERS
// ========================================

param enableAuthentication = true
param aadClientId = '12345678-1234-1234-1234-123456789abc'  // TODO: Replace with your App Client ID

// ========================================
// BRING YOUR OWN RESOURCES
// ========================================
// Provide resource IDs of existing resources
// These can be in different subscriptions or resource groups

// ---------- Container Apps Environment ----------
// Use existing Container Apps environment for multiple apps
param existingContainerEnvironmentId = '/subscriptions/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/resourceGroups/rg-shared-platform/providers/Microsoft.App/managedEnvironments/cae-shared-prod'

// ---------- Log Analytics Workspace ----------
// Use central logging workspace for compliance/cost optimization
param existingLogAnalyticsWorkspaceId = '/subscriptions/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/resourceGroups/rg-monitoring/providers/Microsoft.OperationalInsights/workspaces/log-centralmonitoring-prod'

// ---------- Application Insights ----------
// Use shared Application Insights across multiple applications
param existingApplicationInsightsId = '/subscriptions/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/resourceGroups/rg-monitoring/providers/Microsoft.Insights/components/appi-shared-prod'

// ---------- Storage Account ----------
// Use existing storage account (file share will be created if it doesn't exist)
param existingStorageAccountId = '/subscriptions/bbbbbbbb-cccc-dddd-eeee-ffffffffffff/resourceGroups/rg-storage/providers/Microsoft.Storage/storageAccounts/stsharedprod01'

// Storage account key - only needed if storage is in a different subscription
// Leave empty if in same subscription (key retrieved automatically via RBAC)
// param existingStorageAccountKey = 'secret-key-here'  // Use Key Vault or secure parameters in production

// ---------- Azure Container Registry ----------
// Use existing ACR for centralized container management (requires useLocalAcr = true)
param existingContainerRegistryId = '/subscriptions/cccccccc-dddd-eeee-ffff-aaaaaaaaaaaa/resourceGroups/rg-containers/providers/Microsoft.ContainerRegistry/registries/acrsharedprod'

// ---------- Action Group for Alerts ----------
// Use existing Action Group for security alerts
param malwareAlertActionGroupId = '/subscriptions/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/resourceGroups/rg-monitoring/providers/Microsoft.Insights/actionGroups/ag-security-team'

// ========================================
// CONTAINER APP CONFIGURATION
// ========================================

param containerImageTag = 'v1.2.0'  // Use specific version in production
param containerCpuCores = '2.0'
param containerMemory = '4.0Gi'
param minReplicas = 2  // Higher for production HA
param maxReplicas = 10

// ========================================
// CONTAINER IMAGE SOURCE
// ========================================

// Using local ACR (since we have existing ACR)
param useLocalAcr = true
param containerRegistrySku = 'Premium'  // Using existing, but parameter still required
param enableAcrAdminUser = false

// If using Docker Hub instead, set useLocalAcr = false and uncomment:
// param dockerHubImage = 'laurentaerenscodit/clamav-api'

// ========================================
// APPLICATION SETTINGS
// ========================================

param maxFileSizeMB = 200
param maxConcurrentWorkers = 4
param aspNetCoreEnvironment = 'Production'

// ========================================
// STORAGE CONFIGURATION
// ========================================

param clamavFileShareName = 'clamav-database-prod'
param fileShareQuotaGiB = 10  // Larger for production

// ========================================
// LOG ANALYTICS & MONITORING
// ========================================
// These apply to Container Environment if creating new (ignored when using existing)

param logRetentionDays = 90
param enableApplicationInsights = true
param appInsightsRetentionDays = 180  // Longer retention for production
param appInsightsDisableIpMasking = false

// ========================================
// MALWARE DETECTION ALERTS
// ========================================

param enableMalwareAlerts = true
// malwareAlertActionGroupId is set above (using existing Action Group)

// Alternative: Create new Action Group from email (if no existing Action Group)
// param malwareAlertEmail = 'security-team@example.com'

// ========================================
// RESOURCE TAGS
// ========================================

param tags = {
  Environment: 'prod'
  Application: 'clamav-api'
  ManagedBy: 'Bicep'
  CostCenter: 'IT-Security'
  Owner: 'security-team@example.com'
  Compliance: 'SOC2'
  DataClassification: 'Confidential'
}

// ========================================
// LEGACY PARAMETERS (Deprecated)
// ========================================
// Use existingContainerEnvironmentId instead

param useExistingManagedEnvironment = false
