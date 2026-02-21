// Main Bicep Template for ClamAV API on Azure Container Apps
// Deploys complete infrastructure including Container Registry, Storage, and Container Apps

targetScope = 'resourceGroup'

// ========================================
// Global Parameters
// ========================================

@description('Environment name (e.g., dev, staging, prod)')
@minLength(2)
@maxLength(10)
param environmentName string

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Base name for the application (used for naming resources)')
@minLength(3)
@maxLength(20)
param applicationName string = 'clamav-api'

// ========================================
// Container Image Source Parameters
// ========================================

@description('Use local Azure Container Registry instead of pulling from Docker Hub')
param useLocalAcr bool = false

@description('Default Docker Hub image (only used if useLocalAcr is false)')
param dockerHubImage string = 'laurentaerenscodit/clamav-api'

// ========================================
// Azure Container Registry Parameters (Optional)
// ========================================
// Only used if useLocalAcr is true

@description('Existing Azure Container Registry resource ID (leave empty to create new)')
@metadata({
  example: '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.ContainerRegistry/registries/{registryName}'
})
param existingContainerRegistryId string = ''

@description('Name of the Azure Container Registry (leave empty to auto-generate, used only when creating new)')
param containerRegistryName string = ''

@description('Container Registry SKU (used only when creating new)')
@allowed([
  'Basic'
  'Standard'
  'Premium'
])
param containerRegistrySku string = 'Standard'

@description('Enable admin user for Container Registry (useful for initial setup, used only when creating new)')
param enableAcrAdminUser bool = false

// ========================================
// Container Apps Environment Parameters
// ========================================

@description('Existing Container Apps environment resource ID (leave empty to create new)')
@metadata({
  example: '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.App/managedEnvironments/{environmentName}'
})
param existingContainerEnvironmentId string = ''

@description('[DEPRECATED - use existingContainerEnvironmentId] Use an existing Container Apps managed environment instead of creating a new one')
param useExistingManagedEnvironment bool = false

@description('[DEPRECATED - use existingContainerEnvironmentId] Name of existing Container Apps managed environment (required if useExistingManagedEnvironment is true)')
param existingManagedEnvironmentName string = ''

@description('[DEPRECATED - use existingContainerEnvironmentId] Resource group of existing managed environment (defaults to current resource group)')
param existingManagedEnvironmentResourceGroup string = resourceGroup().name

@description('Enable zone redundancy for Container Apps environment (used only when creating new)')
param enableZoneRedundancy bool = false

// ========================================
// Storage Parameters
// ========================================

@description('Existing storage account resource ID (leave empty to create new)')
@metadata({
  example: '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Storage/storageAccounts/{accountName}'
})
param existingStorageAccountId string = ''

@description('Existing storage account key (required only if using existing storage account in different subscription)')
@secure()
param existingStorageAccountKey string = ''

@description('Name of the storage account for ClamAV database (leave empty to auto-generate, used only when creating new)')
param storageAccountName string = ''

@description('Name of the file share for ClamAV database persistence')
param clamavFileShareName string = 'clamav-database'

@description('File share quota in GB')
@minValue(1)
@maxValue(100)
param fileShareQuotaGiB int = 5

// ========================================
// Container App Parameters
// ========================================

@description('Name of the Container App')
param containerAppName string = '${applicationName}-${environmentName}'

@description('Container image tag to deploy (full image path will be constructed)')
param containerImageTag string = 'latest'

@description('CPU cores per container instance')
param containerCpuCores string = '1.0'

@description('Memory per container instance')
param containerMemory string = '2.0Gi'

@description('Minimum number of replicas')
@minValue(0)
@maxValue(30)
param minReplicas int = 1

@description('Maximum number of replicas')
@minValue(1)
@maxValue(30)
param maxReplicas int = 5

// ========================================
// Application Configuration Parameters
// ========================================

@description('Maximum file size in MB for virus scanning')
@minValue(1)
@maxValue(1000)
param maxFileSizeMB int = 200

@description('Maximum concurrent background scanning workers')
@minValue(1)
@maxValue(20)
param maxConcurrentWorkers int = 4

@description('ASP.NET Core environment')
@allowed([
  'Development'
  'Staging'
  'Production'
])
param aspNetCoreEnvironment string = 'Production'

// ========================================
// Authentication Parameters (Entra ID / Azure AD)
// ========================================

@description('Enable authentication via Container Apps EasyAuth (Entra ID)')
param enableAuthentication bool = true

