using './loadtesting.bicep'

// Source-controlled template parameters for standalone Azure Load Testing deployment
// Copy this file locally if you want environment-specific values.
param environmentName = 'dev'
param location = 'westeurope'
param applicationName = 'cav'

// Leave empty to auto-generate: lt-{applicationName}-{environmentName}
param loadTestingName = ''
param loadTestDescription = 'Load testing resource for ClamAV JSON endpoint performance experiments.'

// Keep disabled unless needed for RBAC scenarios
param enableSystemAssignedIdentity = false

param tags = {
  Environment: environmentName
  Application: 'clamav-api'
  ManagedBy: 'Bicep'
  Workload: 'loadtesting'
}
