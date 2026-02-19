param location string
param tags object
param resourceToken string
param functionAppName string
param documentsStorageAccountName string
param cuEndpoint string
param cuAnalyzerId string
param cuInputContainer string
param cuInputPrefix string
param cuOutputPrefix string
param cuAnalyzePath string
param cuPollIntervalSeconds int

var abbrs = loadJsonContent('./abbreviations.json')
var functionStorageName = toLower('stfunc${resourceToken}')
var resolvedFunctionAppName = empty(functionAppName)
  ? '${abbrs.functionApps}bim-standards-ingest-${resourceToken}'
  : functionAppName

resource functionStorage 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: functionStorageName
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

resource functionPlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: '${resolvedFunctionAppName}-plan'
  location: location
  tags: tags
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {
    reserved: false
  }
}

resource functionApp 'Microsoft.Web/sites@2022-09-01' = {
  name: resolvedFunctionAppName
  location: location
  tags: tags
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    httpsOnly: true
    serverFarmId: functionPlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'AzureWebJobsStorage__accountName'
          value: functionStorage.name
        }
        {
          name: 'AzureWebJobsStorage__credential'
          value: 'managedidentity'
        }
        {
          name: 'DocumentsStorage__accountName'
          value: documentsStorageAccountName
        }
        {
          name: 'DocumentsStorage__credential'
          value: 'managedidentity'
        }
        {
          name: 'CU_ENDPOINT'
          value: cuEndpoint
        }
        {
          name: 'CU_ANALYZER_ID'
          value: cuAnalyzerId
        }
        {
          name: 'CU_INPUT_CONTAINER'
          value: cuInputContainer
        }
        {
          name: 'CU_INPUT_PREFIX'
          value: cuInputPrefix
        }
        {
          name: 'CU_OUTPUT_PREFIX'
          value: cuOutputPrefix
        }
        {
          name: 'CU_ANALYZE_PATH'
          value: cuAnalyzePath
        }
        {
          name: 'CU_POLL_INTERVAL_SECONDS'
          value: string(cuPollIntervalSeconds)
        }
      ]
    }
  }
}

output functionAppName string = functionApp.name
output functionIdentityPrincipalId string = functionApp.identity.principalId
output functionStorageAccountName string = functionStorage.name
