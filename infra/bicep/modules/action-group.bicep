// Azure Monitor Action Group Module
// Creates an Action Group with email notification for malware detection alerts

@description('Name of the Action Group')
param actionGroupName string

@description('Location for the Action Group (must be global)')
param location string = 'global'

@description('Short name for the Action Group (max 12 characters)')
@maxLength(12)
param shortName string

@description('Email address to receive alerts')
param emailAddress string

@description('Display name for the email receiver')
param emailReceiverName string = 'Security Team'

@description('Tags to apply to the Action Group')
param tags object = {}

// Deploy Action Group
resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: actionGroupName
  location: location
  tags: tags
  properties: {
    groupShortName: shortName
    enabled: true
    emailReceivers: [
      {
        name: emailReceiverName
        emailAddress: emailAddress
        useCommonAlertSchema: true
      }
    ]
  }
}

@description('Resource ID of the Action Group')
output actionGroupId string = actionGroup.id

@description('Name of the Action Group')
output actionGroupName string = actionGroup.name
