// Azure Storage Account Module for ClamAV database persistence
// Creates storage account with Azure Files share OR references existing storage

@description('Existing storage account resource ID (leave empty to create new)')
param existingStorageAccountId string = ''

@description('Name of the storage account (used only when creating new)')
param storageAccountName string = ''

@description('Location for the storage account (used only when creating new)')
param location string = resourceGroup().location

@description('Name of the file share for ClamAV database')
param fileShareName string = 'clamav-database'

@description('Quota for the file share in GB')
@minValue(1)
@maxValue(100)
param fileShareQuotaGiB int = 5

@description('Tags to apply to the storage account (used only when creating new)')
param tags object = {}

// Determine whether to use existing or create new
var useExisting = !empty(existingStorageAccountId)
var effectiveStorageAccountName = useExisting ? last(split(existingStorageAccountId, '/')) : storageAccountName

// Reference existing storage account
resource existingStorageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = if (useExisting) {
  name: effectiveStorageAccountName
}

// Deploy Storage Account using AVM module (only if not using existing)
module storageAccount 'br/public:avm/res/storage/storage-account:0.14.3' = if (!useExisting) {
  name: 'storage-deployment'
  params: {
    name: storageAccountName
    location: location
    kind: 'StorageV2'
    skuName: 'Standard_LRS'
    allowBlobPublicAccess: false
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
    fileServices: {
      shares: [
        {
          name: fileShareName
          shareQuota: fileShareQuotaGiB
          enabledProtocols: 'SMB'
        }
      ]
    }
    tags: tags
  }
}

// Create file share in existing storage account if needed
resource existingStorageFileShareService 'Microsoft.Storage/storageAccounts/fileServices@2023-01-01' existing = if (useExisting) {
  parent: existingStorageAccount
  name: 'default'
}

resource fileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-01-01' = if (useExisting) {
  parent: existingStorageFileShareService
  name: fileShareName
  properties: {
    shareQuota: fileShareQuotaGiB
    enabledProtocols: 'SMB'
  }
}

@description('Resource ID of the storage account')
output storageAccountId string = useExisting ? existingStorageAccount.id : storageAccount.outputs.resourceId

@description('Name of the storage account')
output storageAccountName string = useExisting ? existingStorageAccount.name : storageAccount.outputs.name

@description('Name of the file share')
output fileShareName string = fileShareName

@description('Primary endpoints for the storage account')
output primaryEndpoints object = useExisting ? {
  blob: existingStorageAccount.properties.primaryEndpoints.blob
  file: existingStorageAccount.properties.primaryEndpoints.file
} : {
  blob: 'https://${storageAccount.outputs.name}.blob.${environment().suffixes.storage}'
  file: 'https://${storageAccount.outputs.name}.file.${environment().suffixes.storage}'
}
