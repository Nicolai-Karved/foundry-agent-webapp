targetScope = 'resourceGroup'

@minLength(1)
@maxLength(64)
@description('Name of the environment (e.g., dev, prod)')
param environmentName string = 'bim-compliance-agent'

@minLength(1)
@description('Primary location for all resources')
param location string = 'swedencentral'

@description('Default tags applied by Azure Policy (optional)')
param defaultTags object = {}

@description('AI Agent endpoint (auto-discovered by preprovision hook)')
param aiAgentEndpoint string = ''

@description('AI Agent ID (configured via azd env set AI_AGENT_ID)')
param aiAgentId string = ''

@description('Entra ID Client ID (set by azd hook)')
param entraSpaClientId string = ''

@description('Entra ID Tenant ID (set by azd hook or auto-detected)')
param entraTenantId string = tenant().tenantId

@description('Container image for web service (set by predeploy hook)')
param webImageName string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('Container App name for BIM compliance workload')
param webContainerAppName string = 'bim-compliance-agent'

var resourceToken = toLower(uniqueString(resourceGroup().id, environmentName, location))
var appTags = {
  'azd-env-name': environmentName
  'app-name': 'bim-compliance-agent'
}
var tags = union(defaultTags, appTags)

module infrastructure './main-infrastructure.bicep' = {
  name: 'bim-compliance-infrastructure'
  params: {
    location: location
    tags: tags
    resourceToken: resourceToken
  }
}

module app './main-app.bicep' = {
  name: 'bim-compliance-app'
  params: {
    location: location
    tags: tags
    resourceToken: resourceToken
    containerAppsEnvironmentId: infrastructure.outputs.containerAppsEnvironmentId
    containerRegistryName: infrastructure.outputs.containerRegistryName
    aiAgentEndpoint: aiAgentEndpoint
    aiAgentId: aiAgentId
    entraSpaClientId: entraSpaClientId
    entraTenantId: entraTenantId
    webImageName: webImageName
    webContainerAppName: webContainerAppName
  }
}

output AZURE_CONTAINER_REGISTRY_NAME string = infrastructure.outputs.containerRegistryName
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = infrastructure.outputs.containerRegistryLoginServer
output AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = infrastructure.outputs.containerAppsEnvironmentId
output AZURE_RESOURCE_GROUP_NAME string = resourceGroup().name
output AZURE_CONTAINER_APP_NAME string = app.outputs.webAppName
output WEB_ENDPOINT string = app.outputs.webEndpoint
output WEB_IDENTITY_PRINCIPAL_ID string = app.outputs.webIdentityPrincipalId
