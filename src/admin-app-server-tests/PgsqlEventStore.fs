module HTLVB.RegistrationForm.Admin.Server.Tests.PgsqlEventStore

open Dapper
open Expecto
open HTLVB.RegistrationForm.Admin.Server
open Npgsql
open System
open System.IO
open Testcontainers.PostgreSql

let startDb = async {
    let db =
        PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithBindMount(Path.GetFullPath("db-schema.sql"), "/docker-entrypoint-initdb.d/01-schema.sql")
            .Build()
    do! db.StartAsync() |> Async.AwaitTask
    return db
}

let getDataSource (db: PostgreSqlContainer) =
    NpgsqlDataSourceBuilder($"{db.GetConnectionString()};Include Error Detail=true").EnableDynamicJson().Build()

[<Tests>]
let tests =
    testList "PgsqlEventStore" [
        testCaseTask "Can insert event with slots" <| fun () -> task {
            use! db = startDb
            use dataSource = getDataSource db
            let eventStore = PgsqlEventStore(dataSource) :> IEventStore
            do! eventStore.CreateEvent {
                Key = "lets-code-2425"
                Title = "Let's code 2024/25"
                InfoText = ""
                ReservationStartTime = DateTime.Today
                Slots = [|
                    {
                        Time = DateTime.Today.AddDays 1.5
                        Duration = Some (TimeSpan.FromHours 1.)
                        ClosingDate = Some (DateTime.Today.AddDays 1.)
                        MaxQuantityPerBooking = Some 5
                        RemainingCapacity = Some 20
                        CanRequestIfFullyBooked = false
                    }
                    {
                        Time = DateTime.Today.AddDays 2.75
                        Duration = None
                        ClosingDate = None
                        MaxQuantityPerBooking = None
                        RemainingCapacity = None
                        CanRequestIfFullyBooked = true
                    }
                |]
                RegistrationConfirmationMail = { Subject = "Anmeldung"; ContentTemplate = "Danke für deine Anmeldung" }
                RequestConfirmationMail = None
            }
            let! actual = eventStore.GetEvents()
            let expected : Domain.EventData list = [
                {
                    Key = "lets-code-2425"
                    Title = "Let's code 2024/25"
                    InfoText = ""
                    ReservationStartTime = DateTime.Today
                    Slots = [|
                        {
                            Time = DateTime.Today.AddDays 1.5
                            Duration = Some (TimeSpan.FromHours 1.)
                            ClosingDate = Some (DateTime.Today.AddDays 1.)
                            MaxQuantityPerBooking = Some 5
                            RemainingCapacity = Some 20
                            CanRequestIfFullyBooked = false
                        }
                        {
                            Time = DateTime.Today.AddDays 2.75
                            Duration = None
                            ClosingDate = None
                            MaxQuantityPerBooking = None
                            RemainingCapacity = None
                            CanRequestIfFullyBooked = true
                        }
                    |]
                    RegistrationConfirmationMail = { Subject = "Anmeldung"; ContentTemplate = "Danke für deine Anmeldung" }
                    RequestConfirmationMail = None
                }
            ]
            Expect.equal actual expected "Event should have been created"
        }

        testCaseTask "Can update event key" <| fun () -> task {
            use! db = startDb
            use dataSource = getDataSource db
            let eventStore = PgsqlEventStore(dataSource) :> IEventStore
            do! eventStore.CreateEvent {
                Key = "lets-code-2425"
                Title = "Let's code 2024/25"
                InfoText = ""
                ReservationStartTime = DateTime.Today
                Slots = [||]
                RegistrationConfirmationMail = { Subject = "Anmeldung"; ContentTemplate = "Danke für deine Anmeldung" }
                RequestConfirmationMail = None
            }
            do! eventStore.UpdateEvent "lets-code-2425" {
                Key = Some "lets-code-2425-1"
                Title = None
                InfoText = None
                ReservationStartTime = None
                Slots = [||]
                RegistrationConfirmationMail = None
                RequestConfirmationMail = None
            }
            let! actual = eventStore.GetEvents()
            let expected : Domain.EventData list = [
                {
                    Key = "lets-code-2425-1"
                    Title = "Let's code 2024/25"
                    InfoText = ""
                    ReservationStartTime = DateTime.Today
                    Slots = [||]
                    RegistrationConfirmationMail = { Subject = "Anmeldung"; ContentTemplate = "Danke für deine Anmeldung" }
                    RequestConfirmationMail = None
                }
            ]
            Expect.equal actual expected "Key should have been updated"
        }

        testCaseTask "Can update event slots" <| fun () -> task {
            use! db = startDb
            use dataSource = getDataSource db
            let eventStore = PgsqlEventStore(dataSource) :> IEventStore
            do! eventStore.CreateEvent {
                Key = "lets-code-2425"
                Title = "Let's code 2024/25"
                InfoText = ""
                ReservationStartTime = DateTime.Today
                Slots = Array.init 5 (fun i ->
                    {
                        Time = DateTime.Today.AddDays(float (i + 1)).AddHours(8)
                        Duration = None
                        ClosingDate = None
                        MaxQuantityPerBooking = Some 10
                        RemainingCapacity = Some 100
                        CanRequestIfFullyBooked = false
                    }
                )
                RegistrationConfirmationMail = { Subject = "Anmeldung"; ContentTemplate = "Danke für deine Anmeldung" }
                RequestConfirmationMail = None
            }
            do! eventStore.UpdateEvent "lets-code-2425" {
                Key = None
                Title = None
                InfoText = None
                ReservationStartTime = None
                Slots = [|
                    Domain.CreateSlot {
                        Time = DateTime.Today.AddDays(10).AddHours(8)
                        Duration = Some (TimeSpan.FromHours 1)
                        ClosingDate = Some (DateTime.Today.AddDays 5)
                        MaxQuantityPerBooking = None
                        RemainingCapacity = None
                        CanRequestIfFullyBooked = false
                    }
                    Domain.UpdateSlot (DateTime.Today.AddDays(1).AddHours(8), {
                        Time = Some (DateTime.Today.AddDays(1).AddHours(10))
                        Duration = Some (Some (TimeSpan.FromHours 2))
                        ClosingDate = Some (Some (DateTime.Today.AddDays(1)))
                        MaxQuantityPerBooking = Some None
                        RemainingCapacity = Some (Some 50)
                        CanRequestIfFullyBooked = Some true
                    })
                    Domain.UpdateSlot (DateTime.Today.AddDays(2).AddHours(8), {
                        Time = None
                        Duration = None
                        ClosingDate = None
                        MaxQuantityPerBooking = Some None
                        RemainingCapacity = Some None
                        CanRequestIfFullyBooked = None
                    })
                    Domain.DeleteSlot (DateTime.Today.AddDays(3).AddHours(8))
                    Domain.DeleteSlot (DateTime.Today.AddDays(4).AddHours(8))
                |]
                RegistrationConfirmationMail = None
                RequestConfirmationMail = None
            }
            let! actual = async {
                let! list = eventStore.GetEvents()
                return list
                    |> List.map (fun event -> { event with Slots = event.Slots |> Array.sortBy (fun v -> v.Time) })
            }
            let expected : Domain.EventData list = [
                {
                    Key = "lets-code-2425"
                    Title = "Let's code 2024/25"
                    InfoText = ""
                    ReservationStartTime = DateTime.Today
                    Slots = [|
                        {
                            Time = DateTime.Today.AddDays(1).AddHours(10)
                            Duration = Some (TimeSpan.FromHours 2)
                            ClosingDate = Some (DateTime.Today.AddDays 1)
                            MaxQuantityPerBooking = None
                            RemainingCapacity = Some 50
                            CanRequestIfFullyBooked = true
                        }
                        {
                            Time = DateTime.Today.AddDays(2).AddHours(8)
                            Duration = None
                            ClosingDate = None
                            MaxQuantityPerBooking = None
                            RemainingCapacity = None
                            CanRequestIfFullyBooked = false
                        }
                        {
                            Time = DateTime.Today.AddDays(5).AddHours(8)
                            Duration = None
                            ClosingDate = None
                            MaxQuantityPerBooking = Some 10
                            RemainingCapacity = Some 100
                            CanRequestIfFullyBooked = false
                        }
                        {
                            Time = DateTime.Today.AddDays(10).AddHours(8)
                            Duration = Some (TimeSpan.FromHours 1)
                            ClosingDate = Some (DateTime.Today.AddDays 5)
                            MaxQuantityPerBooking = None
                            RemainingCapacity = None
                            CanRequestIfFullyBooked = false
                        }
                    |]
                    RegistrationConfirmationMail = { Subject = "Anmeldung"; ContentTemplate = "Danke für deine Anmeldung" }
                    RequestConfirmationMail = None
                }
            ]
            Expect.equal actual expected "Event slots should have been updated"
        }

        testCaseTask "Can't update event slot time when there are registrations" <| fun () -> task {
            use! db = startDb
            use dataSource = getDataSource db
            let eventStore = PgsqlEventStore(dataSource) :> IEventStore
            do! eventStore.CreateEvent {
                Key = "lets-code-2425"
                Title = "Let's code 2024/25"
                InfoText = ""
                ReservationStartTime = DateTime.Today
                Slots = [|
                    {
                        Time = DateTime.Today.AddDays(1).AddHours(8)
                        Duration = None
                        ClosingDate = None
                        MaxQuantityPerBooking = Some 10
                        RemainingCapacity = Some 100
                        CanRequestIfFullyBooked = false
                    }
                |]
                RegistrationConfirmationMail = { Subject = "Anmeldung"; ContentTemplate = "Danke für deine Anmeldung" }
                RequestConfirmationMail = None
            }
            do! async {
                use! connection = dataSource.OpenConnectionAsync().AsTask() |> Async.AwaitTask
                let fields = [
                    ("event_key", "lets-code-2425" :> obj)
                    ("time", DateTime.Today.AddDays(1).AddHours(8))
                    ("quantity", 1)
                    ("name", "Albert Einstein")
                    ("mail_address", "albert@einstein.com")
                    ("phone_number", "0732 524326")
                    ("time_stamp", DateTime.Now)
                    ("is_request", false)
                ]
                let fieldNames = fields |> List.map fst |> String.concat ", "
                let parameterNames = fields |> List.map (fst >> sprintf "@%s") |> String.concat ", "
                let parameters =
                    let p = DynamicParameters()
                    fields |> List.iter (fun (name, value) -> p.Add(name, value))
                    p
                do! connection.ExecuteAsync($"INSERT INTO event_registration (%s{fieldNames}) VALUES (%s{parameterNames})", parameters) |> Async.AwaitTask |> Async.Ignore
            }
            let isFKConstraintViolation (e: exn) =
                match e with
                | :? AggregateException as e ->
                    match e.InnerException with
                    | :? PostgresException as e when e.ConstraintName = "event_registration_event_key_time_fkey" -> true
                    | _ -> false
                | _ -> false
            try
                do! eventStore.UpdateEvent "lets-code-2425" {
                    Key = None
                    Title = None
                    InfoText = None
                    ReservationStartTime = None
                    Slots = [|
                        Domain.UpdateSlot (DateTime.Today.AddDays(1).AddHours(8), {
                            Time = Some (DateTime.Today.AddDays(1).AddHours(10))
                            Duration = None
                            ClosingDate = None
                            MaxQuantityPerBooking = None
                            RemainingCapacity = None
                            CanRequestIfFullyBooked = None
                        })
                    |]
                    RegistrationConfirmationMail = None
                    RequestConfirmationMail = None
                }
                failtest "Updating event slot time with registration should fail"
            with
                | e when isFKConstraintViolation e -> ()
                | e -> failtest $"Updating event slot time with registration should fail with foreign key constraint violation, but got %A{e}"
        }
    ]
