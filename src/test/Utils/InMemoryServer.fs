namespace HTLVB.RegistrationForm.Server.Tests.Utils

open HTLVB.RegistrationForm.Server
open HTLVB.RegistrationForm.Server.Tests
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Options

module InMemoryServer =
    open System.Reflection
    open System
    open Microsoft.Extensions.Time.Testing
    let start() = async {
        return! HostBuilder()
            .ConfigureWebHost(fun webBuilder ->
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(fun services ->
                        services.AddControllers().AddApplicationPart(Assembly.GetAssembly(typeof<Controllers.RegistrationController>)) |> ignore
                        services.AddSingleton<TimeProvider>(fun _ ->
                            let timeProvider = FakeTimeProvider(DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero))
                            timeProvider.SetLocalTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Hawaiian Standard Time"))
                            timeProvider :> TimeProvider
                        ) |> ignore
                        services.AddSingleton<IEventStore>(new InMemoryEventStore(FakeData.events, FakeData.eventRegistrations)) |> ignore
                        services.AddSingleton<IBookingConfirmationSender, InMemoryBookingConfirmationSender>() |> ignore
                    )
                    .Configure(fun app ->
                        app.UseRouting() |> ignore

                        app.UseEndpoints(fun config -> config.MapControllers() |> ignore) |> ignore
                    )
                |> ignore
            )
            .StartAsync()
        |> Async.AwaitTask
    }

    let getJsonSerializerOptions (host: IHost) =
        host.Services.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions

    let setTimeProviderTime (time: DateTime) (host: IHost) =
        let timeProvider = host.Services.GetRequiredService<TimeProvider>() :?> FakeTimeProvider
        timeProvider.SetUtcNow(TimeZoneInfo.ConvertTimeToUtc(time, timeProvider.LocalTimeZone))
