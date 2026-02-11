// Module to add storage mount to existing Container Apps environment
// This is in a separate module to handle cross-resource-group scope properly

@description('Name of the existing Container Apps environment')
param existingEnvironmentName string

@description('Storage account name')
param storageAccountName string

@description('File share name')
param fileShareName string

@description('Name for the storage mount')
param storageMountName string = 'clamav-db-storage'

// Storage account (in current resource group)
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

// Existing Container Apps environment (in current or different resource group)
resource environment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: existingEnvironmentName
}

// Add storage mount to the existing environment
resource storageMount 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  name: storageMountName
  parent: environment
  properties: {
    azureFile: {
      accountName: storageAccount.name
      accountKey: storageAccount.listKeys().keys[0].value
      shareName: fileShareName
      accessMode: 'ReadWrite'
    }
  }
}

@description('Name of the storage mount')
output storageMountName string = storageMount.name
