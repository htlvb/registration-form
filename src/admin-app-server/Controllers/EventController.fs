namespace HTLVB.RegistrationForm.Admin.Server.Controllers

open HTLVB.RegistrationForm.Admin.Server
open HTLVB.RegistrationForm.Admin.Server.Domain
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

    let getSlotUrl (eventKey: string) (slot: Domain.Slot) =
        let slotQueryParam = slot.Time.ToString("yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture)
        this.Url.Action(nameof(this.PatchSlot), {| eventKey = eventKey; slot = slotQueryParam |})

    [<HttpGet>]
    member _.GetEvents() = async {
        let! events = eventStore.GetEvents()
        return events
        |> List.map (fun eventData ->
            let event = Event.fromEventData timeProvider eventData
            DtoMapping.Event.fromDomain (getEventUrl eventData.Key) (getSlotUrl eventData.Key) event
        )
    }

    [<HttpPatch("{eventKey}")>]
    member _.PatchEvent(eventKey: string, [<FromBody>]data: DataTransfer.PatchEventData) = async {
        let update = DtoMapping.PatchEventData.toDomain data
        do! eventStore.UpdateEvent eventKey update
    }

    [<HttpPatch("{eventKey}/{slot}")>]
    member _.PatchSlot(eventKey: string, slot: string, [<FromBody>]data: DataTransfer.PatchSlotData) =
        ()

    [<HttpGet("{eventKey}/registrations")>]
    member _.GetEventRegistrations (eventKey: string) =
        eventStore.GetEventRegistrations eventKey
