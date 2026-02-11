// ========================================
// Application Insights Module
// ========================================
// This module deploys a workspace-based Application Insights resource
// that is linked to an existing Log Analytics workspace.

targetScope = 'resourceGroup'

// ========================================
// Parameters
// ========================================

@description('Name of the Application Insights resource')
param applicationInsightsName string

@description('Azure region for the Application Insights resource')
param location string

@description('Resource ID of the Log Analytics workspace to link to')
param logAnalyticsWorkspaceId string

@description('Application type for Application Insights')
@allowed([
  'web'
  'other'
])
param applicationType string = 'web'

@description('Tags to apply to the Application Insights resource')
param tags object = {}

@description('Data retention in days (30-730)')
@minValue(30)
@maxValue(730)
param retentionInDays int = 90

@description('Daily data cap in GB (0 = no cap)')
@minValue(0)
param dailyDataCapInGB int = 0

@description('Disable IP masking for detailed telemetry')
param disableIpMasking bool = false

@description('Sampling percentage (0-100)')
@minValue(0)
@maxValue(100)
param samplingPercentage int = 100

// ========================================
// Resources
// ========================================

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
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

// Configure daily cap if specified
resource dailyCapConfig 'Microsoft.Insights/components/currentbillingfeatures@2015-05-01' = if (dailyDataCapInGB > 0) {
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
output applicationInsightsId string = applicationInsights.id

@description('Application Insights name')
output applicationInsightsName string = applicationInsights.name

@description('Application Insights connection string')
output connectionString string = applicationInsights.properties.ConnectionString

@description('Application Insights instrumentation key')
output instrumentationKey string = applicationInsights.properties.InstrumentationKey

@description('Application Insights App ID')
output appId string = applicationInsights.properties.AppId

@description('Log Analytics workspace ID linked to Application Insights')
output workspaceId string = logAnalyticsWorkspaceId
