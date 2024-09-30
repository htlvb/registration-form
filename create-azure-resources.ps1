$SubscriptionName = "Pay-As-You-Go"
$ServerAppName = "RegistrationForm-Server"

"=== Logging in"
az account set --name $SubscriptionName
$SubscriptionId = az account show --query id -o tsv

"=== Creating server app registration"
$MSGraphId = az ad sp list --filter "displayname eq 'Microsoft Graph'" --query "[].appId" -o tsv
$ServerAppRequiredResourceAccesses = New-TemporaryFile
@"
[{
    "resourceAppId": "$MSGraphId",
    "resourceAccess": [
        {
            "id": "$(az ad sp show --id $MSGraphId --query "appRoles[?value=='Mail.Send'].id | [0]" -o tsv)",
            "type": "Role"
        }
   ]
}]
"@ | Set-Content $ServerAppRequiredResourceAccesses
$ServerApp = az ad app create --display-name $ServerAppName | ConvertFrom-Json
az ad app update --id $ServerApp.appId --required-resource-accesses @$ServerAppRequiredResourceAccesses

Remove-Item $ServerAppRequiredResourceAccesses

$ServerAppCredentials = az ad app credential reset --id $ServerApp.appId --display-name Initial --years 2 --append | ConvertFrom-Json

"=== Giving admin consent to server and client app permissions"
"!!! Login with admin account !!!"
az login --use-device-code --allow-no-subscriptions -o none
az ad app permission admin-consent --id $ServerApp.appId
az logout
az account set --name $SubscriptionName

Write-Host "# Showing summary"
Write-Host "* Tenant id: $($ServerAppCredentials.tenant)"
Write-Host "* Server app id: $($ServerAppCredentials.appId)"
Write-Host "* Server app secret: $($ServerAppCredentials.password)"

<#
"=== Deleting resources"
az ad app delete --id (az ad app list --filter "displayName eq '$ServerAppName'" --query "[].id" -o tsv)
#>
