// ========================================
// Azure Load Testing Module
// ========================================
// This module deploys an Azure Load Testing resource via AVM.

targetScope = 'resourceGroup'

@description('Name of the Azure Load Testing resource')
param loadTestingName string

@description('Azure region for the Azure Load Testing resource')
param location string = resourceGroup().location

@description('Optional description for the Azure Load Testing resource')
param loadTestDescription string = ''

@description('Enable AVM telemetry')
param enableTelemetry bool = true

@description('Enable system-assigned managed identity for the load testing resource')
param enableSystemAssignedIdentity bool = false

@description('Tags to apply to the Azure Load Testing resource')
param tags object = {}

module loadTesting 'br/public:avm/res/load-test-service/load-test:0.4.3' = {
  params: {
    name: loadTestingName
    location: location
    loadTestDescription: empty(loadTestDescription) ? null : loadTestDescription
    enableTelemetry: enableTelemetry
    managedIdentities: enableSystemAssignedIdentity ? {
      systemAssigned: true
    } : null
    tags: tags
  }
}

@description('Azure Load Testing resource ID')
output loadTestingResourceId string = loadTesting.outputs.resourceId

@description('Azure Load Testing resource name')
output loadTestingResourceName string = loadTesting.outputs.name

@description('Azure region used by Azure Load Testing')
output loadTestingLocation string = loadTesting.outputs.location
