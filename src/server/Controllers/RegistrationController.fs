namespace HTLVB.RegistrationForm.Server.Controllers

open HTLVB.RegistrationForm.Server
open HTLVB.RegistrationForm.Server.Domain
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Options
open System
open System.Text.Json
open System.Globalization

[<ApiController>]
[<Route("/api/event")>]
type RegistrationController(
    eventStore: IEventStore,
    bookingConfirmation: IBookingConfirmationSender,
    timeProvider: TimeProvider,
    jsonOptions: IOptions<JsonOptions>) as this =
    inherit ControllerBase()

    let getSlotType eventKey slot =
        match slot.ClosingDate with
        | Some closingDate when timeProvider.GetLocalNow().DateTime >= closingDate ->
            DataTransfer.SlotTypeClosed() :> DataTransfer.SlotType
        | _ ->
            match slot.RemainingCapacity with
            | Some v when v <= 0 -> DataTransfer.SlotTypeTaken()
            | remainingCapacity ->
                let url = this.Url.Action(nameof(this.CreateRegistration), {| eventKey = eventKey; slot = slot.Time.ToString("yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture) |})
                DataTransfer.SlotTypeFree (
                    url,
                    Option.toNullable slot.ClosingDate,
                    Option.toNullable slot.MaxQuantityPerBooking,
                    Option.toNullable remainingCapacity
                )

    let getSlot eventKey slot : DataTransfer.Slot =
        {
            StartTime = slot.Time
            Type = getSlotType eventKey slot
        }

    [<HttpGet("{eventKey?}")>]
    member this.GetRegistrations (eventKey: string) = async {
        match! eventStore.TryGetEvent eventKey with
        | None -> return this.NotFound() :> IActionResult
        | Some event ->
            let! eventDto = async {
                if event.ReservationStartTime > timeProvider.GetLocalNow().DateTime then
                    return DataTransfer.HiddenEvent(event.Title, event.ReservationStartTime) :> DataTransfer.Event
                else
                    return DataTransfer.ReleasedEvent(event.Title, event.InfoText, [ for slot in event.Slots -> getSlot event.Key slot ])
            }
            return this.Ok(JsonSerializer.SerializeToElement(eventDto, jsonOptions.Value.JsonSerializerOptions))
    }

    [<HttpPost("{eventKey}/registration/{slot}")>]
    member this.CreateRegistration (eventKey: string, slot: string, [<FromBody()>] subscriber: DataTransfer.Subscriber) = async {
        match! eventStore.TryGetEvent eventKey with
        | None -> return this.NotFound() :> IActionResult
        | Some event ->
            match DateTime.TryParseExact(slot, "yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.None) with
            | (false, _) -> return this.BadRequest() :> IActionResult
            | (true, slotTime) ->
                let now = timeProvider.GetLocalNow().DateTime
                let slot = event.Slots |> Seq.tryFind (fun v -> v.Time = slotTime)
                match slot with
                | None -> return this.NotFound()
                | Some { ClosingDate = Some closingDate } when closingDate <= now -> return this.BadRequest()
                | Some slot ->
                    match Subscriber.validate subscriber with
                    | Error _ -> return this.BadRequest()
                    | Ok subscriber ->
                        match slot.MaxQuantityPerBooking with
                        | Some maxQuantityPerBooking when subscriber.Quantity > maxQuantityPerBooking ->
                            return this.BadRequest ()
                        | _ ->
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
                                    return this.BadRequest([{| Error = "capacity-exceeded"; SlotType = getSlotType event.Key { slot with RemainingCapacity = Some remainingCapacity } |}])
                                | Ok remainingCapacity ->
                                    let newSlotType = getSlotType event.Key { slot with RemainingCapacity = remainingCapacity }
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
                                    try
                                        do! bookingConfirmation.SendBookingConfirmation mailSettings
                                        let bookingResult: DataTransfer.BookingResult = {
                                            SlotType = newSlotType
                                            MailSendError = false
                                        }
                                        return this.Ok(bookingResult)
                                    with _ ->
                                        let bookingResult: DataTransfer.BookingResult = {
                                            SlotType = newSlotType
                                            MailSendError = true
                                        }
                                        return this.Ok(bookingResult)
    }