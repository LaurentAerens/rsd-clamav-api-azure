// Azure Container Registry Module
// Based on Azure Verified Modules pattern
// Supports using existing registry or creating new

@description('Existing Container Registry resource ID (leave empty to create new)')
param existingRegistryId string = ''

@description('Name of the Azure Container Registry (used only when creating new)')
param registryName string = ''

@description('Location for the Container Registry (used only when creating new)')
param location string = resourceGroup().location

@description('SKU for the Container Registry (used only when creating new)')
@allowed([
  'Basic'
  'Standard'
  'Premium'
])
param sku string = 'Standard'

@description('Enable admin user (used only when creating new)')
param adminUserEnabled bool = false

@description('Tags to apply to the registry (used only when creating new)')
param tags object = {}

// Determine whether to use existing or create new
var useExisting = !empty(existingRegistryId)

// Reference existing Container Registry
resource existingRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = if (useExisting) {
  name: last(split(existingRegistryId, '/'))
}

// Deploy Azure Container Registry using AVM module (only if not using existing)
module containerRegistry 'br/public:avm/res/container-registry/registry:0.7.0' = if (!useExisting) {
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
output registryId string = useExisting ? existingRegistry.id : containerRegistry.outputs.resourceId

@description('Login server for the Container Registry')
output loginServer string = useExisting ? existingRegistry.properties.loginServer : containerRegistry.outputs.loginServer

@description('Name of the Container Registry')
output registryName string = useExisting ? existingRegistry.name : containerRegistry.outputs.name
