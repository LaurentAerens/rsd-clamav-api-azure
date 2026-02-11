using './main.bicep'

// ========================================
// Example Parameter File for ClamAV API Deployment
// ========================================
// Copy this file and customize for your environment (dev/staging/prod)
// Rename to: <environment>.bicepparam (e.g., dev.bicepparam, prod.bicepparam)

// ========================================
// REQUIRED PARAMETERS
// ========================================

// Environment name - used for resource naming and tagging
// Examples: 'dev', 'staging', 'prod'
param environmentName = 'dev'

// Azure region for all resources
// Examples: 'eastus', 'westeurope', 'westus2'
param location = 'eastus'

// Application name - used as prefix for resource names
// Must be 3-20 characters, alphanumeric only
param applicationName = 'clamav-api'

// ========================================
// AUTHENTICATION PARAMETERS
// ========================================
// If enableAuthentication is true, you must provide aadClientId
// See docs/azure-deployment.md for AAD app registration steps

// Enable Azure AD authentication via EasyAuth
param enableAuthentication = true

// Azure AD Client ID (Application ID from your app registration)
// REQUIRED if enableAuthentication = true
// Example: '12345678-1234-1234-1234-123456789abc'
param aadClientId = ''  // TODO: Replace with your Azure AD App Client ID

// Azure AD Tenant ID (defaults to current tenant)
// Can be found in Azure Portal > Azure Active Directory > Overview
// Example: '87654321-4321-4321-4321-cba987654321'
// param aadTenantId = '00000000-0000-0000-0000-000000000000'  // Uncomment and set if different from deployment tenant

// Azure AD Audience for token validation
// Usually same as aadClientId or 'api://{clientId}'
// Leave empty to default to aadClientId
// param aadAudience = 'api://12345678-1234-1234-1234-123456789abc'  // Uncomment if using custom audience

// ========================================
// EXISTING ENVIRONMENT (OPTIONAL)
// ========================================
// Set useExistingManagedEnvironment = true if you want to deploy
// to an existing Container Apps environment instead of creating a new one

// Use existing Container Apps environment instead of creating new
param useExistingManagedEnvironment = false

// Name of existing environment (required if useExistingManagedEnvironment = true)
// param existingManagedEnvironmentName = 'cae-shared-prod'  // Uncomment if using existing environment

// Resource group of existing environment (defaults to deployment resource group)
// param existingManagedEnvironmentResourceGroup = 'rg-shared-infrastructure'  // Uncomment if in different RG

// ========================================
// CONTAINER APP CONFIGURATION
// ========================================

// Container App name (defaults to '{applicationName}-{environmentName}')
// Uncomment to override
// param containerAppName = 'clamav-api-custom-name'

// Container image tag to deploy
// Set to specific version in production (e.g., '1.0.0', 'v2.3.1')
param containerImageTag = 'latest'

// Container resources
param containerCpuCores = '1.0'        // CPU cores: 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0
param containerMemory = '2.0Gi'        // Memory: must be 2x CPU (e.g., 0.5Gi, 1.0Gi, 2.0Gi, 4.0Gi)

// Scaling configuration
param minReplicas = 1                  // Minimum replicas (0 = scale to zero)
param maxReplicas = 5                  // Maximum replicas

// ========================================
// APPLICATION SETTINGS
// ========================================

// Maximum file size for virus scanning (in MB)
param maxFileSizeMB = 200

// Maximum concurrent background workers for async scanning
param maxConcurrentWorkers = 4

// ASP.NET Core environment
// Use 'Production' for prod, 'Staging' or 'Development' for non-prod
param aspNetCoreEnvironment = 'Development'

// ========================================
// AZURE CONTAINER REGISTRY
// ========================================

// Container Registry name (leave empty for auto-generated name)
// param containerRegistryName = 'myacrname'  // Uncomment to use custom name

// Container Registry SKU
// Basic: Dev/test, Standard: Production (recommended), Premium: Geo-replication
param containerRegistrySku = 'Standard'

// Enable admin user for ACR (useful for initial setup, disable in production)
param enableAcrAdminUser = false

// ========================================
// STORAGE CONFIGURATION
// ========================================

// Storage account name for ClamAV database (leave empty for auto-generated)
// param storageAccountName = 'stclamavprod'  // Uncomment to use custom name

// File share name for ClamAV virus database
param clamavFileShareName = 'clamav-database'

// File share quota in GB
param fileShareQuotaGiB = 5

// ========================================
// LOG ANALYTICS
// ========================================

// Log retention in days (only applies if creating new environment)
param logRetentionDays = 30

// ========================================
// ADVANCED OPTIONS
// ========================================

// Enable zone redundancy for Container Apps environment
// Requires Premium SKU and specific regions
// param enableZoneRedundancy = false  // Uncomment for production with HA requirements

// ========================================
// RESOURCE TAGS
// ========================================

param tags = {
  Environment: 'dev'
  Application: 'clamav-api'
  ManagedBy: 'Bicep'
  CostCenter: 'IT-Security'
  Owner: 'platform-team@example.com'
}
