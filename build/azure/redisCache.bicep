@description('The name of the cache')
param name string

@description('The location to deploy the cache')
param location string = resourceGroup().location

// Azure Managed Redis; classic Basic/Standard caches can no longer be created (blocked from Oct 2026)
resource cache 'Microsoft.Cache/redisEnterprise@2025-07-01' = {
  name: name
  location: location
  sku: {
    name: 'Balanced_B0'
  }
  properties: {
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource database 'Microsoft.Cache/redisEnterprise/databases@2025-07-01' = {
  name: 'default'
  parent: cache
  properties: {
    clientProtocol: 'Encrypted'
    port: 10000
    // single endpoint: the service enumerates keys with GetServer(GetEndPoints().Single())
    clusteringPolicy: 'NoCluster'
    evictionPolicy: 'VolatileLRU'
    accessKeysAuthentication: 'Enabled'
  }
}

output hostName string = cache.properties.hostName
