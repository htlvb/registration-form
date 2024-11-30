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

    let getSlotUrl eventKey slot =
        let createRequest =
            match slot.Type with
            | SlotTypeFree _
            | SlotTypeTaken
            | SlotTypeClosed -> false
            | SlotTypeTakenWithRequestPossible _ -> true
        this.Url.Action(
            nameof(this.CreateRegistration),
            {|
                eventKey = eventKey
                slot = slot.StartTime.ToString("yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture)
                createRequest = createRequest
            |}
        )

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
    member this.CreateRegistration (eventKey: string, slot: string, [<FromQuery>]createRequest: Nullable<bool>, [<FromBody>] subscriber: DataTransfer.Subscriber) = async {
        let! eventData = eventStore.TryGetEvent eventKey
        match DtoParsing.Subscriber.parse timeProvider eventData slot subscriber with
        | Error errors -> return this.BadRequest(errors |> List.map (DtoMapping.BookingValidationError.fromDomain (getSlotUrl eventKey))) :> IActionResult
        | Ok (DtoParsing.Subscriber.CanRequestBooking (bookingData, slot, bookingRequestConfirmationDataOpt)) ->
            if createRequest.GetValueOrDefault(false) then
                do! eventStore.AddBookingRequest bookingData
                let! mailSendSucceeded =
                    match bookingRequestConfirmationDataOpt with
                    | Some bookingRequestConfirmationData ->
                        async {
                            try
                                do! bookingConfirmation.SendBookingConfirmation bookingRequestConfirmationData
                                return true
                            with _ -> return false
                        }
                    | None -> async { return false }
                let requestBookingResult: DataTransfer.RequestBookingResult = {
                    MailSendError = mailSendSucceeded
                }
                return this.Ok(requestBookingResult)
            else
                let newSlotType = DtoMapping.SlotType.fromDomain (getSlotUrl bookingData.EventKey slot) slot.Type
                return this.BadRequest([{| Error = "slot-needs-request"; SlotType = newSlotType |}])
        | Ok (DtoParsing.Subscriber.CanBook (bookingData, slot, bookingConfirmationData)) ->
            match! eventStore.TryBook bookingData with
            | Error (CapacityExceeded remainingCapacity) ->
                let newSlotType =
                    SlotType.setCapacity slot.Type (Some remainingCapacity)
                    |> DtoMapping.SlotType.fromDomain (getSlotUrl bookingData.EventKey slot)
                return this.BadRequest([{| Error = "capacity-exceeded"; SlotType = newSlotType |}])
            | Ok remainingCapacity ->
                let newSlotType =
                    SlotType.setCapacity slot.Type remainingCapacity
                    |> DtoMapping.SlotType.fromDomain (getSlotUrl bookingData.EventKey slot)
                let! mailSendSucceeded = async {
                    try
                        do! bookingConfirmation.SendBookingConfirmation bookingConfirmationData
                        return true
                    with _ -> return false
                }
                let bookingResult: DataTransfer.BookingResult = {
                    SlotType = newSlotType
                    MailSendError = mailSendSucceeded
                }
                return this.Ok(bookingResult)
    }