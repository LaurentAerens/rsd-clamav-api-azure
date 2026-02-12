// Azure Container Registry Module
// Based on Azure Verified Modules pattern

@description('Name of the Azure Container Registry')
param registryName string

@description('Location for the Container Registry')
param location string

@description('SKU for the Container Registry')
@allowed([
  'Basic'
  'Standard'
  'Premium'
])
param sku string = 'Standard'

@description('Enable admin user')
param adminUserEnabled bool = false

@description('Tags to apply to the registry')
param tags object = {}

// Deploy Azure Container Registry using AVM module
module containerRegistry 'br/public:avm/res/container-registry/registry:0.7.0' = {
  name: 'acr-deployment'
  params: {
    name: registryName
    location: location
    acrSku: sku
    acrAdminUserEnabled: adminUserEnabled
    tags: tags
    publicNetworkAccess: 'Enabled'
    networkRuleBypassOptions: 'AzureServices'
  }
}

@description('Resource ID of the Container Registry')
output registryId string = containerRegistry.outputs.resourceId

@description('Login server for the Container Registry')
output loginServer string = containerRegistry.outputs.loginServer

@description('Name of the Container Registry')
output registryName string = containerRegistry.outputs.name