@description('Azure AD tenant ID (required if authentication is enabled)')
param aadTenantId string = tenant().tenantId

@description('Azure AD client ID / application ID (required if authentication is enabled). Leave empty for no authentication.')
param aadClientId string = ''

@description('Azure AD audience for token validation (defaults to client ID if not specified)')
param aadAudience string = ''

// ========================================
// Log Analytics Parameters
// ========================================

@description('Existing Log Analytics workspace resource ID (leave empty to create new)')
@metadata({
  example: '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.OperationalInsights/workspaces/{workspaceName}'
})
param existingLogAnalyticsWorkspaceId string = ''

@description('Log retention in days (used only when creating new)')
@minValue(30)
@maxValue(730)
param logRetentionDays int = 30

// ========================================
// Application Insights Parameters
// ========================================

@description('Existing Application Insights resource ID (leave empty to create new)')
@metadata({
  example: '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Insights/components/{appInsightsName}'
})
param existingApplicationInsightsId string = ''

@description('Enable Application Insights for telemetry and monitoring (disable = no telemetry)')
param enableApplicationInsights bool = true

@description('Name of the Application Insights resource (leave empty to auto-generate, used only when creating new)')
param applicationInsightsName string = ''

@description('Application Insights data retention in days (used only when creating new)')
@minValue(30)
@maxValue(730)
param appInsightsRetentionDays int = 90

@description('Application Insights daily data cap in GB (0 = no cap, used only when creating new)')
@minValue(0)
param appInsightsDailyCapGB int = 0

@description('Disable IP masking in Application Insights for detailed telemetry (used only when creating new)')
param appInsightsDisableIpMasking bool = false

// ========================================
// Malware Detection Alert Parameters
// ========================================

@description('Enable alerts when malware is detected')
param enableMalwareAlerts bool = true

@description('Resource ID of existing Action Group to notify on malware detection (leave empty to create from email)')
@metadata({
  example: '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Insights/actionGroups/{actionGroupName}'
})
param malwareAlertActionGroupId string = ''

@description('Email address for malware alerts (creates new Action Group if malwareAlertActionGroupId is empty)')
param malwareAlertEmail string = ''

@description('Alert threshold: number of malware detections to trigger alert')
@minValue(1)
param malwareAlertThreshold int = 1

@description('Time window for malware alert evaluation (in minutes)')
@minValue(1)
@maxValue(1440)
param malwareAlertEvaluationMinutes int = 5

// ========================================
// Tags
// ========================================

@description('Tags to apply to all resources')
param tags object = {
  Environment: environmentName
  Application: applicationName
  ManagedBy: 'Bicep'
}

// ========================================
// Variables
// ========================================

var resourceSuffix = uniqueString(resourceGroup().id, applicationName, environmentName)
var acrName = !empty(containerRegistryName) ? containerRegistryName : 'acr${applicationName}${resourceSuffix}'
var storageAcctName = !empty(storageAccountName) ? storageAccountName : 'st${applicationName}${resourceSuffix}'
var logAnalyticsName = 'log-${applicationName}-${environmentName}-${resourceSuffix}'
var appInsightsName = !empty(applicationInsightsName) ? applicationInsightsName : 'appi-${applicationName}-${environmentName}'
var containerEnvironmentName = 'cae-${applicationName}-${environmentName}'
var actionGroupName = 'ag-${applicationName}-${environmentName}-malware'
var actionGroupShortName = 'MalwareAlert'

// Determine if using existing resources
var useExistingContainerEnv = !empty(existingContainerEnvironmentId) || useExistingManagedEnvironment

// Determine final action group ID (existing > create from email > none)
var shouldCreateActionGroup = enableMalwareAlerts && empty(malwareAlertActionGroupId) && !empty(malwareAlertEmail)

// ========================================
// Module Deployments
// ========================================

// Deploy Action Group for malware alerts (create from email if needed)
module actionGroup './modules/action-group.bicep' = if (shouldCreateActionGroup) {
  name: 'deploy-action-group'
  params: {
    actionGroupName: actionGroupName
    shortName: actionGroupShortName
    emailAddress: malwareAlertEmail
    emailReceiverName: 'Security Team'
    tags: tags
  }
}

// Deploy Log Analytics Workspace
module logAnalytics './modules/log-analytics.bicep' = if (!useExistingContainerEnv || enableApplicationInsights) {
  name: 'deploy-log-analytics'
  params: {
    existingWorkspaceId: existingLogAnalyticsWorkspaceId
    workspaceName: logAnalyticsName
    location: location
    retentionInDays: logRetentionDays
    tags: tags
  }
}

