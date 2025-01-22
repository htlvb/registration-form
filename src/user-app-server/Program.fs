module HTLVB.RegistrationForm.Server.Main

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Graph
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
    let mailConfig = builder.Configuration.GetRequiredSection("GraphMail")
    builder.Services.AddSingleton<MSGraphMailSettings>({
        MailboxUserName = mailConfig.GetRequiredSection("Mailbox").Value
        Sender = { Name = mailConfig.GetRequiredSection("SenderName").Value; MailAddress = mailConfig.GetRequiredSection("SenderAddress").Value }
        BccRecipients =
            mailConfig.GetRequiredSection("BccRecipients").GetChildren()
            |> Seq.map (fun v ->
                let mailUser: Domain.MailUser = { Name = v.GetRequiredSection("Name").Value; MailAddress = v.GetRequiredSection("Address").Value }
                mailUser
            )
            |> Seq.toList
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

    app.UseFileServer() |> ignore
    app.MapFallbackToFile("index.html") |> ignore

    app.MapControllers() |> ignore

    app.Run()

    0