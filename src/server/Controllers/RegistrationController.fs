namespace HTLVB.RegistrationForm.Server.Controllers

open HTLVB.RegistrationForm.Server
open HTLVB.RegistrationForm.Server.Domain
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Options
open System
open System.Text.Json
open System.Globalization

[<ApiController>]
[<Route("/api/schedule")>]
type RegistrationController(
    eventStore: IEventStore,
    bookingConfirmation: IBookingConfirmationSender,
    timeProvider: TimeProvider,
    jsonOptions: IOptions<JsonOptions>) as this =
    inherit ControllerBase()

    let getReservationType eventKey slot =
        match slot.RemainingCapacity with
        | Some v when v <= 0 -> DataTransfer.ReservationTypeTaken() :> DataTransfer.ReservationType
        | remainingCapacity ->
            let url = this.Url.Action(nameof(this.CreateRegistration), {| eventKey = eventKey; slot = slot.Time.ToString("yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture) |})
            DataTransfer.ReservationTypeFree (url, Option.toNullable slot.MaxQuantityPerBooking, Option.toNullable remainingCapacity)

    let getScheduleEntry eventKey slot : DataTransfer.ScheduleEntry =
        {
            StartTime = slot.Time
            ReservationType = getReservationType eventKey slot
        }
                        

    [<HttpGet("{eventKey}")>]
    member this.GetRegistrations (eventKey: string) = async {
        match! eventStore.TryGetEvent eventKey with
        | None -> return this.NotFound() :> IActionResult
        | Some event ->
            let! schedule = async {
                if event.ReservationStartTime > timeProvider.GetLocalNow().DateTime then
                    return DataTransfer.HiddenSchedule(event.Title, event.ReservationStartTime) :> DataTransfer.Schedule
                else
                    return DataTransfer.ReleasedSchedule(event.Title, event.InfoText, [ for slot in event.Slots -> getScheduleEntry event.Key slot ])
            }
            return this.Ok(JsonSerializer.SerializeToElement(schedule, jsonOptions.Value.JsonSerializerOptions))
    }

    [<HttpPost("{eventKey}/registration/{slot}")>]
    member this.CreateRegistration (eventKey: string, slot: string, [<FromBody()>] subscriber: DataTransfer.Subscriber) = async {
        match! eventStore.TryGetEvent eventKey with
        | None -> return this.NotFound() :> IActionResult
        | Some event ->
            match DateTime.TryParseExact(slot, "yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.None) with
            | (false, _) -> return this.BadRequest() :> IActionResult
            | (true, slotTime) ->
                let slot = event.Slots |> Seq.tryFind (fun v -> v.Time = slotTime)
                match slot with
                | None -> return this.NotFound()
                | Some slot ->
                    match Subscriber.validate subscriber with
                    | Error _ -> return this.BadRequest()
                    | Ok subscriber ->
                        match slot.MaxQuantityPerBooking with
                        | Some maxQuantityPerBooking when subscriber.Quantity > maxQuantityPerBooking ->
                            return this.BadRequest ()
                        | _ ->
                            let now = timeProvider.GetLocalNow().DateTime
                            if now < event.ReservationStartTime || now > slotTime then
                                return this.BadRequest ()
                            else
                                let! remainingCapacity = eventStore.TryBook event.Key {
                                    time = slotTime
                                    quantity = subscriber.Quantity
                                    name = subscriber.Name
                                    mail_address = subscriber.MailAddress
                                    phone_number = subscriber.PhoneNumber
                                    time_stamp = now
                                }
                                match remainingCapacity with
                                | Error (CapacityExceeded remainingCapacity) ->
                                    return this.BadRequest({| Error = "CapacityExceeded"; RemainingCapacity = remainingCapacity |})
                                | Ok remainingCapacity ->
                                    let newReservationType = getReservationType event.Key { slot with RemainingCapacity = remainingCapacity }
                                    let mailSettings = {
                                        Recipient = {
                                            Name = subscriber.Name
                                            MailAddress = subscriber.MailAddress
                                        }
                                        Subject =  event.MailSubject
                                        Content =
                                            let templateVars = [
                                                "FullName", subscriber.Name
                                                "Date", slot.Time.ToString("d", CultureInfo.GetCultureInfo("de-AT"))
                                                "Time", slot.Time.ToString("t", CultureInfo.GetCultureInfo("de-AT"))
                                            ]
                                            (event.MailContentTemplate, templateVars)
                                            ||> List.fold (fun text (varName, value) -> text.Replace(sprintf "{{{%s}}}" varName, value))
                                    }
                                    do! bookingConfirmation.SendBookingConfirmation mailSettings
                                    return this.Ok(JsonSerializer.SerializeToElement(newReservationType, jsonOptions.Value.JsonSerializerOptions)) :> IActionResult
    }