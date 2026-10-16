namespace HTLVB.RegistrationForm.Admin.Server

open Microsoft.AspNetCore.Authentication
open System
open System.Security.Claims
open System.Text.Json.Nodes

type KeycloakRolesClaimsTransformation(clientName: string) =
    interface IClaimsTransformation with
        member _.TransformAsync(principal: ClaimsPrincipal) =
            task {
                match principal.Identity with
                | :? ClaimsIdentity as identity ->
                    match identity.FindFirst "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" |> Option.ofObj with
                    | Some nameIdentifier -> identity.AddClaim(Claim("oid", nameIdentifier.Value))
                    | None -> ()

                    match identity.FindFirst "resource_access" |> Option.ofObj with
                    | Some realmAccessClaim ->
                        let roles =
                            try
                                let realmAccess = JsonNode.Parse realmAccessClaim.Value
                                realmAccess.[clientName].["roles"].AsArray()
                                |> Seq.map _.GetValue<string>()
                            with _ -> Seq.empty
                        roles
                        |> Seq.iter (fun role ->
                            identity.AddClaim(Claim(ClaimTypes.Role, role))
                        )
                    | None -> ()
                | _ -> ()
                return principal
            }

module UserClaims =
    /// mail address identifies event editors
    let tryGetMailAddress (user: ClaimsPrincipal) =
        // "email" is mapped to `ClaimTypes.Email` unless inbound claim mapping is disabled
        [ ClaimTypes.Email; "email" ]
        |> List.tryPick (fun claimType -> user.FindFirst claimType |> Option.ofObj)
        |> Option.map _.Value
        |> Option.filter (String.IsNullOrWhiteSpace >> not)
