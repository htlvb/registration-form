namespace HTLVB.RegistrationForm.Admin.Server

open Dapper
open Npgsql
open System
open System.Data
open System.Text.Json

type DbEventSlot = {
    event_key: string
    time: DateTime
    duration: Nullable<TimeSpan>
    closing_date: Nullable<DateTime>
    max_quantity_per_booking: Nullable<int>
    remaining_capacity: Nullable<int>
    can_request_if_fully_booked: bool
}

module DbEventSlot =
    let toDomain (dbEventSlot: DbEventSlot) : Domain.Slot =
        {
            Time = dbEventSlot.time
            Duration = Option.ofNullable dbEventSlot.duration
            ClosingDate = Option.ofNullable dbEventSlot.closing_date
            MaxQuantityPerBooking = Option.ofNullable dbEventSlot.max_quantity_per_booking
            RemainingCapacity = Option.ofNullable dbEventSlot.remaining_capacity
            CanRequestIfFullyBooked = dbEventSlot.can_request_if_fully_booked
        }

type DbEvent = {
    key: string
    title: string
    info_text: string
    reservation_start_time: DateTime
    registration_confirmation_mail_subject: string
    registration_confirmation_mail_content_template: string
    request_confirmation_mail_subject: string
    request_confirmation_mail_content_template: string
    editor_ids: string
}

module DbEvent =
    let toDomain dbEvent dbEventSlots : Domain.EventData =
        {
            Key = dbEvent.key
            Title = dbEvent.title
            InfoText = dbEvent.info_text
            ReservationStartTime = dbEvent.reservation_start_time
            Slots = dbEventSlots |> Array.map DbEventSlot.toDomain
            RegistrationConfirmationMail = {
                Subject = dbEvent.registration_confirmation_mail_subject
                ContentTemplate = dbEvent.registration_confirmation_mail_content_template
            }
            RequestConfirmationMail =
                match Option.ofObj dbEvent.request_confirmation_mail_subject, Option.ofObj dbEvent.request_confirmation_mail_content_template with
                | Some subject, Some contentTemplate -> Some { Subject = subject; ContentTemplate = contentTemplate }
                | _ -> None
            EditorIds = JsonSerializer.Deserialize(dbEvent.editor_ids)
        }

type DbEventRegistration = {
    id: int
    event_key: string
    time: DateTime
    quantity: int
    name: string
    mail_address: string
    phone_number: string
    time_stamp: DateTime
    is_request: bool
    deregistration_time: Nullable<DateTime>
}
module DbEventRegistration =
    let toDomain dbEventRegistration : Domain.EventRegistration =
        {
            Id = string dbEventRegistration.id
            EventKey = dbEventRegistration.event_key
            Time = dbEventRegistration.time
            Quantity = dbEventRegistration.quantity
            Name = dbEventRegistration.name
            MailAddress = dbEventRegistration.mail_address
            PhoneNumber = dbEventRegistration.phone_number
            Timestamp = dbEventRegistration.time_stamp
            IsRequest = dbEventRegistration.is_request
            DeregistrationTime = Option.ofNullable dbEventRegistration.deregistration_time
        }

type IEventStore =
    abstract member CreateEvent: event: Domain.EventData -> Async<unit>
    abstract member GetEvents: unit -> Async<Domain.EventData list>
    abstract member TryGetEvent: eventKey: string -> Async<Domain.EventData option>
    abstract member UpdateEvent: eventKey: string -> data: Domain.EventUpdateData -> Async<unit>
    abstract member DeleteEvent: eventKey: string -> Async<unit>
    abstract member GetEventRegistrations: eventKey: string -> time: DateTime -> Async<Domain.EventRegistration list>
    abstract member GetEventRegistration: registrationId: string -> Async<Domain.EventRegistration option>
    abstract member CancelEventRegistration: registrationId: string -> deregistrationTime: DateTime -> Async<unit>

