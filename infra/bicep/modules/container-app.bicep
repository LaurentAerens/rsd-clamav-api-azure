// Azure Container App Module for ClamAV Scanning API
// Based on Azure Verified Modules pattern with EasyAuth configuration

@description('Name of the Container App')
param containerAppName string

@description('Location for the Container App')
param location string

@description('Resource ID of the Container Apps managed environment')
param environmentId string

@description('Name of the storage mount for ClamAV database')
param storageMountName string

@description('Container image to deploy (e.g., myacr.azurecr.io/clamav-api:latest)')
param containerImage string

@description('Container registry server (e.g., myacr.azurecr.io)')
param containerRegistryServer string

@description('Use managed identity for container registry authentication')
param useManagedIdentityForRegistry bool = true

@description('Container registry username (if not using managed identity)')
@secure()
param containerRegistryUsername string = ''

@description('Container registry password (if not using managed identity)')
@secure()
param containerRegistryPassword string = ''

@description('CPU cores for the container (e.g., 0.5, 1.0, 2.0)')
param cpuCores string = '1.0'

@description('Memory for the container (e.g., 1.0Gi, 2.0Gi)')
param memorySize string = '2.0Gi'

@description('Minimum number of replicas')
@minValue(0)
@maxValue(30)
param minReplicas int = 1

@description('Maximum number of replicas')
@minValue(1)
@maxValue(30)
param maxReplicas int = 5

@description('Maximum file size in MB for scanning')
@minValue(1)
@maxValue(1000)
param maxFileSizeMB int = 200

@description('Maximum concurrent background workers')
@minValue(1)
@maxValue(20)
param maxConcurrentWorkers int = 4

@description('Enable Azure AD authentication via EasyAuth')
param enableAuthentication bool = true

@description('Azure AD tenant ID for authentication')
param aadTenantId string = ''

@description('Azure AD client ID (application ID) for authentication')
param aadClientId string = ''

@description('Azure AD audience (typically same as client ID or api://{clientId})')
param aadAudience string = ''

@description('ASP.NET Core environment')
@allowed([
  'Development'
  'Staging'
  'Production'
])
param aspNetCoreEnvironment string = 'Production'

@description('Tags to apply to the Container App')
param tags object = {}

// Prepare registry configuration
var registries = useManagedIdentityForRegistry ? [
  {
    server: containerRegistryServer
    identity: 'system'
  }
] : (empty(containerRegistryUsername) ? [] : [
  {
    server: containerRegistryServer
    username: containerRegistryUsername
    passwordSecretRef: 'registry-password'
  }
])

// Deploy Container App using AVM module
module containerApp 'br/public:avm/res/app/container-app:0.11.0' = {
  name: 'containerApp-deployment'
  params: {
    name: containerAppName
    location: location
    environmentResourceId: environmentId
    tags: tags
    
    // Managed identity for ACR pull
    managedIdentities: {
      systemAssigned: useManagedIdentityForRegistry
    }
    
    // Container configuration
    containers: [
      {
        name: 'clamav-api'
        image: containerImage
        resources: {
          cpu: json(cpuCores)
          memory: memorySize
        }
        env: [
          {
            name: 'ASPNETCORE_ENVIRONMENT'
            value: aspNetCoreEnvironment
          }
          {
            name: 'ASPNETCORE_URLS'
            value: 'http://0.0.0.0:8080'
          }
          {
            name: 'MAX_FILE_SIZE_MB'
            value: string(maxFileSizeMB)
          }
          {
            name: 'BackgroundProcessing__MaxConcurrentWorkers'
            value: string(maxConcurrentWorkers)
          }
          {
            name: 'CLAMD_HOST'
            value: '127.0.0.1'
          }
          {
            name: 'CLAMD_PORT'
            value: '3310'
          }
        ]
        volumeMounts: [
          {
            volumeName: 'clamav-database'
            mountPath: '/var/lib/clamav'
          }
        ]
        probes: [
          {
            type: 'Liveness'
            httpGet: {
              path: '/healthz'
              port: 8080
              scheme: 'HTTP'
            }
            initialDelaySeconds: 30
            periodSeconds: 30
            timeoutSeconds: 5
            failureThreshold: 3
          }
          {
            type: 'Readiness'
            httpGet: {
              path: '/healthz'
              port: 8080
              scheme: 'HTTP'
            }
            initialDelaySeconds: 60
            periodSeconds: 10
            timeoutSeconds: 5
            failureThreshold: 3
          }
          {
            type: 'Startup'
            httpGet: {
              path: '/healthz'
              port: 8080
              scheme: 'HTTP'
            }
            initialDelaySeconds: 1
            periodSeconds: 10
            timeoutSeconds: 5
            failureThreshold: 10
          }
        ]
      }
    ]
    
    // Volume configuration - Azure Files mount
    volumes: [
      {
        name: 'clamav-database'
        storageType: 'AzureFile'
        storageName: storageMountName
      }
    ]
    
    // Registry credentials
    registries: registries
    secrets: useManagedIdentityForRegistry ? null : (empty(containerRegistryPassword) ? null : {
      secureList: [
        {
          name: 'registry-password'
          value: containerRegistryPassword
        }
      ]
    })
    
    // Ingress configuration
    ingressExternal: true
    ingressTargetPort: 8080
    ingressTransport: 'http'
    ingressAllowInsecure: false
    
    // Scaling configuration
    scaleMinReplicas: minReplicas
    scaleMaxReplicas: maxReplicas
    scaleRules: [
      {
        name: 'http-scaling-rule'
        http: {
          metadata: {
            concurrentRequests: '20'
          }
        }
      }
      {
        name: 'cpu-scaling-rule'
        custom: {
          type: 'cpu'
          metadata: {
            type: 'Utilization'
            value: '70'
          }
        }
      }
    ]
  }
}

// Configure EasyAuth if enabled
resource containerAppAuth 'Microsoft.App/containerApps/authConfigs@2024-03-01' = if (enableAuthentication && !empty(aadClientId)) {
  name: '${containerAppName}/current'
  dependsOn: [
    containerApp
  ]
  properties: {
    platform: {
      enabled: true
    }
    globalValidation: {
      unauthenticatedClientAction: 'Return401'
      redirectToProvider: 'azureactivedirectory'
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        registration: {
          openIdIssuer: 'https://sts.windows.net/${aadTenantId}/v2.0'
          clientId: aadClientId
        }
        validation: {
          allowedAudiences: empty(aadAudience) ? [
            aadClientId
          ] : [
            aadAudience
          ]
        }
      }
    }
    login: {
      tokenStore: {
        enabled: true
      }
    }
  }
}

@description('Resource ID of the Container App')
output containerAppId string = containerApp.outputs.resourceId

@description('Name of the Container App')
output containerAppName string = containerApp.outputs.name

@description('FQDN of the Container App')
output fqdn string = containerApp.outputs.fqdn

@description('System-assigned managed identity principal ID')
output principalId string = useManagedIdentityForRegistry ? containerApp.outputs.systemAssignedMIPrincipalId : ''
