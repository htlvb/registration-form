namespace HTLVB.RegistrationForm.Admin.Server.Controllers

open HTLVB.RegistrationForm.Admin.Server
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open System
open System.Globalization

type PublicRegistrationSettings = {
    BaseUrl: Uri
}

type private CheckEventAuthorizationResult =
    | EventNotFound
    | EventAuthorizationFailed of AuthorizationFailure
    | EventAuthorizationSucceeded of Domain.EventData

[<ApiController>]
[<Route("/api/events")>]
[<Authorize>]
type EventController (eventStore: IEventStore, timeProvider: TimeProvider, authService: IAuthorizationService, publicRegistrationSettings: PublicRegistrationSettings, logger : ILogger<EventController>) as this =
    inherit ControllerBase()

    let checkEventAuthorization eventKey (policyName: string) = async {
        match! eventStore.TryGetEvent eventKey with
        | None -> return EventNotFound
        | Some event ->
            match! authService.AuthorizeAsync(this.User, event, policyName) |> Async.AwaitTask with
            | v when v.Succeeded -> return EventAuthorizationSucceeded event
            | v -> return EventAuthorizationFailed v.Failure
    }

    let getEventUrl (eventKey: string) =
        this.Url.Action(nameof(this.PatchEvent),  {| eventKey = eventKey |})

    let getPublicRegistrationUrl (eventKey: string) =
        Uri(publicRegistrationSettings.BaseUrl, Uri.EscapeDataString eventKey).ToString()

    let getSlotRegistrationsUrl (eventKey: string) (slot: Domain.Slot) =
        let slotQueryParam = slot.Time.ToString("yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture)
        this.Url.Action(nameof(this.GetEventSlotRegistrations), {| eventKey = eventKey; slot = slotQueryParam |})

    let getRegistrationUrl (registration: Domain.EventRegistration) =
        this.Url.Action(nameof(this.CancelRegistration), {| registrationId = registration.Id |})

    [<HttpGet>]
    [<Authorize(Policy = "ReadEvent")>]
    member _.GetEvents() = async {
        let! events = eventStore.GetEvents()
        let! createEventAuthorization = authService.AuthorizeAsync(this.User, "CreateEvent") |> Async.AwaitTask
        return {|
            Events =
                events
                |> List.map (fun eventData ->
                    let event = Domain.Event.fromEventData timeProvider eventData
                    DtoMapping.Event.fromDomain (getEventUrl eventData.Key) (getPublicRegistrationUrl eventData.Key) (getSlotRegistrationsUrl eventData.Key) event
                )
            CreateEventUrl = if createEventAuthorization.Succeeded then Some (this.Url.Action(nameof(this.CreateEvent))) else None
        |}
    }

    [<HttpPost>]
    [<Authorize(Policy = "CreateEvent")>]
    member _.CreateEvent([<FromBody>]data: DataTransfer.CreateEventData) = async {
        if String.IsNullOrWhiteSpace data.Key || String.IsNullOrWhiteSpace data.Title then
            return this.BadRequest() :> IActionResult
        else
            // creator must be an editor, otherwise event writers couldn't edit their own events
            let editorIds = this.User.Identity.Name |> Option.ofObj |> Option.toArray
            let event = DtoMapping.CreateEventData.toDomain editorIds data
            match! eventStore.CreateEvent event with
            | Ok () -> return this.Created(getEventUrl event.Key, null)
            | Error Domain.EventKeyAlreadyExists -> return this.Conflict()
    }

    [<HttpPatch("{eventKey}")>]
    member _.PatchEvent(eventKey: string, [<FromBody>]data: DataTransfer.PatchEventData) = async {
        match! checkEventAuthorization eventKey "UpdateEvent" with
        | EventNotFound -> return this.NotFound() :> IActionResult
        | EventAuthorizationFailed _ -> return this.Unauthorized()
        | EventAuthorizationSucceeded event ->
            let update = DtoMapping.PatchEventData.toDomain data
            let permissions = Domain.Event.fromEventData timeProvider event |> Domain.EventPermissions.fromEvent
            if not (Domain.EventPermissions.allowsUpdate update permissions) then
                return this.BadRequest()
            else
                do! eventStore.UpdateEvent event.Key update
                return this.Ok()
    }

    [<HttpDelete("{eventKey}")>]
    member _.DeleteEvent(eventKey: string) = async {
        match! checkEventAuthorization eventKey "UpdateEvent" with
        | EventNotFound -> return this.NotFound() :> IActionResult
        | EventAuthorizationFailed _ -> return this.Unauthorized()
        | EventAuthorizationSucceeded event ->
            let permissions = Domain.Event.fromEventData timeProvider event |> Domain.EventPermissions.fromEvent
            if not permissions.CanDelete then
                return this.BadRequest()
            else
                do! eventStore.DeleteEvent event.Key
                return this.Ok()
    }

    [<HttpGet("{eventKey}/{slot}/registrations")>]
    member _.GetEventSlotRegistrations (eventKey: string) (slot: string) = async {
        match! checkEventAuthorization eventKey "ReadEventRegistrations" with
        | EventNotFound -> return this.NotFound() :> IActionResult
        | EventAuthorizationFailed _ -> return this.Unauthorized()
        | EventAuthorizationSucceeded event ->
            match DtoParsing.DateTime.tryParse slot with
            | Some slotTime ->
                let! registrations = eventStore.GetEventRegistrations event.Key slotTime
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
        let! registration = eventStore.GetEventRegistration registrationId
        match registration with
        | None -> return this.NotFound() :> IActionResult
        | Some registration ->
            match! checkEventAuthorization registration.EventKey "UpdateEventRegistrations" with
            | EventNotFound -> return this.NotFound()
            | EventAuthorizationFailed _ -> return this.Unauthorized()
            | EventAuthorizationSucceeded _ ->
                do! eventStore.CancelEventRegistration registrationId (timeProvider.GetLocalNow().DateTime)
                return this.Ok()
    }
