// ========================================
// Application Insights Module
// ========================================
// This module deploys a workspace-based Application Insights resource
// that is linked to an existing Log Analytics workspace.
// Supports using existing Application Insights or creating new.

targetScope = 'resourceGroup'

// ========================================
// Parameters
// ========================================

@description('Existing Application Insights resource ID (leave empty to create new)')
param existingApplicationInsightsId string = ''

@description('Name of the Application Insights resource (used only when creating new)')
param applicationInsightsName string = ''

@description('Azure region for the Application Insights resource (used only when creating new)')
param location string = resourceGroup().location

@description('Resource ID of the Log Analytics workspace to link to (used only when creating new)')
param logAnalyticsWorkspaceId string = ''

@description('Application type for Application Insights')
@allowed([
  'web'
  'other'
])
param applicationType string = 'web'

@description('Tags to apply to the Application Insights resource (used only when creating new)')
param tags object = {}

@description('Data retention in days (30-730) (used only when creating new)')
@minValue(30)
@maxValue(730)
param retentionInDays int = 90

@description('Daily data cap in GB (0 = no cap) (used only when creating new)')
@minValue(0)
param dailyDataCapInGB int = 0

@description('Disable IP masking for detailed telemetry (used only when creating new)')
param disableIpMasking bool = false

@description('Sampling percentage (0-100) (used only when creating new)')
@minValue(0)
@maxValue(100)
param samplingPercentage int = 100

// ========================================
// Variables
// ========================================

var useExisting = !empty(existingApplicationInsightsId)

// ========================================
// Resources
// ========================================

// Reference existing Application Insights
resource existingAppInsights 'Microsoft.Insights/components@2020-02-02' existing = if (useExisting) {
  name: last(split(existingApplicationInsightsId, '/'))
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = if (!useExisting) {
  name: applicationInsightsName
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: applicationType
    WorkspaceResourceId: logAnalyticsWorkspaceId
    RetentionInDays: retentionInDays
    DisableIpMasking: disableIpMasking
    SamplingPercentage: samplingPercentage
    IngestionMode: 'LogAnalytics' // Workspace-based mode
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// Configure daily cap if specified (only for new resources)
resource dailyCapConfig 'Microsoft.Insights/components/currentbillingfeatures@2015-05-01' = if (!useExisting && dailyDataCapInGB > 0) {
  parent: applicationInsights
  name: 'Current'
  properties: {
    CurrentBillingFeatures: 'Basic'
    DataVolumeCap: {
      Cap: dailyDataCapInGB
      WarningThreshold: 90
      ResetTime: 0
    }
  }
}

// ========================================
// Outputs
// ========================================

@description('Application Insights resource ID')
output applicationInsightsId string = useExisting ? existingAppInsights.id : applicationInsights.id

@description('Application Insights name')
output applicationInsightsName string = useExisting ? existingAppInsights.name : applicationInsights.name

@description('Application Insights connection string')
output connectionString string = useExisting ? existingAppInsights.properties.ConnectionString : applicationInsights.properties.ConnectionString

@description('Application Insights instrumentation key')
output instrumentationKey string = useExisting ? existingAppInsights.properties.InstrumentationKey : applicationInsights.properties.InstrumentationKey

@description('Application Insights App ID')
output appId string = useExisting ? existingAppInsights.properties.AppId : applicationInsights.properties.AppId

@description('Log Analytics workspace ID linked to Application Insights')
output workspaceId string = useExisting ? existingAppInsights.properties.WorkspaceResourceId : logAnalyticsWorkspaceId
