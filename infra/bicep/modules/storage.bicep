// Azure Storage Account Module for ClamAV database persistence
// Creates storage account with Azure Files share

@description('Name of the storage account')
param storageAccountName string

@description('Location for the storage account')
param location string

@description('Name of the file share for ClamAV database')
param fileShareName string = 'clamav-database'

@description('Quota for the file share in GB')
@minValue(1)
@maxValue(100)
param fileShareQuotaGiB int = 5

@description('Tags to apply to the storage account')
param tags object = {}

// Deploy Storage Account using AVM module
module storageAccount 'br/public:avm/res/storage/storage-account:0.14.3' = {
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

@description('Resource ID of the storage account')
output storageAccountId string = storageAccount.outputs.resourceId

@description('Name of the storage account')
output storageAccountName string = storageAccount.outputs.name

@description('Name of the file share')
output fileShareName string = fileShareName

@description('Primary endpoints for the storage account')
output primaryEndpoints object = {
  blob: 'https://${storageAccount.outputs.name}.blob.${environment().suffixes.storage}'
  file: 'https://${storageAccount.outputs.name}.file.${environment().suffixes.storage}'
}
