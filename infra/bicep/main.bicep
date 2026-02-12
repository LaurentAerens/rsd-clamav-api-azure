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
// Azure Container Registry Parameters
// ========================================

@description('Name of the Azure Container Registry (leave empty to auto-generate)')
param containerRegistryName string = ''

@description('Container Registry SKU')
@allowed([
  'Basic'
  'Standard'
  'Premium'
])
param containerRegistrySku string = 'Standard'

@description('Enable admin user for Container Registry (useful for initial setup)')
param enableAcrAdminUser bool = false

// ========================================
// Container Apps Environment Parameters
// ========================================

@description('Use an existing Container Apps managed environment instead of creating a new one')
param useExistingManagedEnvironment bool = false

@description('Name of existing Container Apps managed environment (required if useExistingManagedEnvironment is true)')
param existingManagedEnvironmentName string = ''

@description('Resource group of existing managed environment (defaults to current resource group)')
param existingManagedEnvironmentResourceGroup string = resourceGroup().name

@description('Enable zone redundancy for Container Apps environment')
param enableZoneRedundancy bool = false

// ========================================
// Storage Parameters
// ========================================

@description('Name of the storage account for ClamAV database (leave empty to auto-generate)')
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
// Authentication Parameters (Azure AD EasyAuth)
// ========================================

@description('Enable Azure AD authentication via Container Apps EasyAuth')
param enableAuthentication bool = true

@description('Azure AD tenant ID (required if authentication is enabled)')
param aadTenantId string = tenant().tenantId

@description('Azure AD client ID / application ID (required if authentication is enabled)')
param aadClientId string = ''

@description('Azure AD audience for token validation (defaults to client ID if not specified)')
param aadAudience string = ''

// ========================================
// Log Analytics Parameters
// ========================================

@description('Log retention in days')
@minValue(30)
@maxValue(730)
param logRetentionDays int = 30

// ========================================
// Application Insights Parameters
// ========================================

@description('Enable Application Insights for telemetry and monitoring')
param enableApplicationInsights bool = true

@description('Name of the Application Insights resource (leave empty to auto-generate)')
param applicationInsightsName string = ''

@description('Application Insights data retention in days')
@minValue(30)
@maxValue(730)
param appInsightsRetentionDays int = 90

@description('Application Insights daily data cap in GB (0 = no cap)')
@minValue(0)
param appInsightsDailyCapGB int = 0

@description('Disable IP masking in Application Insights for detailed telemetry')
param appInsightsDisableIpMasking bool = false

// ========================================
// Malware Detection Alert Parameters
// ========================================

@description('Enable alerts when malware is detected')
param enableMalwareAlerts bool = true

@description('Resource ID of the Action Group to notify on malware detection')
@metadata({
  example: '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Insights/actionGroups/{actionGroupName}'
})
param malwareAlertActionGroupId string = ''

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

// ========================================
// Module Deployments
// ========================================

// Deploy Log Analytics Workspace (skip if using existing environment)
module logAnalytics './modules/log-analytics.bicep' = if (!useExistingManagedEnvironment) {
  name: 'deploy-log-analytics'
  params: {
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
    applicationInsightsName: appInsightsName
    location: location
    logAnalyticsWorkspaceId: !useExistingManagedEnvironment ? logAnalytics.outputs.workspaceId : ''
    applicationType: 'web'
    retentionInDays: appInsightsRetentionDays
    dailyDataCapInGB: appInsightsDailyCapGB
    disableIpMasking: appInsightsDisableIpMasking
    samplingPercentage: 100
    tags: tags
  }
}

// Deploy Azure Container Registry
module containerRegistry './modules/acr.bicep' = {
  name: 'deploy-acr'
  params: {
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
    storageAccountName: storageAcctName
    location: location
    fileShareName: clamavFileShareName
    fileShareQuotaGiB: fileShareQuotaGiB
    tags: tags
  }
}

// Deploy Container Apps Managed Environment (skip if using existing)
module containerEnvironment './modules/container-environment.bicep' = if (!useExistingManagedEnvironment) {
  name: 'deploy-container-environment'
  params: {
    environmentName: containerEnvironmentName
    location: location
    logAnalyticsWorkspaceId: !useExistingManagedEnvironment ? logAnalytics.outputs.workspaceId : ''
    storageAccountName: storage.outputs.storageAccountName
    fileShareName: storage.outputs.fileShareName
    storageMountName: 'clamav-db-storage'
    zoneRedundant: enableZoneRedundancy
    tags: tags
  }
}

