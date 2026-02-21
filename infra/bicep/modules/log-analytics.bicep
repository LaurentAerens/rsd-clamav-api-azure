// Log Analytics Workspace Module for Container Apps
// Based on Azure Verified Modules pattern
// Supports using existing workspace or creating new

@description('Existing Log Analytics workspace resource ID (leave empty to create new)')
param existingWorkspaceId string = ''

@description('Name of the Log Analytics workspace (used only when creating new)')
param workspaceName string = ''

@description('Location for the Log Analytics workspace (used only when creating new)')
param location string = resourceGroup().location

@description('Retention period in days (30-730) (used only when creating new)')
@minValue(30)
@maxValue(730)
param retentionInDays int = 30

@description('Tags to apply to the workspace (used only when creating new)')
param tags object = {}

// Determine whether to use existing or create new
var useExisting = !empty(existingWorkspaceId)

// Reference existing workspace
resource existingWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = if (useExisting) {
  name: last(split(existingWorkspaceId, '/'))
}

// Deploy Log Analytics Workspace using AVM module (only if not using existing)
module logAnalyticsWorkspace 'br/public:avm/res/operational-insights/workspace:0.9.1' = if (!useExisting) {
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
output workspaceId string = useExisting ? existingWorkspace.id : logAnalyticsWorkspace.outputs.resourceId

@description('Customer ID (workspace ID) for the Log Analytics workspace')
output customerId string = useExisting ? existingWorkspace.properties.customerId : logAnalyticsWorkspace.outputs.logAnalyticsWorkspaceId

@description('Name of the Log Analytics workspace')
output workspaceName string = useExisting ? existingWorkspace.name : logAnalyticsWorkspace.outputs.name
