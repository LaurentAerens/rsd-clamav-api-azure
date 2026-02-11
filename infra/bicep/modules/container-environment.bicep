// Azure Container Apps Managed Environment Module
// Based on Azure Verified Modules pattern

@description('Name of the Container Apps managed environment')
param environmentName string

@description('Location for the managed environment')
param location string

@description('Resource ID of the Log Analytics workspace')
param logAnalyticsWorkspaceId string

@description('Storage account name for Azure Files mount')
param storageAccountName string

@description('File share name for ClamAV database')
param fileShareName string

@description('Name for the storage mount')
param storageMountName string = 'clamav-db-storage'

@description('Enable zone redundancy')
param zoneRedundant bool = false

@description('Tags to apply to the environment')
param tags object = {}

// Get storage account key for file share access
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

// Deploy Container Apps Managed Environment using AVM module
module containerEnvironment 'br/public:avm/res/app/managed-environment:0.8.2' = {
  name: 'containerEnvironment-deployment'
  params: {
    name: environmentName
    location: location
    logAnalyticsWorkspaceResourceId: logAnalyticsWorkspaceId
    zoneRedundant: zoneRedundant
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
    infrastructureResourceGroupName: '${environmentName}-infra-rg'
    tags: tags
  }
}

// Add Azure Files storage to the environment
resource storageMount 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  name: '${environmentName}/${storageMountName}'
  dependsOn: [
    containerEnvironment
  ]
  properties: {
    azureFile: {
      accountName: storageAccountName
      accountKey: storageAccount.listKeys().keys[0].value
      shareName: fileShareName
      accessMode: 'ReadWrite'
    }
  }
}

@description('Resource ID of the Container Apps managed environment')
output environmentId string = containerEnvironment.outputs.resourceId

@description('Name of the Container Apps managed environment')
output environmentName string = containerEnvironment.outputs.name

@description('Default domain of the Container Apps managed environment')
output defaultDomain string = containerEnvironment.outputs.defaultDomain

@description('Name of the storage mount')
output storageMountName string = storageMountName