// Reference existing Container Apps Managed Environment if specified
resource existingEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' existing = if (useExistingManagedEnvironment) {
  name: existingManagedEnvironmentName
  scope: resourceGroup(existingManagedEnvironmentResourceGroup)
}

// Add storage mount to existing environment using module
module existingEnvironmentStorageMount './modules/existing-env-storage-mount.bicep' = if (useExistingManagedEnvironment) {
  name: 'add-storage-mount-existing-env'
  scope: resourceGroup(existingManagedEnvironmentResourceGroup)
  params: {
    existingEnvironmentName: existingManagedEnvironmentName
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
    environmentId: useExistingManagedEnvironment ? existingEnvironment.id : (!useExistingManagedEnvironment ? containerEnvironment.outputs.environmentId : '')
    storageMountName: 'clamav-db-storage'
    containerImage: '${containerRegistry.outputs.loginServer}/${applicationName}:${containerImageTag}'
    containerRegistryServer: containerRegistry.outputs.loginServer
    useManagedIdentityForRegistry: true
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

// Assign AcrPull role to Container App managed identity
var acrPullRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
var roleAssignmentName = guid(containerRegistry.name, containerApp.name, acrPullRoleDefinitionId)

resource acrPullRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: roleAssignmentName
  scope: resourceGroup()
  properties: {
    roleDefinitionId: acrPullRoleDefinitionId
    principalId: containerApp.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

// Create metric alert for malware detections (optional)
resource malwareDetectionAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (enableMalwareAlerts && enableApplicationInsights && !empty(malwareAlertActionGroupId)) {
  name: '${applicationName}-malware-detection-alert'
  location: 'global'
  tags: tags
  properties: {
    description: 'Alert triggered when malware is detected by the ClamAV scanning API'
    severity: 2 // Critical (0=Critical, 1=Error, 2=Warning, 3=Informational, 4=Verbose)
    enabled: true
    scopes: [
      enableApplicationInsights ? applicationInsights.outputs.applicationInsightsId : ''
    ]
    evaluationFrequency: 'PT1M' // Evaluate every minute
    windowSize: 'PT${malwareAlertEvaluationMinutes}M' // Time window (e.g., PT5M for 5 minutes)
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'MalwareDetections'
          metricName: 'MalwareDetections'
          operator: 'GreaterThanOrEqual'
          threshold: malwareAlertThreshold
          timeAggregation: 'Total'
          dimensions: []
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: malwareAlertActionGroupId
      }
    ]
  }
}

// ========================================
// Outputs
// ========================================

@description('Container App FQDN')
output containerAppFqdn string = containerApp.outputs.fqdn

@description('Container App URL')
output containerAppUrl string = 'https://${containerApp.outputs.fqdn}'

@description('Container Registry login server')
output containerRegistryLoginServer string = containerRegistry.outputs.loginServer

@description('Container Registry name')
output containerRegistryName string = containerRegistry.outputs.registryName

@description('Storage account name')
output storageAccountName string = storage.outputs.storageAccountName

@description('File share name')
output fileShareName string = storage.outputs.fileShareName

@description('Container Apps environment name')
output containerEnvironmentName string = useExistingManagedEnvironment ? existingManagedEnvironmentName : 'cae-${applicationName}-${environmentName}'

@description('Container App name')
output containerAppName string = containerApp.outputs.containerAppName

@description('Log Analytics workspace name (if created)')
output logAnalyticsWorkspaceName string = useExistingManagedEnvironment ? 'N/A - using existing environment' : logAnalyticsName

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

@description('Malware detection alert name (if enabled)')
output malwareAlertName string = (enableMalwareAlerts && enableApplicationInsights && !empty(malwareAlertActionGroupId)) ? malwareDetectionAlert.name : 'Not enabled'

@description('Malware detection alert ID (if enabled)')
output malwareAlertId string = (enableMalwareAlerts && enableApplicationInsights && !empty(malwareAlertActionGroupId)) ? malwareDetectionAlert.id : 'Not enabled'
