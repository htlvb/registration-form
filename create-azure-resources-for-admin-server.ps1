$SubscriptionName = "Pay-As-You-Go"
$ClientAppName = "RegistrationForm-AdminClient"
$ServerAppName = "RegistrationForm-AdminServer"

"=== Logging in"
az account set --name $SubscriptionName

"=== Creating admin server app registration"
$ServerAppAppRoles = New-TemporaryFile
@(
    @{
        allowedMemberTypes = @("User")
        description = "All event editors can edit others' events."
        displayName = "All event editors"
        isEnabled = $true
        value = "Event.Write.All"
    }
    @{
        allowedMemberTypes = @("User")
        description = "Event editors can create events."
        displayName = "Event editors"
        isEnabled = $true
        value = "Event.Write"
    }
    @{
        allowedMemberTypes = @("User")
        description = "Event viewers can view others' events."
        displayName = "Event viewers"
        isEnabled = $true
        value = "Event.Read"
    }
) | ConvertTo-Json | Set-Content $ServerAppAppRoles
$ServerAppApiScopes = New-TemporaryFile
@{
    oauth2PermissionScopes = @(
        @{
            adminConsentDescription = "Allows the app to access server app API endpoints."
            adminConsentDisplayName = "Access API"
            id = "$(New-Guid)"
            isEnabled = $true
            type = "User"
            userConsentDescription = $null
            userConsentDisplayName = $null
            value = "Api.Access"
        }
    )
} | ConvertTo-Json | Set-Content $ServerAppApiScopes
$ServerApp = az ad app create --display-name $ServerAppName --app-roles @$ServerAppAppRoles | ConvertFrom-Json
az ad app update --id $ServerApp.appId --identifier-uris "api://$($ServerApp.appId)"
az ad app update --id $ServerApp.appId --set api=@$ServerAppApiScopes
Remove-Item $ServerAppAppRoles, $ServerAppApiScopes

$ServerAppCredentials = az ad app credential reset --id $ServerApp.appId --display-name Initial --years 2 --append | ConvertFrom-Json

az ad sp create --id $ServerApp.appId -o none
$ServerAppResourceId = az ad sp show --id $ServerApp.appId --query "id" -o tsv
$AppRoleAssigments = @(
    [PSCustomObject]@{PrincipalId = az ad group show --group GrpLehrer --query id -o tsv; AppRoleName = "Event.Read"}
    [PSCustomObject]@{PrincipalId = az ad group show --group GrpLehrer --query id -o tsv; AppRoleName = "Event.Write"}
    [PSCustomObject]@{PrincipalId = az ad user show --id EGGJ@htlvb.at --query id -o tsv; AppRoleName = "Event.Write.All"}
    [PSCustomObject]@{PrincipalId = az ad user show --id KLIL@htlvb.at --query id -o tsv; AppRoleName = "Event.Write.All"}
    [PSCustomObject]@{PrincipalId = az ad user show --id BRAE@htlvb.at --query id -o tsv; AppRoleName = "Event.Write.All"}
)
foreach ($Item in $AppRoleAssigments) {
    $AppRoleAssignment = New-TemporaryFile

    @{
        principalId = $Item.PrincipalId
        resourceId = $ServerAppResourceId
        appRoleId = az ad app show --id $ServerApp.appId --query "appRoles[?value=='$($Item.AppRoleName)'].id | [0]" -o tsv
    } | ConvertTo-Json | Set-Content $AppRoleAssignment
    az rest --method POST --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$ServerAppResourceId/appRoleAssignedTo" --headers "Content-Type=application/json" --body @$AppRoleAssignment -o none
    Remove-Item $AppRoleAssignment
}

"=== Creating admin client app registration"
$ClientAppRequiredResourceAccesses = New-TemporaryFile
ConvertTo-Json -Depth 10 @(
    @{
        resourceAppId = $ServerApp.appId
        resourceAccess = @(
            @{
                id = az ad sp show --id $ServerApp.appId --query "oauth2PermissionScopes[?value=='Api.Access'].id | [0]" -o tsv
                type = "Scope"
            }
        )
    }
) | Set-Content $ClientAppRequiredResourceAccesses
$ClientApp = az ad app create --display-name $ClientAppName `
    --sign-in-audience AzureADMyOrg `
    --required-resource-accesses @$ClientAppRequiredResourceAccesses `
    | ConvertFrom-Json
$ClientAppSpaRedirectUris = New-TemporaryFile
@{
    redirectUris = @(
        "https://localhost/authentication/login-callback"
        "https://admin.registration.htlvb.at/authentication/login-callback"
    )
} | ConvertTo-Json | Set-Content $ClientAppSpaRedirectUris
az ad app update --id $ClientApp.appId --set spa=@$ClientAppSpaRedirectUris

Remove-Item $ClientAppRequiredResourceAccesses, $ClientAppSpaRedirectUris

Write-Warning "TODO: Update ServerApiScope in appsettings.json to 'api://$($ServerApp.appId)/Api.Access'"

Write-Host "# Showing summary"
Write-Host "* Tenant id: $($ServerAppCredentials.tenant)"
Write-Host "* Server app id: $($ServerAppCredentials.appId)"
Write-Host "* Server app secret: $($ServerAppCredentials.password)"
Write-Host "* Client app id: $($ClientApp.appId)"

<#
"=== Deleting resources"
az ad app delete --id (az ad app list --filter "displayName eq '$ClientAppName'" --query "[].id" -o tsv)
az ad app delete --id (az ad app list --filter "displayName eq '$ServerAppName'" --query "[].id" -o tsv)
#>