// Deploy Application Insights (linked to Log Analytics workspace)
module applicationInsights './modules/app-insights.bicep' = if (enableApplicationInsights) {
  name: 'deploy-app-insights'
  params: {
    existingApplicationInsightsId: existingApplicationInsightsId
    applicationInsightsName: appInsightsName
    location: location
    logAnalyticsWorkspaceId: enableApplicationInsights ? logAnalytics.outputs.workspaceId : ''
    applicationType: 'web'
    retentionInDays: appInsightsRetentionDays
    dailyDataCapInGB: appInsightsDailyCapGB
    disableIpMasking: appInsightsDisableIpMasking
    samplingPercentage: 100
    tags: tags
  }
}

// Deploy Azure Container Registry (only if using local ACR)
module containerRegistry './modules/acr.bicep' = if (useLocalAcr) {
  name: 'deploy-acr'
  params: {
    existingRegistryId: existingContainerRegistryId
    registryName: acrName
    location: location
    sku: containerRegistrySku
    adminUserEnabled: enableAcrAdminUser
    tags: tags
  }
}

// Deploy Storage Account with Azure Files
module storage './modules/storage.bicep' = {
  name: 'deploy-storage'
  params: {
    existingStorageAccountId: existingStorageAccountId
    storageAccountName: storageAcctName
    location: location
    fileShareName: clamavFileShareName
    fileShareQuotaGiB: fileShareQuotaGiB
    tags: tags
  }
}

// Deploy Container Apps Managed Environment (skip if using existing)
module containerEnvironment './modules/container-environment.bicep' = if (!useExistingContainerEnv) {
  name: 'deploy-container-environment'
  params: {
    environmentName: containerEnvironmentName
    location: location
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
    storageAccountName: storage.outputs.storageAccountName
    fileShareName: storage.outputs.fileShareName
    storageMountName: 'clamav-db-storage'
    zoneRedundant: enableZoneRedundancy
    tags: tags
  }
}

// Reference existing Container Apps Managed Environment if specified
resource existingEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' existing = if (useExistingContainerEnv) {
  name: !empty(existingContainerEnvironmentId) ? last(split(existingContainerEnvironmentId, '/')) : existingManagedEnvironmentName
}

// Add storage mount to existing environment using module
module existingEnvironmentStorageMount './modules/existing-env-storage-mount.bicep' = if (useExistingContainerEnv) {
  name: 'add-storage-mount-existing-env'
  params: {
    existingEnvironmentName: !empty(existingContainerEnvironmentId) ? last(split(existingContainerEnvironmentId, '/')) : existingManagedEnvironmentName
    storageAccountName: storage.outputs.storageAccountName
    fileShareName: storage.outputs.fileShareName
    storageMountName: 'clamav-db-storage'
  }
}

// Deploy Container App
module containerApp './modules/container-app.bicep' = {
  name: 'deploy-container-app'
  params: {
    containerAppName: containerAppName
    location: location
    environmentId: useExistingContainerEnv ? existingEnvironment.id : containerEnvironment.outputs.environmentId
    storageMountName: 'clamav-db-storage'
    containerImage: useLocalAcr ? '${containerRegistry.outputs.loginServer}/${applicationName}:${containerImageTag}' : (contains(dockerHubImage, ':') ? dockerHubImage : '${dockerHubImage}:${containerImageTag}')
    containerRegistryServer: useLocalAcr ? containerRegistry.outputs.loginServer : ''
    useManagedIdentityForRegistry: useLocalAcr
    cpuCores: containerCpuCores
    memorySize: containerMemory
    minReplicas: minReplicas
    maxReplicas: maxReplicas
    maxFileSizeMB: maxFileSizeMB
    maxConcurrentWorkers: maxConcurrentWorkers
    aspNetCoreEnvironment: aspNetCoreEnvironment
    enableAuthentication: enableAuthentication
    aadTenantId: aadTenantId
    aadClientId: aadClientId
    aadAudience: aadAudience
    applicationInsightsConnectionString: enableApplicationInsights ? applicationInsights.outputs.connectionString : ''
    tags: tags
  }
}

// Assign AcrPull role to Container App managed identity (only if using local ACR)
var acrPullRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
var roleAssignmentName = guid(containerRegistry.name, containerApp.name, acrPullRoleDefinitionId)