type PgsqlEventStore(dataSource: NpgsqlDataSource) =
    let insertSlots (connection: IDbConnection) tx (eventKey: string) (slots: Domain.Slot[]) = async {
        let fields = [
            "event_key"
            "time"
            "duration"
            "closing_date"
            "max_quantity_per_booking"
            "remaining_capacity"
            "can_request_if_fully_booked"
        ]
        let fieldNames = fields |> String.concat ", "
        let paramNames = fields |> List.map (sprintf "@%s") |> String.concat ", "
        let parameters =
            slots
            |> Array.map (fun v -> {|
                event_key = eventKey
                time = v.Time
                duration = Option.toNullable v.Duration
                closing_date = Option.toNullable v.ClosingDate
                max_quantity_per_booking = Option.toNullable v.MaxQuantityPerBooking
                remaining_capacity = Option.toNullable v.RemainingCapacity
                can_request_if_fully_booked = v.CanRequestIfFullyBooked
            |})
        do! connection.ExecuteAsync($"INSERT INTO event_slot (%s{fieldNames}) VALUES (%s{paramNames})", parameters, tx) |> Async.AwaitTask |> Async.Ignore
    }

    let updateSlots (connection: IDbConnection) tx (eventKey: string) (updates: (DateTime * Domain.SlotUpdateData)[]) = async {
        for (time, update) in updates do
            let updateData = [
                match update.Time with
                | Some v -> ("time", v :> obj)
                | None -> ()

                match update.Duration with
                | Some v -> ("duration", Option.toNullable v)
                | None -> ()

                match update.ClosingDate with
                | Some v -> ("closing_date", Option.toNullable v)
                | None -> ()

                match update.MaxQuantityPerBooking with
                | Some v -> ("max_quantity_per_booking", Option.toNullable v)
                | None -> ()

                match update.RemainingCapacity with
                | Some v -> ("remaining_capacity", Option.toNullable v)
                | None -> ()

                match update.CanRequestIfFullyBooked with
                | Some v -> ("can_request_if_fully_booked", v)
                | None -> ()
            ]
            if not (List.isEmpty updateData) then
                let updateFields =
                    [ for (name, _value) in updateData -> $"{name}=@{name}" ]
                    |> String.concat ", "
                let parameters =
                    let p = DynamicParameters()
                    p.Add("event_key", eventKey)
                    p.Add("old_time", time)
                    updateData |> List.iter (fun (name, value) -> p.Add(name, value))
                    p
                do! connection.ExecuteAsync($"UPDATE event_slot SET %s{updateFields} WHERE event_key = @event_key AND time = @old_time", parameters, tx) |> Async.AwaitTask |> Async.Ignore
    }

    let deleteSlots (connection: IDbConnection) tx (eventKey: string) (times: DateTime array) = async {
        let parameters = {|
            event_key = eventKey
            times = times
        |}
        do! connection.ExecuteAsync($"DELETE FROM event_slot WHERE event_key = @event_key AND time = ANY(@times)", parameters, tx) |> Async.AwaitTask |> Async.Ignore
    }

    interface IEventStore with
        member _.CreateEvent event = async {
            use! connection = dataSource.OpenConnectionAsync().AsTask() |> Async.AwaitTask
            use! tx = connection.BeginTransactionAsync().AsTask() |> Async.AwaitTask
            let fields = [
                ("key", event.Key :> obj)
                ("title", event.Title :> obj)
                ("info_text", event.InfoText)
                ("reservation_start_time", event.ReservationStartTime)
                ("registration_confirmation_mail_subject", event.RegistrationConfirmationMail.Subject)
                ("registration_confirmation_mail_content_template", event.RegistrationConfirmationMail.ContentTemplate)
                match event.RequestConfirmationMail with
                | Some v ->
                    ("request_confirmation_mail_subject", v.Subject)
                    ("request_confirmation_mail_content_template", v.ContentTemplate)
                | None -> ()
            ]
            let fieldNames = fields |> List.map fst |> String.concat ", "
            let paramNames = fields |> List.map (fst >> sprintf "@%s") |> String.concat ", "
            let parameters =
                let p = DynamicParameters()
                fields |> List.iter (fun (name, value) -> p.Add(name, value))
                p
            do! connection.ExecuteAsync($"INSERT INTO event (%s{fieldNames}) VALUES (%s{paramNames})", parameters, tx) |> Async.AwaitTask |> Async.Ignore
            do! insertSlots connection tx event.Key event.Slots
            
            do! tx.CommitAsync() |> Async.AwaitTask
        }

        member _.GetEvents () = async {
            use! connection = dataSource.OpenConnectionAsync().AsTask() |> Async.AwaitTask
            let! dbEvents = connection.QueryAsync<DbEvent>("SELECT key, title, info_text, reservation_start_time, registration_confirmation_mail_subject, registration_confirmation_mail_content_template, request_confirmation_mail_subject, request_confirmation_mail_content_template, editor_ids FROM event") |> Async.AwaitTask
            let! dbEventSlots = connection.QueryAsync<DbEventSlot>("SELECT event_key, time, duration, closing_date, max_quantity_per_booking, remaining_capacity, can_request_if_fully_booked FROM event_slot WHERE event_key = ANY(@EventKeys)", {| EventKeys = dbEvents |> Seq.map _.key |> Seq.toArray |}) |> Async.AwaitTask
            let dbEventSlotMap = dbEventSlots |> Seq.groupBy _.event_key |> Map.ofSeq
            return
                dbEvents
                |> Seq.map (fun dbEvent ->
                    let dbEventSlots = dbEventSlotMap |> Map.tryFind dbEvent.key |> Option.defaultValue []
                    DbEvent.toDomain dbEvent (Seq.toArray dbEventSlots)
                )
                |> Seq.toList
        }

        member _.TryGetEvent eventKey = async {
            use! connection = dataSource.OpenConnectionAsync().AsTask() |> Async.AwaitTask
            let! dbEvents = connection.QueryAsync<DbEvent>("SELECT key, title, info_text, reservation_start_time, registration_confirmation_mail_subject, registration_confirmation_mail_content_template, request_confirmation_mail_subject, request_confirmation_mail_content_template, editor_ids FROM event WHERE key = @EventKey", {| EventKey = eventKey |}) |> Async.AwaitTask
            match Seq.tryExactlyOne dbEvents with
            | None -> return None
            | Some dbEvent ->
                let! dbEventSlots = connection.QueryAsync<DbEventSlot>("SELECT event_key, time, duration, closing_date, max_quantity_per_booking, remaining_capacity, can_request_if_fully_booked FROM event_slot WHERE event_key = @EventKey", {| EventKey = eventKey |}) |> Async.AwaitTask
                return Some (DbEvent.toDomain dbEvent (Seq.toArray dbEventSlots))
        }

        member _.UpdateEvent eventKey data = async {
            use! connection = dataSource.OpenConnectionAsync().AsTask() |> Async.AwaitTask
            use! tx = connection.BeginTransactionAsync().AsTask() |> Async.AwaitTask
            let updateData = [
                match data.Key with
                | Some v -> ("key", v :> obj)
                | None -> ()

                match data.Title with
                | Some v -> ("title", v)
                | None -> ()

                match data.InfoText with
                | Some v -> ("info_text", v)
                | None -> ()

                match data.ReservationStartTime with
                | Some v -> ("reservation_start_time", v)
                | None -> ()

                match data.RegistrationConfirmationMail with
                | Some v ->
                    ("registration_confirmation_mail_subject", v)
                    ("registration_confirmation_mail_content_template", v)
                | None -> ()

                match data.RequestConfirmationMail with
                | Some (Some v) ->
                    ("registration_confirmation_mail_subject", v.Subject)
                    ("registration_confirmation_mail_content_template", v.ContentTemplate)
                | Some None ->
                    ("registration_confirmation_mail_subject", null)
                    ("registration_confirmation_mail_content_template", null)
                | None -> ()
            ]
            if not (List.isEmpty updateData) then
                let updateFields =
                    [ for (name, _value) in updateData -> $"{name}=@{name}" ]
                    |> String.concat ", "
                let parameters =
                    let p = DynamicParameters()
                    p.Add("old_key", eventKey)
                    updateData |> List.iter (fun (name, value) -> p.Add(name, value))
                    p
                do! connection.ExecuteAsync($"UPDATE event SET %s{updateFields} WHERE key = @old_key", parameters, tx) |> Async.AwaitTask |> Async.Ignore

            do! insertSlots connection tx eventKey (data.Slots |> Array.choose (function | Domain.CreateSlot slot -> Some slot | _ -> None))
            do! updateSlots connection tx eventKey (data.Slots |> Array.choose (function | Domain.UpdateSlot (time, data) -> Some (time, data) | _ -> None))
            do! deleteSlots connection tx eventKey (data.Slots |> Array.choose (function | Domain.DeleteSlot time -> Some time | _ -> None))

            do! tx.CommitAsync() |> Async.AwaitTask
        }

        member _.DeleteEvent eventKey = async {
            use! connection = dataSource.OpenConnectionAsync().AsTask() |> Async.AwaitTask
            do! connection.ExecuteAsync("DELETE FROM event WHERE key = @EventKey", {| EventKey = eventKey |}) |> Async.AwaitTask |> Async.Ignore
        }

        member _.GetEventRegistrations eventKey time = async {
            use! connection = dataSource.OpenConnectionAsync().AsTask() |> Async.AwaitTask
            let! result = connection.QueryAsync<DbEventRegistration>("SELECT id, event_key, time, quantity, name, mail_address, phone_number, time_stamp, is_request, deregistration_time FROM event_registration WHERE event_key = @EventKey AND time = @Time", {| EventKey = eventKey; Time = time |}) |> Async.AwaitTask
            return result |> Seq.map DbEventRegistration.toDomain |> Seq.toList
        }

        member _.GetEventRegistration registrationId = async {
            use! connection = dataSource.OpenConnectionAsync().AsTask() |> Async.AwaitTask
            let! result = connection.QueryAsync<DbEventRegistration>("SELECT id, event_key, time, quantity, name, mail_address, phone_number, time_stamp, is_request, deregistration_time FROM event_registration WHERE id = @RegistrationId", {| RegistrationId = registrationId |}) |> Async.AwaitTask
            return result |> Seq.tryExactlyOne |> Option.map DbEventRegistration.toDomain
        }

        member _.CancelEventRegistration registrationId timestamp = async {
            use! connection = dataSource.OpenConnectionAsync().AsTask() |> Async.AwaitTask
            do! connection.ExecuteAsync("UPDATE event_registration SET deregistration_time = @DeregistrationTime WHERE id = @RegistrationId AND deregistration_time IS NULL", {| RegistrationId = int registrationId; DeregistrationTime = timestamp |}) |> Async.AwaitTask |> Async.Ignore
        }

