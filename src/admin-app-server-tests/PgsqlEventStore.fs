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

let addRegistrationWith (dataSource: NpgsqlDataSource) (eventKey: string) (time: DateTime) (quantity: int) (isRequest: bool) = async {
    use! connection = dataSource.OpenConnectionAsync().AsTask() |> Async.AwaitTask
    let fields = [
        ("event_key", eventKey :> obj)
        ("time", time)
        ("quantity", quantity)
        ("name", "Albert Einstein")
        ("mail_address", "albert@einstein.com")
        ("phone_number", "0732 524326")
        ("time_stamp", DateTime.Now)
        ("is_request", isRequest)
    ]
    let fieldNames = fields |> List.map fst |> String.concat ", "
    let parameterNames = fields |> List.map (fst >> sprintf "@%s") |> String.concat ", "
    let parameters =
        let p = DynamicParameters()
        fields |> List.iter (fun (name, value) -> p.Add(name, value))
        p
    return! connection.QuerySingleAsync<int>($"INSERT INTO event_registration (%s{fieldNames}) VALUES (%s{parameterNames}) RETURNING id", parameters) |> Async.AwaitTask
}

let addRegistration dataSource eventKey time = addRegistrationWith dataSource eventKey time 1 false

let createEvent (eventStore: IEventStore) event = async {
    match! eventStore.CreateEvent event with
    | Ok () -> ()
    | Error e -> failwith $"Creating event failed: %A{e}"
}

