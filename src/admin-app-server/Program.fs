module HTLVB.RegistrationForm.Admin.Server.Main

open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Identity.Web
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

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd")) |> ignore

    builder.Services.AddAuthorization(fun options ->
        options.AddPolicy("ReadEvent", fun policy ->
            policy.RequireRole("Event.Read") |> ignore
        )
        let isEventEditor (ctx: AuthorizationHandlerContext) =
            ctx.User.IsInRole("Event.Write.All") ||
            ctx.Resource :?> Domain.EventData
            |> Option.ofObj
            |> Option.exists (fun v ->
                v.EditorIds
                |> Seq.exists (fun v ->
                    String.Equals(v, ctx.User.Identity.Name, StringComparison.InvariantCultureIgnoreCase)
                )
            )
        options.AddPolicy("ReadEventRegistrations", fun policy ->
            policy.RequireAssertion(isEventEditor) |> ignore
        )
        options.AddPolicy("UpdateEventRegistrations", fun policy ->
            policy.RequireAssertion(isEventEditor) |> ignore
        )
        options.AddPolicy("CreateEvent", fun policy ->
            policy.RequireAssertion(fun ctx ->
                ctx.User.IsInRole("Event.Write") ||
                ctx.User.IsInRole("Event.Write.All")
            ) |> ignore
        )
        options.AddPolicy("UpdateEvent", fun policy ->
            policy.RequireAssertion(isEventEditor) |> ignore
        )
    ) |> ignore

    builder.Services.AddControllers() |> ignore

    let app = builder.Build()

    app.UseHttpsRedirection() |> ignore

    app.UseAuthentication() |> ignore
    app.UseAuthorization() |> ignore

    app.MapControllers() |> ignore

    app.Run()

    0
