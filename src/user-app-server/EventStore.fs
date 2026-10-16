namespace HTLVB.RegistrationForm.Server

open Dapper
open System
open System.Text.Json
open Npgsql

type DbEventSlot = {
    time: DateTime
    duration: Nullable<TimeSpan>
    closing_date: Nullable<DateTime>
    max_quantity_per_booking: Nullable<int>
    remaining_capacity: Nullable<int>
    can_request_if_fully_booked: bool
}

module DbEventSlot =
    let toDomain (dbEventSlot: DbEventSlot) : Domain.SlotData =
        {
            Time = dbEventSlot.time
            Duration = Option.ofNullable dbEventSlot.duration
            ClosingDate = Option.ofNullable dbEventSlot.closing_date
            MaxQuantityPerBooking = Option.ofNullable dbEventSlot.max_quantity_per_booking
            RemainingCapacity = Option.ofNullable dbEventSlot.remaining_capacity
            CanRequestIfFullyBooked = dbEventSlot.can_request_if_fully_booked
        }

type DbRegistrationField = {
    key: string
    label: string
    ``type``: string
    validator: string
}
module DbRegistrationField =
    let toDomain (v: DbRegistrationField) : Domain.RegistrationField =
        {
            Key = v.key
            Label = v.label
            Type =
                match v.``type`` with
                | "text" -> Domain.TextField
                | "mail" -> Domain.MailField
                | "phone" -> Domain.PhoneField
                | v -> failwith $"Unknown registration field type \"%s{v}\""
            Validator =
                match v.validator with
                | "none" -> Domain.NoValidation
                | "notEmpty" -> Domain.NotEmpty
                | v -> failwith $"Unknown registration field validator \"%s{v}\""
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
    registration_fields: string
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
            RegistrationFields =
                JsonSerializer.Deserialize<DbRegistrationField[]>(dbEvent.registration_fields)
                |> Array.map DbRegistrationField.toDomain
        }

type IEventStore =
    abstract member TryGetEvent: eventKey: string -> Async<Domain.EventData option>
    abstract member TryBook: Domain.BookingData -> Async<Result<int option, Domain.BookingError>>
    abstract member AddBookingRequest: Domain.BookingData -> Async<unit>

type PgsqlEventStore(dataSource: NpgsqlDataSource) =
    interface IEventStore with
        member _.TryGetEvent eventKey = async {
            use! connection = dataSource.OpenConnectionAsync().AsTask() |> Async.AwaitTask
            let! dbEvents = connection.QueryAsync<DbEvent>("SELECT key, title, info_text, reservation_start_time, registration_confirmation_mail_subject, registration_confirmation_mail_content_template, request_confirmation_mail_subject, request_confirmation_mail_content_template, registration_fields FROM event WHERE key = @EventKey", {| EventKey = eventKey |}) |> Async.AwaitTask
            match Seq.toList dbEvents with
            | [ dbEvent ] ->
                let! dbEventSlots = connection.QueryAsync<DbEventSlot>("SELECT time, duration, closing_date, max_quantity_per_booking, remaining_capacity, can_request_if_fully_booked FROM event_slot WHERE event_key = @EventKey", {| EventKey = eventKey |}) |> Async.AwaitTask
                return DbEvent.toDomain dbEvent (Seq.toArray dbEventSlots) |> Some
            | _ -> return None
        }

        member _.TryBook bookingData = async {
            use! connection = dataSource.OpenConnectionAsync().AsTask() |> Async.AwaitTask
            use! tx = connection.BeginTransactionAsync().AsTask() |> Async.AwaitTask
            let! remainingCapacity = connection.QuerySingleAsync<Nullable<int>>("SELECT remaining_capacity FROM event_slot WHERE event_key = @EventKey AND time = @Time FOR UPDATE", {| EventKey = bookingData.EventKey; Time = bookingData.SlotTime |}, tx) |> Async.AwaitTask
            let insertRegistration = async {
                do! connection.ExecuteAsync(
                        "INSERT INTO event_registration (event_key, time, is_request, quantity, data, time_stamp) VALUES (@EventKey, @Time, FALSE, @Quantity, @Data::jsonb, @Timestamp)",
                        {|
                            EventKey = bookingData.EventKey
                            Time = bookingData.SlotTime
                            Quantity = bookingData.Subscriber.Quantity.Value
                            Data = JsonSerializer.Serialize bookingData.Subscriber.Data.Value
                            Timestamp = bookingData.Timestamp
                        |},
                        tx
                    )
                    |> Async.AwaitTask |> Async.Ignore
            }
            match Option.ofNullable remainingCapacity with
            | Some remainingCapacity when remainingCapacity < bookingData.Subscriber.Quantity.Value ->
                return Error (Domain.CapacityExceeded remainingCapacity)
            | Some remainingCapacity ->
                do! insertRegistration
                do! connection.ExecuteAsync("UPDATE event_slot SET remaining_capacity = remaining_capacity - @Quantity WHERE event_key = @EventKey AND time = @Time", {| EventKey = bookingData.EventKey; Time = bookingData.SlotTime; Quantity = bookingData.Subscriber.Quantity.Value |}, tx) |> Async.AwaitTask |> Async.Ignore
                do! tx.CommitAsync() |> Async.AwaitTask
                return Ok (Some (remainingCapacity - bookingData.Subscriber.Quantity.Value))
            | None ->
                do! insertRegistration
                do! tx.CommitAsync() |> Async.AwaitTask
                return Ok None
        }
        member _.AddBookingRequest bookingData = async {
            use! connection = dataSource.OpenConnectionAsync().AsTask() |> Async.AwaitTask
            do! connection.ExecuteAsync(
                    "INSERT INTO event_registration (event_key, time, is_request, quantity, data, time_stamp) VALUES (@EventKey, @Time, TRUE, @Quantity, @Data::jsonb, @Timestamp)",
                    {|
                        EventKey = bookingData.EventKey
                        Time = bookingData.SlotTime
                        Quantity = bookingData.Subscriber.Quantity.Value
                        Data = JsonSerializer.Serialize bookingData.Subscriber.Data.Value
                        Timestamp = bookingData.Timestamp
                    |}
                )
                |> Async.AwaitTask |> Async.Ignore
        }
