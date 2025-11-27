// managed-identity.bicep - User Assigned Managed Identity
// Creates identity for App Service to connect to Azure SQL and other services

@description('Location for the managed identity')
param location string

@description('Base name for resources')
param baseName string

@description('Unique suffix for naming')
param uniqueSuffix string

// Create a deterministic name using the unique suffix
var managedIdentityName = toLower('mid-${baseName}-${uniqueSuffix}')

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
  tags: {
    purpose: 'App Service Managed Identity for Expense Management System'
  }
}

// Outputs
output managedIdentityId string = managedIdentity.id
output managedIdentityName string = managedIdentity.name
output managedIdentityClientId string = managedIdentity.properties.clientId
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
