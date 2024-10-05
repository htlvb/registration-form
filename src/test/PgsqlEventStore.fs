module HTLVB.RegistrationForm.Server.Tests.PgsqlEventStore

open Dapper
open HTLVB.RegistrationForm.Server
open Npgsql
open System
open System.IO
open Testcontainers.PostgreSql
open Xunit

let private startDb() = task {
    let pgsqlContainer =
        (new PostgreSqlBuilder())
            .WithBindMount(Path.GetFullPath("db-schema.sql"), "/docker-entrypoint-initdb.d/01-schema.sql")
            .Build()
    do! pgsqlContainer.StartAsync()
    return pgsqlContainer
}

let private insertSampleEvent (dataSource: NpgsqlDataSource) remainingCapacity = task {
    use! connection = dataSource.OpenConnectionAsync()
    let! _ = connection.ExecuteAsync("INSERT INTO event (key, title, info_text, reservation_start_time, mail_subject, mail_content_template) VALUES ('schulfuehrungen-2025', 'Schulführungen 2025', 'Bitte melden Sie sich für eine Schulführung an.', '2024-09-29 00:00:00', 'Anmeldung zum Tag der offenen Tür der HTL Vöcklabruck', '{{{FullName}}}, danke für die Anmeldung am {{{Date}}} um {{{Time}}}.')")
    let! _ = connection.ExecuteAsync("INSERT INTO event_slot (event_key, time, max_quantity_per_booking, remaining_capacity) VALUES ('schulfuehrungen-2025', '2025-01-28 14:00:00', NULL, @RemainingCapacity)", {| RemainingCapacity = remainingCapacity |})
    ()
}

let private sampleEventRegistration quantity = {
    time = DateTime(2025, 1, 28, 14, 0, 0)
    quantity = quantity
    name = "Albert"
    mail_address = "albert@einstein.com"
    phone_number = "07612/123456789"
    time_stamp = DateTime.Now
}

type BookingResult = {
    SucceededBookingResults: Set<int option>
    FailedBookingResults: Set<Domain.BookingError>
    RemainingCapacity: int option
    Registrations: int
}

let makeParallelSampleEventBookings dataSource count registration = task {
    let eventStore : IEventStore = PgsqlEventStore(dataSource)
    let! bookingResults =
        List.replicate count (eventStore.TryBook "schulfuehrungen-2025" registration)
        |> Async.Parallel
        |> Async.StartAsTask
    let succeededBookings = bookingResults |> Array.choose Result.toOption
    let failedBookings = bookingResults |> Array.choose (function | Error v -> Some v | Ok _ -> None)
    let! (remainingCapacity, registrations) = task {
        use! connection = dataSource.OpenConnectionAsync()
        let! remainingCapacity = connection.QuerySingleAsync<Nullable<int>>("SELECT remaining_capacity FROM event_slot")
        let! registrations = connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM event_registration")
        return (remainingCapacity, registrations)
    }
    return {
        SucceededBookingResults = Set.ofArray succeededBookings
        FailedBookingResults = Set.ofArray failedBookings
        RemainingCapacity = Option.ofNullable remainingCapacity
        Registrations = registrations
    }
}

[<Fact>]
let ``Can book in parallel`` () = task {
    use! db = startDb()
    use dataSource = NpgsqlDataSource.Create(db.GetConnectionString())
    do! insertSampleEvent dataSource 15

    let! actualBookingResult = makeParallelSampleEventBookings dataSource 15 (sampleEventRegistration 1)
    let expectedBookingResult = {
        SucceededBookingResults = Set.ofList [ for i in 0..14 -> Some i ]
        FailedBookingResults = Set.empty
        RemainingCapacity = Some 0
        Registrations = 15
    }
    Assert.Equal(expectedBookingResult, actualBookingResult)
}

[<Fact>]
let ``Can't overbook`` () = task {
    use! db = startDb()
    use dataSource = NpgsqlDataSource.Create(db.GetConnectionString())
    do! insertSampleEvent dataSource 100

    let! actualBookingResult = makeParallelSampleEventBookings dataSource 200 (sampleEventRegistration 1)
    let expectedBookingResult = {
        SucceededBookingResults = Set.ofList [ for i in 0..99 -> Some i ]
        FailedBookingResults = Set.singleton (Domain.CapacityExceeded 0)
        RemainingCapacity = Some 0
        Registrations = 100
    }
    Assert.Equal(expectedBookingResult, actualBookingResult)
}