resource acrPullRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (useLocalAcr) {
  name: roleAssignmentName
  scope: resourceGroup()
  properties: {
    roleDefinitionId: acrPullRoleDefinitionId
    principalId: containerApp.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

// Create log-based alert for malware detections (optional)
// Uses scheduled query rules to query Application Insights for MalwareDetected events
resource malwareDetectionAlert 'Microsoft.Insights/scheduledQueryRules@2021-08-01' = if (enableMalwareAlerts && enableApplicationInsights && (!empty(malwareAlertActionGroupId) || shouldCreateActionGroup)) {
  name: '${applicationName}-malware-detection-alert'
  location: location
  tags: tags
  properties: {
    displayName: 'Malware Detection Alert - ${applicationName}'
    description: 'Alert triggered when malware is detected by the ClamAV scanning API'
    severity: 2 // Warning (0=Critical, 1=Error, 2=Warning, 3=Informational, 4=Verbose)
    enabled: true
    scopes: [
      applicationInsights.outputs.applicationInsightsId
    ]
    evaluationFrequency: 'PT${malwareAlertEvaluationMinutes}M'
    windowSize: 'PT${malwareAlertEvaluationMinutes}M'
    criteria: {
      allOf: [
        {
          query: 'customEvents | where name == "MalwareDetected" | summarize count()'
          timeAggregation: 'Count'
          operator: 'GreaterThanOrEqual'
          threshold: malwareAlertThreshold
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        !empty(malwareAlertActionGroupId) ? malwareAlertActionGroupId : actionGroup.outputs.actionGroupId
      ]
    }
    autoMitigate: true
  }
}

// ========================================
// Outputs
// ========================================

@description('Container App FQDN')
output containerAppFqdn string = containerApp.outputs.fqdn

@description('Container App URL')
output containerAppUrl string = 'https://${containerApp.outputs.fqdn}'

@description('Container Registry login server (empty if using Docker Hub)')
output containerRegistryLoginServer string = useLocalAcr ? containerRegistry.outputs.loginServer : 'Using Docker Hub: ${dockerHubImage}'

@description('Container Registry name (N/A if using Docker Hub)')
output containerRegistryName string = useLocalAcr ? containerRegistry.outputs.registryName : 'N/A - using Docker Hub'

@description('Storage account name')
output storageAccountName string = storage.outputs.storageAccountName

@description('File share name')
output fileShareName string = storage.outputs.fileShareName

@description('Container Apps environment name')
output containerEnvironmentName string = useExistingContainerEnv ? (!empty(existingContainerEnvironmentId) ? last(split(existingContainerEnvironmentId, '/')) : existingManagedEnvironmentName) : containerEnvironmentName

@description('Container App name')
output containerAppName string = containerApp.outputs.containerAppName

@description('Log Analytics workspace name')
output logAnalyticsWorkspaceName string = !empty(existingLogAnalyticsWorkspaceId) ? 'Using existing workspace' : (useExistingContainerEnv ? 'Using existing environment workspace' : logAnalyticsName)

@description('Container App system-assigned managed identity principal ID')
output containerAppPrincipalId string = containerApp.outputs.principalId

@description('Application Insights connection string (if enabled)')
output applicationInsightsConnectionString string = enableApplicationInsights ? applicationInsights.outputs.connectionString : 'Not enabled'

@description('Application Insights instrumentation key (if enabled)')
output applicationInsightsInstrumentationKey string = enableApplicationInsights ? applicationInsights.outputs.instrumentationKey : 'Not enabled'

@description('Application Insights name (if enabled)')
output applicationInsightsName string = enableApplicationInsights ? applicationInsights.outputs.applicationInsightsName : 'Not enabled'

@description('Application Insights App ID (if enabled)')
output applicationInsightsAppId string = enableApplicationInsights ? applicationInsights.outputs.appId : 'Not enabled'

@description('Action Group ID for malware alerts (if configured)')
output actionGroupId string = !empty(malwareAlertActionGroupId) ? malwareAlertActionGroupId : (shouldCreateActionGroup ? actionGroup.outputs.actionGroupId : 'Not configured')

@description('Malware detection alert name (if enabled)')
output malwareAlertName string = (enableMalwareAlerts && enableApplicationInsights && (!empty(malwareAlertActionGroupId) || shouldCreateActionGroup)) ? malwareDetectionAlert.name : 'Not enabled'

@description('Malware detection alert ID (if enabled)')
output malwareAlertId string = (enableMalwareAlerts && enableApplicationInsights && (!empty(malwareAlertActionGroupId) || shouldCreateActionGroup)) ? malwareDetectionAlert.id : 'Not enabled'
