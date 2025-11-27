// app-service.bicep - App Service with Plan
// Deploys App Service in UKSOUTH with S1 SKU

@description('Location for App Service')
param location string

@description('Resource prefix for naming')
param resourcePrefix string

@description('Managed Identity resource ID')
param managedIdentityId string

@description('Managed Identity Client ID')
param managedIdentityClientId string

var appServicePlanName = toLower('asp-${resourcePrefix}')
var appServiceName = toLower('app-${resourcePrefix}')

// App Service Plan - S1 SKU to avoid cold start
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'S1'
    tier: 'Standard'
    capacity: 1
  }
  kind: 'linux'
  properties: {
    reserved: true // Required for Linux
  }
}

// App Service
resource appService 'Microsoft.Web/sites@2023-01-01' = {
  name: appServiceName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityId}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'ManagedIdentityClientId'
          value: managedIdentityClientId
        }
        {
          name: 'AZURE_CLIENT_ID'
          value: managedIdentityClientId
        }
      ]
    }
  }
}

// Outputs
output appServiceName string = appService.name
output appServiceUrl string = 'https://${appService.properties.defaultHostName}'
output managedIdentityPrincipalId string = managedIdentityClientId
