module HTLVB.RegistrationForm.Server.Main

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Graph
open Microsoft.Identity.Web
open Npgsql
open System

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    let appConfig = AppConfig.fromEnvironment builder.Configuration
    builder.Services.AddSingleton(appConfig) |> ignore
    builder.Services.AddSingleton(TimeProvider.System) |> ignore
    let pgsqlConnectionString =
        builder.Configuration.GetConnectionString("Pgsql")
        |> Option.ofObj
        |> Option.defaultWith (fun () -> failwith "Can't find \"ConnectionStrings:Pgsql\"")
    builder.Services.AddSingleton(NpgsqlDataSource.Create(pgsqlConnectionString)) |> ignore
    builder.Services.AddSingleton<IRegistrationStore, PgsqlRegistrationStore>() |> ignore
    builder.Services.AddSingleton<MSGraphMailSettings>({
        MailboxUserName =  appConfig.MailConfig.MailboxUserName
        Sender =  appConfig.MailConfig.Sender
        BccRecipient =  appConfig.MailConfig.BccRecipient
    }) |> ignore
    builder.Services.AddScoped<IBookingConfirmationSender, MSGraphBookingConfirmationSender>() |> ignore

    builder.Services
        .AddMicrosoftIdentityWebApiAuthentication(builder.Configuration)
        .EnableTokenAcquisitionToCallDownstreamApi()
        .AddInMemoryTokenCaches()
        .AddMicrosoftGraphAppOnly(fun v -> new GraphServiceClient(v)) |> ignore

    builder.Services.AddControllers() |> ignore

    let app = builder.Build()

    app.UseHttpsRedirection() |> ignore

    app.MapControllers() |> ignore

    app.Run()

    0