module HTLVB.RegistrationForm.Admin.Server.Main

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

    builder.Services.AddControllers() |> ignore

    let app = builder.Build()

    app.UseHttpsRedirection() |> ignore

    app.UseAuthorization() |> ignore
    app.MapControllers() |> ignore

    app.Run()

    0