[<Tests>]
let tests =
    testList "PgsqlEventStore" [
        testCaseTask "Can insert event with slots" <| fun () -> task {
            use! db = startDb
            use dataSource = getDataSource db
            let eventStore = PgsqlEventStore(dataSource) :> IEventStore
            do! createEvent eventStore {
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
                EditorIds = [| "EINA@htlvb.at" |]
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
                    EditorIds = [| "EINA@htlvb.at" |]
                }
            ]
            Expect.equal actual expected "Event should have been created"
        }

        testCaseTask "Can't create event with existing key" <| fun () -> task {
            use! db = startDb
            use dataSource = getDataSource db
            let eventStore = PgsqlEventStore(dataSource) :> IEventStore
            let event : Domain.EventData = {
                Key = "lets-code-2425"
                Title = "Let's code 2024/25"
                InfoText = ""
                ReservationStartTime = DateTime.Today
                Slots = [|
                    {
                        Time = DateTime.Today.AddDays 1.5
                        Duration = None
                        ClosingDate = None
                        MaxQuantityPerBooking = None
                        RemainingCapacity = None
                        CanRequestIfFullyBooked = false
                    }
                |]
                RegistrationConfirmationMail = { Subject = "Anmeldung"; ContentTemplate = "Danke für deine Anmeldung" }
                RequestConfirmationMail = None
                EditorIds = [| "EINA@htlvb.at" |]
            }
            do! createEvent eventStore event
            let! result = eventStore.CreateEvent { event with Title = "Let's code 2025/26"; Slots = [||] }
            Expect.equal result (Error Domain.EventKeyAlreadyExists) "Event with existing key should not have been created"
            let! actual = eventStore.GetEvents()
            Expect.equal actual [ event ] "Existing event should not have been changed"
        }

        testCaseTask "Can update event key" <| fun () -> task {
            use! db = startDb
            use dataSource = getDataSource db
            let eventStore = PgsqlEventStore(dataSource) :> IEventStore
            do! createEvent eventStore {
                Key = "lets-code-2425"
                Title = "Let's code 2024/25"
                InfoText = ""
                ReservationStartTime = DateTime.Today
                Slots = [||]
                RegistrationConfirmationMail = { Subject = "Anmeldung"; ContentTemplate = "Danke für deine Anmeldung" }
                RequestConfirmationMail = None
                EditorIds = [| "EINA@htlvb.at" |]
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
                    EditorIds = [| "EINA@htlvb.at" |]
                }
            ]
            Expect.equal actual expected "Key should have been updated"
        }

        testCaseTask "Can update event slots" <| fun () -> task {
            use! db = startDb
            use dataSource = getDataSource db
            let eventStore = PgsqlEventStore(dataSource) :> IEventStore
            do! createEvent eventStore {
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
                EditorIds = [| "EINA@htlvb.at" |]
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
                    EditorIds = [| "EINA@htlvb.at" |]
                }
            ]
            Expect.equal actual expected "Event slots should have been updated"
        }

        testCaseTask "Can update mail templates" <| fun () -> task {
            use! db = startDb
            use dataSource = getDataSource db
            let eventStore = PgsqlEventStore(dataSource) :> IEventStore
            do! createEvent eventStore {
                Key = "lets-code-2425"
                Title = "Let's code 2024/25"
                InfoText = ""
                ReservationStartTime = DateTime.Today
                Slots = [||]
                RegistrationConfirmationMail = { Subject = "Anmeldung"; ContentTemplate = "Danke für deine Anmeldung" }
                RequestConfirmationMail = None
                EditorIds = [| "EINA@htlvb.at" |]
            }
            do! eventStore.UpdateEvent "lets-code-2425" {
                Key = None
                Title = None
                InfoText = None
                ReservationStartTime = None
                Slots = [||]
                RegistrationConfirmationMail = Some { Subject = "Neue Anmeldung"; ContentTemplate = "Vielen Dank für deine Anmeldung" }
                RequestConfirmationMail = Some (Some { Subject = "Anfrage"; ContentTemplate = "Danke für deine Anfrage" })
            }
            let! actual = eventStore.GetEvents()
            let expected : Domain.EventData list = [
                {
                    Key = "lets-code-2425"
                    Title = "Let's code 2024/25"
                    InfoText = ""
                    ReservationStartTime = DateTime.Today
                    Slots = [||]
                    RegistrationConfirmationMail = { Subject = "Neue Anmeldung"; ContentTemplate = "Vielen Dank für deine Anmeldung" }
                    RequestConfirmationMail = Some { Subject = "Anfrage"; ContentTemplate = "Danke für deine Anfrage" }
                    EditorIds = [| "EINA@htlvb.at" |]
                }
            ]
            Expect.equal actual expected "Mail templates should have been updated"
        }

        testCaseTask "Can replace deleted slot with new slot at the same time" <| fun () -> task {
            use! db = startDb
            use dataSource = getDataSource db
            let eventStore = PgsqlEventStore(dataSource) :> IEventStore
            let slotTime = DateTime.Today.AddDays(1).AddHours(8)
            do! createEvent eventStore {
                Key = "lets-code-2425"
                Title = "Let's code 2024/25"
                InfoText = ""
                ReservationStartTime = DateTime.Today
                Slots = [|
                    {
                        Time = slotTime
                        Duration = None
                        ClosingDate = None
                        MaxQuantityPerBooking = Some 10
                        RemainingCapacity = Some 100
                        CanRequestIfFullyBooked = false
                    }
                |]
                RegistrationConfirmationMail = { Subject = "Anmeldung"; ContentTemplate = "Danke für deine Anmeldung" }
                RequestConfirmationMail = None
                EditorIds = [| "EINA@htlvb.at" |]
            }
            let newSlot : Domain.Slot = {
                Time = slotTime
                Duration = Some (TimeSpan.FromHours 1)
                ClosingDate = None
                MaxQuantityPerBooking = None
                RemainingCapacity = Some 20
                CanRequestIfFullyBooked = true
            }
            do! eventStore.UpdateEvent "lets-code-2425" {
                Key = None
                Title = None
                InfoText = None
                ReservationStartTime = None
                Slots = [| Domain.CreateSlot newSlot; Domain.DeleteSlot slotTime |]
                RegistrationConfirmationMail = None
                RequestConfirmationMail = None
            }
            let! actual = eventStore.GetEvents()
            Expect.equal (actual |> List.map _.Slots) [ [| newSlot |] ] "Slot should have been replaced"
        }

        testCaseTask "Can't update event slot time when there are registrations" <| fun () -> task {
            use! db = startDb
            use dataSource = getDataSource db
            let eventStore = PgsqlEventStore(dataSource) :> IEventStore
            do! createEvent eventStore {
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
                EditorIds = [| "EINA@htlvb.at" |]
            }
            do! addRegistration dataSource "lets-code-2425" (DateTime.Today.AddDays(1).AddHours(8)) |> Async.Ignore
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

        testCaseTask "Can delete event without registrations" <| fun () -> task {
            use! db = startDb
            use dataSource = getDataSource db
            let eventStore = PgsqlEventStore(dataSource) :> IEventStore
            do! createEvent eventStore {
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
                EditorIds = [| "EINA@htlvb.at" |]
            }
            do! eventStore.DeleteEvent "lets-code-2425"
            let! events = eventStore.GetEvents ()
            Expect.isEmpty events "Event should have been deleted"
        }

        testCaseTask "Can get all event registrations" <| fun () -> task {
            use! db = startDb
            use dataSource = getDataSource db
            let eventStore = PgsqlEventStore(dataSource) :> IEventStore
            let slot1 = DateTime.Today.AddDays(1).AddHours(8)
            let slot2 = DateTime.Today.AddDays(1).AddHours(10)
            do! createEvent eventStore {
                Key = "lets-code-2425"
                Title = "Let's code 2024/25"
                InfoText = ""
                ReservationStartTime = DateTime.Today
                Slots = [|
                    { Time = slot1; Duration = None; ClosingDate = None; MaxQuantityPerBooking = None; RemainingCapacity = None; CanRequestIfFullyBooked = true }
                    { Time = slot2; Duration = None; ClosingDate = None; MaxQuantityPerBooking = None; RemainingCapacity = None; CanRequestIfFullyBooked = true }
                    { Time = DateTime.Today.AddDays(1).AddHours(12); Duration = None; ClosingDate = None; MaxQuantityPerBooking = None; RemainingCapacity = None; CanRequestIfFullyBooked = true }
                |]
                RegistrationConfirmationMail = { Subject = "Anmeldung"; ContentTemplate = "Danke für deine Anmeldung" }
                RequestConfirmationMail = None
                EditorIds = [| "EINA@htlvb.at" |]
            }
            let! _ = addRegistrationWith dataSource "lets-code-2425" slot1 2 false
            let! _ = addRegistrationWith dataSource "lets-code-2425" slot1 3 false
            let! _ = addRegistrationWith dataSource "lets-code-2425" slot1 4 true
            let! cancelledRegistrationId = addRegistrationWith dataSource "lets-code-2425" slot2 5 false
            let! _ = addRegistrationWith dataSource "lets-code-2425" slot2 1 false
            do! eventStore.CancelEventRegistration (string cancelledRegistrationId) DateTime.Now
            let! registrations = eventStore.GetAllEventRegistrations()
            let actual = registrations |> List.map (fun v -> v.EventKey, v.Time, v.Quantity, v.IsRequest, v.DeregistrationTime.IsSome)
            let expected = [
                ("lets-code-2425", slot1, 2, false, false)
                ("lets-code-2425", slot1, 3, false, false)
                ("lets-code-2425", slot1, 4, true, false)
                ("lets-code-2425", slot2, 5, false, true)
                ("lets-code-2425", slot2, 1, false, false)
            ]
            Expect.equal actual expected "All registrations (incl. cancelled ones) should be returned"
        }

        testCaseTask "Can get event registration by id" <| fun () -> task {
            use! db = startDb
            use dataSource = getDataSource db
            let eventStore = PgsqlEventStore(dataSource) :> IEventStore
            let slot = DateTime.Today.AddDays(1).AddHours(8)
            do! createEvent eventStore {
                Key = "lets-code-2425"
                Title = "Let's code 2024/25"
                InfoText = ""
                ReservationStartTime = DateTime.Today
                Slots = [|
                    { Time = slot; Duration = None; ClosingDate = None; MaxQuantityPerBooking = None; RemainingCapacity = None; CanRequestIfFullyBooked = false }
                |]
                RegistrationConfirmationMail = { Subject = "Anmeldung"; ContentTemplate = "Danke für deine Anmeldung" }
                RequestConfirmationMail = None
                EditorIds = [| "EINA@htlvb.at" |]
            }
            let! registrationId = addRegistrationWith dataSource "lets-code-2425" slot 3 false
            let! registration = eventStore.GetEventRegistration (string registrationId)
            Expect.equal (registration |> Option.map (fun v -> v.Id, v.EventKey, v.Time, v.Quantity)) (Some (string registrationId, "lets-code-2425", slot, 3)) "Registration should have been found"
            let! unknownRegistration = eventStore.GetEventRegistration "abc"
            Expect.isNone unknownRegistration "Registration with invalid id should not have been found"
        }

        testCaseTask "Cancelling registration releases capacity" <| fun () -> task {
            use! db = startDb
            use dataSource = getDataSource db
            let eventStore = PgsqlEventStore(dataSource) :> IEventStore
            let slot = DateTime.Today.AddDays(1).AddHours(8)
            do! createEvent eventStore {
                Key = "lets-code-2425"
                Title = "Let's code 2024/25"
                InfoText = ""
                ReservationStartTime = DateTime.Today
                Slots = [|
                    { Time = slot; Duration = None; ClosingDate = None; MaxQuantityPerBooking = None; RemainingCapacity = Some 10; CanRequestIfFullyBooked = true }
                |]
                RegistrationConfirmationMail = { Subject = "Anmeldung"; ContentTemplate = "Danke für deine Anmeldung" }
                RequestConfirmationMail = None
                EditorIds = [| "EINA@htlvb.at" |]
            }
            let! registrationId = addRegistrationWith dataSource "lets-code-2425" slot 3 false
            let! requestId = addRegistrationWith dataSource "lets-code-2425" slot 4 true
            do! eventStore.CancelEventRegistration (string registrationId) DateTime.Now
            do! eventStore.CancelEventRegistration (string requestId) DateTime.Now
            do! eventStore.CancelEventRegistration (string registrationId) DateTime.Now // already cancelled
            let! event = eventStore.TryGetEvent "lets-code-2425"
            let actual = event |> Option.map (fun v -> v.Slots |> Array.map _.RemainingCapacity)
            Expect.equal actual (Some [| Some 13 |]) "Only cancelling a registration (not a request) once should release capacity"
        }

        testCaseTask "Can cancel event registration" <| fun () -> task {
            use! db = startDb
            use dataSource = getDataSource db
            let eventStore = PgsqlEventStore(dataSource) :> IEventStore
            do! createEvent eventStore {
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
                EditorIds = [| "EINA@htlvb.at" |]
            }
            let! registrationId = addRegistration dataSource "lets-code-2425" (DateTime.Today.AddDays(1).AddHours(8))
            let cancelTimestamp =
                let d = DateTime.Now
                DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, d.Second, d.Millisecond, d.Microsecond)
            do! eventStore.CancelEventRegistration (string registrationId) cancelTimestamp
            let! actual = async {
                let! registrations = eventStore.GetEventRegistrations "lets-code-2425" (DateTime.Today.AddDays(1).AddHours(8))
                return registrations |> List.map _.DeregistrationTime
            }
            let expected = [ Some cancelTimestamp ]
            Expect.equal actual expected "Event registration should have deregistration time"
        }
    ]
