module HTLVB.RegistrationForm.Admin.Server.Main

open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Npgsql
open System

type WestEuropeTimeProvider() =
    inherit TimeProvider()

    override _.LocalTimeZone with get () = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time")

[<EntryPoint>]
let main args =

    let builder = WebApplication.CreateBuilder(args)

    builder.Services.AddSingleton<TimeProvider>(WestEuropeTimeProvider()) |> ignore

    let pgsqlConnectionString =
        builder.Configuration.GetConnectionString("Pgsql")
        |> Option.ofObj
        |> Option.defaultWith (fun () -> failwith "Can't find \"ConnectionStrings:Pgsql\"")
    builder.Services.AddSingleton(NpgsqlDataSourceBuilder(pgsqlConnectionString).EnableDynamicJson().Build()) |> ignore
    builder.Services.AddSingleton<IEventStore, PgsqlEventStore>() |> ignore

    let publicRegistrationBaseUrl = builder.Configuration.GetRequiredSection("PublicRegistration:BaseUrl").Value
    let publicRegistrationSettings : Controllers.PublicRegistrationSettings = {
        // trailing slash, so that relative event keys are appended instead of replacing the last segment
        BaseUrl = Uri(publicRegistrationBaseUrl.TrimEnd('/') + "/")
    }
    builder.Services.AddSingleton(publicRegistrationSettings) |> ignore

    builder.Services.AddAuthentication()
        .AddJwtBearer(fun options ->
            builder.Configuration.GetSection("Oidc").Bind options
        ) |> ignore

    builder.Services.AddTransient<IClaimsTransformation>(fun _ ->
        new KeycloakRolesClaimsTransformation "registration-form"
    ) |> ignore

    builder.Services.AddAuthorization(fun options ->
        options.AddPolicy("ReadEvent", fun policy ->
            policy.RequireRole("registrationform-eventreader") |> ignore
        )
        let isEventEditor (ctx: AuthorizationHandlerContext) =
            ctx.User.IsInRole("registrationform-eventadmin") ||
            match ctx.Resource :?> Domain.EventData |> Option.ofObj, UserClaims.tryGetMailAddress ctx.User with
            | Some event, Some mailAddress ->
                event.EditorIds
                |> Seq.exists (fun v -> String.Equals(v, mailAddress, StringComparison.InvariantCultureIgnoreCase))
            | _ -> false
        options.AddPolicy("ReadEventRegistrations", fun policy ->
            policy.RequireAssertion(isEventEditor) |> ignore
        )
        options.AddPolicy("UpdateEventRegistrations", fun policy ->
            policy.RequireAssertion(isEventEditor) |> ignore
        )
        options.AddPolicy("CreateEvent", fun policy ->
            policy.RequireAssertion(fun ctx ->
                ctx.User.IsInRole("registrationform-eventwriter") ||
                ctx.User.IsInRole("registrationform-eventadmin")
            ) |> ignore
        )
        options.AddPolicy("UpdateEvent", fun policy ->
            policy.RequireAssertion(isEventEditor) |> ignore
        )
    ) |> ignore

    builder.Services.AddControllers() |> ignore

    let app = builder.Build()

    app.UseHttpsRedirection() |> ignore

    app.UseFileServer() |> ignore
    app.MapFallbackToFile("index.html") |> ignore

    app.UseAuthentication() |> ignore
    app.UseAuthorization() |> ignore

    app.MapControllers() |> ignore

    app.Run()

    0
