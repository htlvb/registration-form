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

    let getSlotUrl eventKey slot =
        this.Url.Action(nameof(this.CreateRegistration), {| eventKey = eventKey; slot = slot.StartTime.ToString("yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture) |})

    [<HttpGet("{eventKey?}")>]
    member this.GetEvent (eventKey: string) = async {
        match! eventStore.TryGetEvent eventKey with
        | None -> return this.NotFound() :> IActionResult
        | Some event ->
            return
                event
                |> Event.fromEventData timeProvider
                |> DtoMapping.Event.fromDomain (getSlotUrl eventKey)
                |> fun eventDto -> JsonSerializer.SerializeToElement(eventDto, jsonOptions.Value.JsonSerializerOptions)
                |> this.Ok
                :> IActionResult
    }

    [<HttpPost("{eventKey}/registration/{slot}")>]
    member this.CreateRegistration (eventKey: string, slot: string, [<FromBody()>] subscriber: DataTransfer.Subscriber) = async {
        let! eventData = eventStore.TryGetEvent eventKey
        match DtoParsing.Subscriber.parse timeProvider eventData slot subscriber with
        | Error errors -> return this.BadRequest(errors |> List.map (DtoMapping.BookingValidationError.fromDomain (getSlotUrl eventKey))) :> IActionResult
        | Ok (event, slot, subscriber) ->
            let! remainingCapacity = eventStore.TryBook event.Key {
                time = slot.StartTime
                quantity = subscriber.Quantity.Value
                name = subscriber.Name.Value
                mail_address = subscriber.MailAddress.Value
                phone_number = subscriber.PhoneNumber.Value
                time_stamp = timeProvider.GetLocalNow().DateTime
            }
            match remainingCapacity with
            | Error (CapacityExceeded remainingCapacity) ->
                let newSlotType =
                    SlotType.setCapacity slot.Type (Some remainingCapacity)
                    |> DtoMapping.SlotType.fromDomain (getSlotUrl event.Key slot)
                return this.BadRequest([{| Error = "capacity-exceeded"; SlotType = newSlotType |}])
            | Ok remainingCapacity ->
                let newSlotType =
                    SlotType.setCapacity slot.Type remainingCapacity
                    |> DtoMapping.SlotType.fromDomain (getSlotUrl event.Key slot)
                let mailSettings = {
                    Recipient = {
                        Name = subscriber.Name.Value
                        MailAddress = subscriber.MailAddress.Value
                    }
                    Subject =  event.MailSubject
                    Content =
                        let templateVars = [
                            "FullName", subscriber.Name.Value
                            "Date", slot.StartTime.ToString("d", CultureInfo.GetCultureInfo("de-AT"))
                            "Time", slot.StartTime.ToString("t", CultureInfo.GetCultureInfo("de-AT"))
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