type InMemoryEventStore(events) =
    let mutable events = events
    interface IEventStore with
        member _.CreateEvent event = async {
            events <- event :: events
        }
        member _.GetEvents() = async {
            return events
        }
        member _.TryGetEvent(eventKey: string) = async {
            return events |> List.tryFind (fun v -> v.Key = eventKey)
        }
        member _.UpdateEvent eventKey data = async {
            let updateSlot (slot: Domain.Slot) (update: Domain.SlotUpdateData) : Domain.Slot =
                {
                    Time = update.Time |> Option.defaultValue slot.Time
                    Duration = update.Duration |> Option.defaultValue slot.Duration
                    ClosingDate = update.ClosingDate |> Option.defaultValue slot.ClosingDate
                    MaxQuantityPerBooking = update.MaxQuantityPerBooking |> Option.defaultValue slot.MaxQuantityPerBooking
                    RemainingCapacity = update.RemainingCapacity |> Option.defaultValue slot.RemainingCapacity
                    CanRequestIfFullyBooked = update.CanRequestIfFullyBooked |> Option.defaultValue slot.CanRequestIfFullyBooked
                }
            let applySlotUpdate (slots: Domain.Slot[]) (update: Domain.SlotUpdate) =
                match update with
                | Domain.CreateSlot v -> [| v; yield! slots |]
                | Domain.UpdateSlot (time, update) ->
                    slots |> Array.map (fun v -> if v.Time = time then updateSlot v update else v)
                | Domain.DeleteSlot time -> slots |> Array.filter (fun v -> v.Time <> time)
            let update (event: Domain.EventData) (data: Domain.EventUpdateData) : Domain.EventData =
                {
                    Key = data.Key |> Option.defaultValue event.Key
                    Title = data.Title |> Option.defaultValue event.Title
                    InfoText = data.InfoText |> Option.defaultValue event.InfoText
                    ReservationStartTime = data.ReservationStartTime |> Option.defaultValue event.ReservationStartTime
                    Slots = (event.Slots, data.Slots) ||> Array.fold applySlotUpdate
                    RegistrationConfirmationMail = data.RegistrationConfirmationMail |> Option.defaultValue event.RegistrationConfirmationMail
                    RequestConfirmationMail =
                        match data.RequestConfirmationMail with
                        | None -> event.RequestConfirmationMail
                        | Some v -> v
                    EditorIds = [||]
                }
            events <- events |> List.map (fun v -> if v.Key = eventKey then update v data else v)
        }
        member _.DeleteEvent eventKey = async {
            events <- events |> List.filter (fun v -> v.Key <> eventKey)
        }
        member _.GetEventRegistrations eventKey time = async {
            return failwith "Not implemented"
        }
        member _.GetEventRegistration registrationId = async {
            return failwith "Not implemented"
        }
        member _.CancelEventRegistration registrationId deregistrationTime = async {
            return failwith "Not implemented"
        }
    static member CreateWithSampleData(timeProvider: TimeProvider) =
        let now = timeProvider.GetLocalNow().DateTime
        let events : Domain.EventData list = [
            {
                Key = $"jobportal-{now.AddYears -1:yy}"
                Title = $"Jobportal %d{now.Year - 1}"
                InfoText = "Das Jobportal - die größte Karrieremesse in der Region - ist *die* Chance für Unternehmen, ihre zukünftigen Mitarbeiter kennen zu lernen."
                ReservationStartTime = now.AddYears -1
                Slots = [|
                    {
                        Time = now.AddYears(-1).AddDays 20
                        Duration = None
                        ClosingDate = Some (now.AddYears(-1).AddDays 10)
                        MaxQuantityPerBooking = Some 1
                        RemainingCapacity = None
                        CanRequestIfFullyBooked = false
                    }
                    {
                        Time = now.AddYears(-1).AddDays 21
                        Duration = None
                        ClosingDate = Some (now.AddYears(-1).AddDays 10)
                        MaxQuantityPerBooking = Some 1
                        RemainingCapacity = None
                        CanRequestIfFullyBooked = false
                    }
                |]
                RegistrationConfirmationMail = {
                    Subject = $"Anmeldung zum Jobportal %d{now.Year - 1}"
                    ContentTemplate = "Vielen Dank für Ihre Anmeldung."
                }
                RequestConfirmationMail = None
                EditorIds = [||]
            }

            {
                Key = $"jobportal-{now:yy}"
                Title = $"Jobportal %d{now.Year}"
                InfoText = "Das Jobportal - die größte Karrieremesse in der Region - ist *die* Chance für Unternehmen, ihre zukünftigen Mitarbeiter kennen zu lernen."
                ReservationStartTime = now
                Slots = [|
                    {
                        Time = now.AddDays 20
                        Duration = None
                        ClosingDate = Some (now.AddDays 10)
                        MaxQuantityPerBooking = Some 1
                        RemainingCapacity = None
                        CanRequestIfFullyBooked = false
                    }
                    {
                        Time = now.AddDays 21
                        Duration = None
                        ClosingDate = Some (now.AddDays 10)
                        MaxQuantityPerBooking = Some 1
                        RemainingCapacity = None
                        CanRequestIfFullyBooked = false
                    }
                |]
                RegistrationConfirmationMail = {
                    Subject = $"Anmeldung zum Jobportal %d{now.Year}"
                    ContentTemplate = "Vielen Dank für Ihre Anmeldung."
                }
                RequestConfirmationMail = None
                EditorIds = [||]
            }

            {
                Key = $"jobportal-{now.AddYears 1:yy}"
                Title = $"Jobportal %d{now.Year + 1}"
                InfoText = "Das Jobportal - die größte Karrieremesse in der Region - ist *die* Chance für Unternehmen, ihre zukünftigen Mitarbeiter kennen zu lernen."
                ReservationStartTime = now.AddYears(1)
                Slots = [|
                    {
                        Time = now.AddYears(1).AddDays 20
                        Duration = None
                        ClosingDate = Some (now.AddYears(1).AddDays 10)
                        MaxQuantityPerBooking = Some 1
                        RemainingCapacity = None
                        CanRequestIfFullyBooked = false
                    }
                    {
                        Time = now.AddYears(1).AddDays 21
                        Duration = None
                        ClosingDate = Some (now.AddYears(1).AddDays 10)
                        MaxQuantityPerBooking = Some 1
                        RemainingCapacity = None
                        CanRequestIfFullyBooked = false
                    }
                |]
                RegistrationConfirmationMail = {
                    Subject = $"Anmeldung zum Jobportal %d{now.Year + 1}"
                    ContentTemplate = "Vielen Dank für Ihre Anmeldung."
                }
                RequestConfirmationMail = None
                EditorIds = [||]
            }

            {
                Key = $"lets-code-{now.AddMonths 1:yy}"
                Title = $"Let's Code %d{now.AddMonths(1).Year}"
                InfoText = "Das Jobportal - die größte Karrieremesse in der Region - ist *die* Chance für Unternehmen, ihre zukünftigen Mitarbeiter kennen zu lernen."
                ReservationStartTime = now.AddMonths(1)
                Slots = [|
                    {
                        Time = now.AddMonths(1).AddDays 20
                        Duration = None
                        ClosingDate = Some (now.AddMonths(1).AddDays 10)
                        MaxQuantityPerBooking = Some 1
                        RemainingCapacity = None
                        CanRequestIfFullyBooked = false
                    }
                    {
                        Time = now.AddMonths(1).AddDays 21
                        Duration = None
                        ClosingDate = Some (now.AddMonths(1).AddDays 10)
                        MaxQuantityPerBooking = Some 1
                        RemainingCapacity = None
                        CanRequestIfFullyBooked = false
                    }
                |]
                RegistrationConfirmationMail = {
                    Subject = $"Anmeldung zum Jobportal %d{now.AddMonths(1).Year}"
                    ContentTemplate = "Vielen Dank für Ihre Anmeldung."
                }
                RequestConfirmationMail = None
                EditorIds = [||]
            }
        ]
        InMemoryEventStore events
