// main.bicep - Main deployment template for Expense Management System
// Deploys: Resource Group resources, App Service, Managed Identity, Azure SQL
// Location: UKSOUTH (except GenAI which uses swedencentral)

@description('Base name for all resources')
param baseName string = 'expensemgmt'

@description('Location for resources')
param location string = 'uksouth'

@description('Enable GenAI resources deployment')
param deployGenAI bool = false

@description('SQL Admin Object ID (Entra ID)')
param adminObjectId string

@description('SQL Admin Login (Entra ID UPN)')
param adminLogin string

// Generate unique suffix for resource names
var uniqueSuffix = toLower(uniqueString(resourceGroup().id))
var resourcePrefix = toLower('${baseName}-${uniqueSuffix}')

// Deploy Managed Identity first
module managedIdentity 'managed-identity.bicep' = {
  name: 'managedIdentityDeployment'
  params: {
    location: location
    baseName: baseName
    uniqueSuffix: uniqueSuffix
  }
}

// Deploy App Service
module appService 'app-service.bicep' = {
  name: 'appServiceDeployment'
  params: {
    location: location
    resourcePrefix: resourcePrefix
    managedIdentityId: managedIdentity.outputs.managedIdentityId
    managedIdentityClientId: managedIdentity.outputs.managedIdentityClientId
  }
}

// Deploy Azure SQL
module azureSql 'azure-sql.bicep' = {
  name: 'azureSqlDeployment'
  params: {
    location: location
    resourcePrefix: resourcePrefix
    adminObjectId: adminObjectId
    adminLogin: adminLogin
    managedIdentityPrincipalId: managedIdentity.outputs.managedIdentityPrincipalId
  }
}

// Deploy GenAI resources conditionally
module genai 'genai.bicep' = if (deployGenAI) {
  name: 'genaiDeployment'
  params: {
    baseName: baseName
    uniqueSuffix: uniqueSuffix
    managedIdentityPrincipalId: managedIdentity.outputs.managedIdentityPrincipalId
  }
}

// Outputs
output appServiceName string = appService.outputs.appServiceName
output appServiceUrl string = appService.outputs.appServiceUrl
output managedIdentityName string = managedIdentity.outputs.managedIdentityName
output managedIdentityClientId string = managedIdentity.outputs.managedIdentityClientId
output managedIdentityPrincipalId string = managedIdentity.outputs.managedIdentityPrincipalId
output sqlServerName string = azureSql.outputs.sqlServerName
output sqlServerFqdn string = azureSql.outputs.sqlServerFqdn
output databaseName string = azureSql.outputs.databaseName

// GenAI outputs (conditional)
output openAIEndpoint string = deployGenAI ? genai.outputs.openAIEndpoint : ''
output openAIModelName string = deployGenAI ? genai.outputs.openAIModelName : ''
output openAIName string = deployGenAI ? genai.outputs.openAIName : ''
output searchEndpoint string = deployGenAI ? genai.outputs.searchEndpoint : ''
output searchName string = deployGenAI ? genai.outputs.searchName : ''
