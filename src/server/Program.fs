module HTLVB.RegistrationForm.Server.Main

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open System

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    let appConfig = AppConfig.fromEnvironment builder.Configuration
    builder.Services.AddSingleton(appConfig) |> ignore
    builder.Services.AddSingleton(TimeProvider.System) |> ignore

    builder.Services.AddControllers() |> ignore

    let app = builder.Build()

    app.UseHttpsRedirection() |> ignore

    app.MapControllers() |> ignore

    app.Run()

    0