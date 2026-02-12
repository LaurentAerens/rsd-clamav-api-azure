// Log Analytics Workspace Module for Container Apps
// Based on Azure Verified Modules pattern

@description('Name of the Log Analytics workspace')
param workspaceName string

@description('Location for the Log Analytics workspace')
param location string

@description('Retention period in days (30-730)')
@minValue(30)
@maxValue(730)
param retentionInDays int = 30

@description('Tags to apply to the workspace')
param tags object = {}

// Deploy Log Analytics Workspace using AVM module
module logAnalyticsWorkspace 'br/public:avm/res/operational-insights/workspace:0.9.1' = {
  name: 'logAnalytics-deployment'
  params: {
    name: workspaceName
    location: location
    skuName: 'PerGB2018'
    dataRetention: retentionInDays
    tags: tags
  }
}

@description('Resource ID of the Log Analytics workspace')
output workspaceId string = logAnalyticsWorkspace.outputs.resourceId

@description('Customer ID (workspace ID) for the Log Analytics workspace')
output customerId string = logAnalyticsWorkspace.outputs.logAnalyticsWorkspaceId

@description('Name of the Log Analytics workspace')
output workspaceName string = logAnalyticsWorkspace.outputs.name
