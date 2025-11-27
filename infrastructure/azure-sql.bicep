// azure-sql.bicep - Azure SQL Database with Entra ID authentication
// MCAPS Governance compliant: Azure AD-Only Authentication enabled

@description('Location for SQL resources')
param location string

@description('Resource prefix for naming')
param resourcePrefix string

@description('Entra ID Admin Object ID')
param adminObjectId string

@description('Entra ID Admin Login (UPN)')
param adminLogin string

@description('Managed Identity Principal ID for database access')
param managedIdentityPrincipalId string

var sqlServerName = toLower('sql-${resourcePrefix}')
var databaseName = 'Northwind'

// SQL Server with Entra ID only authentication
resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: adminLogin
      sid: adminObjectId
      principalType: 'User'
      tenantId: subscription().tenantId
    }
  }
}

// Allow Azure services to access SQL Server
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Database - Basic tier for development
resource database 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648 // 2GB
  }
}

// Outputs
output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = database.name
