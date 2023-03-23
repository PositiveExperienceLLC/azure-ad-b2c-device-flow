@description('Name of the Key Vault')
param keyVaultName string

@description('Object ID of the AAD identity. Must be a GUID.')
param objectId string

resource keyVault 'Microsoft.KeyVault/vaults@2021-06-01-preview' existing = {
  name: keyVaultName
}

resource keyVaultRoleSecretsUser 'Microsoft.Authorization/roleDefinitions@2018-01-01-preview' existing = {
  // Key Vault Secrets User
  name: '4633458b-17de-408a-b874-0445c86b69e6'
}

resource keyVaultSecretsUser 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(keyVault.id, keyVaultRoleSecretsUser.id, objectId)
  scope: keyVault
  properties: {
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultRoleSecretsUser.id
    principalId: objectId
  }
}
