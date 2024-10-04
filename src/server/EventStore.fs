namespace HTLVB.RegistrationForm.Server

open Dapper
open System
open Npgsql

type DbEventSlot = {
    time: DateTime
    remaining_capacity: Nullable<int>
}

type DbEvent = {
    key: string
    title: string
    info_text: string
    reservation_start_time: DateTime
    mail_subject: string
    mail_content_template: string
}

module DbEvent =
    let toDomain (dbEvent: DbEvent) (dbEventSlots: DbEventSlot[]) : Domain.Event =
        {
            Key = dbEvent.key
            Title = dbEvent.title
            InfoText = dbEvent.info_text
            ReservationStartTime = dbEvent.reservation_start_time
            Slots = dbEventSlots |> Array.map (fun (v) -> { Time = v.time; RemainingCapacity = Option.ofNullable v.remaining_capacity })
            MailSubject = dbEvent.mail_subject
            MailContentTemplate = dbEvent.mail_content_template
        }

type EventRegistration = {
    time: DateTime
    quantity: int
    name: string
    mail_address: string
    phone_number: string
    time_stamp: DateTime
}

type IEventStore =
    abstract member TryGetEvent: eventKey: string -> Async<Domain.Event option>
    abstract member GetEventRegistrations: eventKey: string -> Async<EventRegistration list>
    abstract member TryBook: eventKey: string -> data: EventRegistration -> Async<Result<int option, Domain.BookingError>>

type PgsqlEventStore(dataSource: NpgsqlDataSource) =
    interface IEventStore with
        member _.TryGetEvent eventKey = async {
            use! connection = dataSource.OpenConnectionAsync().AsTask() |> Async.AwaitTask
            let! dbEvents = connection.QueryAsync<DbEvent>("SELECT key, title, info_text, reservation_start_time, mail_subject, mail_content_template FROM event WHERE key = @EventKey", {| EventKey = eventKey |}) |> Async.AwaitTask
            match Seq.toList dbEvents with
            | [ dbEvent ] ->
                let! dbEventSlots = connection.QueryAsync<DbEventSlot>("SELECT time, remaining_capacity FROM event_slot WHERE event_key = @EventKey", {| EventKey = eventKey |}) |> Async.AwaitTask
                return DbEvent.toDomain dbEvent (Seq.toArray dbEventSlots) |> Some
            | _ -> return None
        }

        member _.GetEventRegistrations eventKey = async {
            use! connection = dataSource.OpenConnectionAsync().AsTask() |> Async.AwaitTask
            let! result = connection.QueryAsync<EventRegistration>("SELECT time, quantity, name, mail_address, phone_number, time_stamp FROM event_registration WHERE event_key = @EventKey", {| EventKey = eventKey |}) |> Async.AwaitTask
            return Seq.toList result
        }

        member _.TryBook eventKey data = async {
            use! connection = dataSource.OpenConnectionAsync().AsTask() |> Async.AwaitTask
            use! tx = connection.BeginTransactionAsync().AsTask() |> Async.AwaitTask
            let! remainingCapacity = connection.QuerySingleAsync<Nullable<int>>("SELECT remaining_capacity FROM event_slot WHERE event_key = @EventKey AND time = @Time", {| EventKey = eventKey; Time = data.time |}, tx) |> Async.AwaitTask
            match Option.ofNullable remainingCapacity with
            | Some remainingCapacity when remainingCapacity < data.quantity ->
                return Error (Domain.CapacityExceeded remainingCapacity)
            | Some remainingCapacity ->
                do! connection.ExecuteAsync("INSERT INTO event_registration (event_key, time, quantity, name, mail_address, phone_number, time_stamp) VALUES (@event_key, @time, @quantity, @name, @mail_address, @phone_number, @time_stamp)", {| data with event_key = eventKey |}, tx) |> Async.AwaitTask |> Async.Ignore
                do! connection.ExecuteAsync("UPDATE event_slot SET remaining_capacity = remaining_capacity - @Quantity WHERE event_key = @EventKey AND time = @Time", {| EventKey = eventKey; Time = data.time; Quantity = data.quantity |}, tx) |> Async.AwaitTask |> Async.Ignore
                do! tx.CommitAsync() |> Async.AwaitTask
                return Ok (Some (remainingCapacity - data.quantity))
            | None ->
                do! connection.ExecuteAsync("INSERT INTO event_registration (event_key, time, quantity, name, mail_address, phone_number, time_stamp) VALUES (@event_key, @time, @quantity, @name, @mail_address, @phone_number, @time_stamp)", {| data with event_key = eventKey |}, tx) |> Async.AwaitTask |> Async.Ignore
                do! tx.CommitAsync() |> Async.AwaitTask
                return Ok None
        }
