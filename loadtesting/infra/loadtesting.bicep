// Standalone deployment template for Azure Load Testing

targetScope = 'resourceGroup'

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

@description('Name of the Azure Load Testing resource (leave empty to auto-generate)')
param loadTestingName string = ''

@description('Optional description for the Azure Load Testing resource')
param loadTestDescription string = ''

@description('Enable AVM telemetry for the module')
param enableModuleTelemetry bool = true

@description('Enable system-assigned managed identity for Azure Load Testing')
param enableSystemAssignedIdentity bool = false

@description('Tags to apply to all resources')
param tags object = {
  Environment: environmentName
  Application: applicationName
  ManagedBy: 'Bicep'
}

var resolvedLoadTestingName = !empty(loadTestingName)
  ? loadTestingName
  : 'lt-${applicationName}-${environmentName}'

module loadTesting './modules/load-testing.bicep' = {
  params: {
    loadTestingName: resolvedLoadTestingName
    location: location
    loadTestDescription: loadTestDescription
    enableTelemetry: enableModuleTelemetry
    enableSystemAssignedIdentity: enableSystemAssignedIdentity
    tags: tags
  }
}

@description('Azure Load Testing resource ID')
output loadTestingResourceId string = loadTesting.outputs.loadTestingResourceId

@description('Azure Load Testing resource name')
output loadTestingResourceName string = loadTesting.outputs.loadTestingResourceName

@description('Azure Load Testing location')
output loadTestingLocation string = loadTesting.outputs.loadTestingLocation
