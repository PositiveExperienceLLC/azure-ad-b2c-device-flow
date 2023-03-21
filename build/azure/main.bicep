@description('The service code')
param serviceCode string = 'das'

@description('The location to deploy the service resources')
param location string = resourceGroup().location

@description('The environment for which this instance is being deployed.')
@allowed([
  'dev'
  'prd'
])
param environmentCode string = 'dev'
var environment = environmentConfiguration[environmentCode]
var environmentConfiguration = {
  dev: {
    slots: false
    appService: {
      properties: {
        siteConfig: {
          alwaysOn: false
        }
      }
    }
    appServicePlan: {
      sku: {
        name: 'Y1'
      }
    }
  }
  prd: {
    slots: true
    appService: {
      properties: {
        siteConfig: {
          alwaysOn: true
        }
      }
    }
    appServicePlan: {
      sku: {
        name: 'S1'
      }
    }
  }
}

var resourceName = '${serviceCode}-${environmentCode}-${uniqueString(resourceGroup().id)}'

var sharedResources = {
  resourceGroup: {
    name: 'shared-${environmentCode}'
  }
  redisCache: {
    name: 'sitescan-devicecode-${environmentCode}'
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: resourceName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${listKeys(storage.id, storage.apiVersion).keys[0].value}'
resource storage 'Microsoft.Storage/storageAccounts@2021-06-01' = {
  name: take(replace(resourceName, '-', ''), 24)
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
}

var redisConnectionString = '${sharedResources.redisCache.name}.redis.cache.windows.net,abortConnect=false,ssl=true,password=${redisCache.listKeys().primaryKey}'
resource redisCache 'Microsoft.Cache/Redis@2021-06-01' existing = {
  name: sharedResources.redisCache.name
  scope: resourceGroup(sharedResources.resourceGroup.name)
}

resource appServicePlan 'Microsoft.Web/serverfarms@2021-02-01' = {
  name: resourceName
  location: location
  kind: 'app'
  sku: environment.appServicePlan.sku
}

var appServiceProperties = union(environment.appService.properties, {
  serverFarmId: appServicePlan.id
  httpsOnly: true
  storageAccountRequired: false
  siteConfig: {
    ftpsState: 'Disabled'
    http20Enabled: true
  }
})

resource appService 'Microsoft.Web/sites@2021-02-01' = {
  name: resourceName
  location: location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: appServiceProperties
}

resource slotSwap 'Microsoft.Web/sites/slots@2021-02-01' = if (environment.slots) {
  name: 'staging'
  location: location
  parent: appService
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: appServiceProperties
}

resource slotLastGood 'Microsoft.Web/sites/slots@2021-02-01' = if (environment.slots) {
  name: 'last-good'
  location: location
  parent: appService
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: appServiceProperties
}

var settings = {
  APPINSIGHTS_INSTRUMENTATIONKEY: appInsights.properties.InstrumentationKey
  AzureWebJobsStorage: '@Microsoft.KeyVault(SecretUri=${secretStorageConnectionString.properties.secretUri})'
  FUNCTIONS_EXTENSION_VERSION: '~4'
  FUNCTIONS_WORKER_RUNTIME: 'dotnet'
  'Config:AppId': '3a43ce17-dc74-43ef-9e2d-070716155cc1'
  'Config:AppSecret': ''
  'Config:Tenant': 'cliriob2c'
  'Config:RedirectUri': 'https://login.clir.io/api/authorization_callback'
  'Config:SignInPolicy': 'B2C_1A_SIGNUP_SIGNIN'
  'Config:VerificationUri': 'https://login.clir.io'
  'Config:Redis:Connection': '@Microsoft.KeyVault(SecretUri=${secretRedisConnectionString.properties.secretUri})'
  WEBSITE_RUN_FROM_PACKAGE: '1'
}

resource appSettings 'Microsoft.Web/sites/config@2021-02-01' = {
  name: 'appsettings'
  parent: appService
  properties: settings
}

resource appSettingsSlotSwap 'Microsoft.Web/sites/slots/config@2021-02-01' = if (environment.slots) {
  name: 'appsettings'
  parent: slotSwap
  properties: settings
}

resource appSettingsSlotLastGood 'Microsoft.Web/sites/slots/config@2021-02-01' = if (environment.slots) {
  name: 'appsettings'
  parent: slotLastGood
  properties: settings
}

resource keyVault 'Microsoft.KeyVault/vaults@2021-06-01-preview' = {
  name: take(resourceName, 24)
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    accessPolicies: []
    enableRbacAuthorization: true
  }
}

resource secretStorageConnectionString 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' = {
  name: 'AzureWebJobsStorage'
  parent: keyVault
  properties: {
    value: storageConnectionString
  }
}

resource secretRedisConnectionString 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' = {
  name: 'SharedRedisConnectionString'
  parent: keyVault
  properties: {
    value: redisConnectionString
  }
}

module roleAssignmentsKeyVaultSite './roleAssignmentsKeyVault.bicep' = {
  name: 'roleAssignmentsKeyVaultSite'
  scope: resourceGroup()
  params: {
    keyVaultName: keyVault.name
    objectId: appService.identity.principalId
  }  
}

module roleAssignmentsKeyVaultSwap './roleAssignmentsKeyVault.bicep' = if (environment.slots) {
  name: 'roleAssignmentsKeyVaultSwap'
  scope: resourceGroup()
  params: {
    keyVaultName: keyVault.name
    objectId: environment.slots ? slotSwap.identity.principalId : ''
  }  
}

module roleAssignmentsKeyVaultLastGood './roleAssignmentsKeyVault.bicep' = if (environment.slots) {
  name: 'roleAssignmentsKeyVaultLastGood'
  scope: resourceGroup()
  params: {
    keyVaultName: keyVault.name
    objectId: environment.slots ? slotLastGood.identity.principalId : ''
  }  
}

output appHostName string = appService.name
