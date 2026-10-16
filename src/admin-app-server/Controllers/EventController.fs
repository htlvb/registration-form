namespace HTLVB.RegistrationForm.Admin.Server.Controllers

open HTLVB.RegistrationForm.Admin.Server
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open System

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

    let getRegistrationUrl (registration: Domain.EventRegistration) =
        this.Url.Action(nameof(this.CancelRegistration), {| registrationId = registration.Id |})

    [<HttpGet>]
    [<Authorize(Policy = "ReadEvent")>]
    member _.GetEvents() = async {
        let! events = eventStore.GetEvents()
        let! registrations = eventStore.GetAllEventRegistrations()
        let registrationMap = registrations |> List.groupBy (fun v -> v.EventKey, v.Time) |> Map.ofList
        let getSlotRegistrations (eventKey: string) (slot: Domain.Slot) =
            registrationMap |> Map.tryFind (eventKey, slot.Time) |> Option.defaultValue []
        let! eventsWithRegistrationAuthorization =
            events
            |> List.map (fun eventData -> async {
                let! authorization = authService.AuthorizeAsync(this.User, eventData, "ReadEventRegistrations") |> Async.AwaitTask
                return eventData, authorization.Succeeded
            })
            |> Async.Sequential
        let! createEventAuthorization = authService.AuthorizeAsync(this.User, "CreateEvent") |> Async.AwaitTask
        return {|
            Events =
                eventsWithRegistrationAuthorization
                |> Array.map (fun (eventData, isAllowedToViewRegistrations) ->
                    let event = Domain.Event.fromEventData timeProvider eventData
                    DtoMapping.Event.fromDomain (getEventUrl eventData.Key) (getPublicRegistrationUrl eventData.Key) getRegistrationUrl (getSlotRegistrations eventData.Key) isAllowedToViewRegistrations event
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
