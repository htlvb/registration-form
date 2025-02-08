namespace HTLVB.RegistrationForm.Admin.Server.Controllers

open HTLVB.RegistrationForm.Admin.Server
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open System
open System.Globalization

[<ApiController>]
[<Route("/api/events")>]
type EventController (eventStore: IEventStore, timeProvider: TimeProvider, logger : ILogger<EventController>) as this =
    inherit ControllerBase()

    let getEventUrl (eventKey: string) =
        this.Url.Action(nameof(this.PatchEvent),  {| eventKey = eventKey |})

    let getSlotRegistrationsUrl (eventKey: string) (slot: Domain.Slot) =
        let slotQueryParam = slot.Time.ToString("yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture)
        this.Url.Action(nameof(this.GetEventSlotRegistrations), {| eventKey = eventKey; slot = slotQueryParam |})

    let getRegistrationUrl (registration: Domain.EventRegistration) =
        this.Url.Action(nameof(this.CancelRegistration), {| registrationId = registration.Id |})

    [<HttpGet>]
    member _.GetEvents() = async {
        let! events = eventStore.GetEvents()
        return events
        |> List.map (fun eventData ->
            let event = Domain.Event.fromEventData timeProvider eventData
            DtoMapping.Event.fromDomain (getEventUrl eventData.Key) (getSlotRegistrationsUrl eventData.Key) event
        )
    }

    [<HttpPatch("{eventKey}")>]
    member _.PatchEvent(eventKey: string, [<FromBody>]data: DataTransfer.PatchEventData) = async {
        let update = DtoMapping.PatchEventData.toDomain data
        do! eventStore.UpdateEvent eventKey update
    }

    [<HttpGet("{eventKey}/{slot}/registrations")>]
    member this.GetEventSlotRegistrations (eventKey: string) (slot: string) = async {
        match DtoParsing.DateTime.tryParse slot with
        | Some slotTime ->
            let! registrations = eventStore.GetEventRegistrations eventKey slotTime
            let registrationDtos = [
                for registration in registrations do
                    let registrationUrl = getRegistrationUrl registration
                    DtoMapping.EventRegistrations.fromDomain registrationUrl registration
            ]
            return this.Ok(registrationDtos) :> IActionResult
        | None -> return this.NotFound()
    }

    [<HttpDelete("registrations/{registrationId}")>]
    member _.CancelRegistration (registrationId: string) = async {
        do! eventStore.CancelEventRegistration registrationId (timeProvider.GetLocalNow().DateTime)
    }
