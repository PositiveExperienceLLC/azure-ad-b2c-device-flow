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
    sharedPlan: false
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
    sharedPlan: true
    appService: {
      properties: {
        siteConfig: {
          alwaysOn: true
        }
      }
    }
  }
}

var resourceName = '${serviceCode}-${environmentCode}-${uniqueString(resourceGroup().id)}'

// in prd the app runs on the WorkspaceService plan (B1, one always-on instance), so the plan below is only created in dev.
// Pending sign-ins are held in memory, which relies on that single instance.
var sharedPlanId = resourceId('wss-${environmentCode}', 'Microsoft.Web/serverfarms', 'wss-${environmentCode}-${uniqueString('/subscriptions/${subscription().subscriptionId}/resourceGroups/wss-${environmentCode}')}')

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

resource appServicePlan 'Microsoft.Web/serverfarms@2021-02-01' = if (!environment.sharedPlan) {
  name: resourceName
  location: location
  kind: 'app'
  sku: environment.appServicePlan.sku
}

var appServiceProperties = union(environment.appService.properties, {
  serverFarmId: environment.sharedPlan ? sharedPlanId : appServicePlan.id
  httpsOnly: true
  storageAccountRequired: false
  siteConfig: {
    ftpsState: 'Disabled'
    http20Enabled: true
    netFrameworkVersion: 'v8.0'
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

var settings = {
  APPINSIGHTS_INSTRUMENTATIONKEY: appInsights.properties.InstrumentationKey
  AzureWebJobsStorage: '@Microsoft.KeyVault(SecretUri=${secretStorageConnectionString.properties.secretUri})'
  FUNCTIONS_EXTENSION_VERSION: '~4'
  FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
  'Config:AppId': '3a43ce17-dc74-43ef-9e2d-070716155cc1'
  'Config:AppSecret': ''
  'Config:Tenant': 'cliriob2c'
  'Config:RedirectUri': 'https://login.clir.io/api/authorization_callback'
  'Config:SignInPolicy': 'B2C_1A_SIGNUP_SIGNIN'
  'Config:VerificationUri': 'https://login.clir.io'
  WEBSITE_RUN_FROM_PACKAGE: '1'
}

resource appSettings 'Microsoft.Web/sites/config@2021-02-01' = {
  name: 'appsettings'
  parent: appService
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

module roleAssignmentsKeyVaultSite './roleAssignmentsKeyVault.bicep' = {
  name: 'roleAssignmentsKeyVaultSite'
  scope: resourceGroup()
  params: {
    keyVaultName: keyVault.name
    objectId: appService.identity.principalId
  }
}

output appHostName string = appService.name
