using '../main.bicep'

// Development environment parameters
param environmentName = 'dev'
param location = 'westeurope'  // belgiumcentral not available for Log Analytics, using westeurope
param applicationName = 'cav'  // Shortened from 'clamav-api' for resource name length constraints

// Disable authentication for dev
param enableAuthentication = false
param aadClientId = ''

// Use Docker Hub image (default)
param useLocalAcr = false
param dockerHubImage = 'laurentaerenscodit/clamav-api:0.1.0-beta'
param containerImageTag = 'latest'

// Container resources - ClamAV requires 4GB minimum
param containerCpuCores = '2.0'
param containerMemory = '4.0Gi'

// Scaling
param minReplicas = 1
param maxReplicas = 5

// Application settings
param maxFileSizeMB = 200
param maxConcurrentWorkers = 4
param aspNetCoreEnvironment = 'Development'

// Storage configuration
param clamavFileShareName = 'clamav-database'
param fileShareQuotaGiB = 5

// Monitoring
param enableApplicationInsights = true
param appInsightsRetentionDays = 90

// Alerts
param enableMalwareAlerts = false
param malwareAlertActionGroupId = ''

// Tags
param tags = {
  Environment: 'dev'
  Application: 'clamav-api'
  ManagedBy: 'Bicep'
}
