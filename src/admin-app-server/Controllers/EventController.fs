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
        let! eventsWithAuthorization =
            events
            |> List.map (fun eventData -> async {
                let! registrationsAuthorization = authService.AuthorizeAsync(this.User, eventData, "ReadEventRegistrations") |> Async.AwaitTask
                let! updateAuthorization = authService.AuthorizeAsync(this.User, eventData, "UpdateEvent") |> Async.AwaitTask
                return eventData, registrationsAuthorization.Succeeded, updateAuthorization.Succeeded
            })
            |> Async.Sequential
        let! createEventAuthorization = authService.AuthorizeAsync(this.User, "CreateEvent") |> Async.AwaitTask
        return {|
            Events =
                eventsWithAuthorization
                |> Array.map (fun (eventData, isAllowedToViewRegistrations, isAllowedToUpdate) ->
                    let event = Domain.Event.fromEventData timeProvider eventData
                    DtoMapping.Event.fromDomain (timeProvider.GetLocalNow().DateTime) (getEventUrl eventData.Key) (getPublicRegistrationUrl eventData.Key) getRegistrationUrl (getSlotRegistrations eventData.Key) isAllowedToViewRegistrations isAllowedToUpdate event
                )
            CreateEventUrl = if createEventAuthorization.Succeeded then Some (this.Url.Action(nameof(this.CreateEvent))) else None
        |}
    }

    [<HttpPost>]
    [<Authorize(Policy = "CreateEvent")>]
    member _.CreateEvent([<FromBody>]data: DataTransfer.CreateEventData) = async {
        // creator must be an editor, otherwise event writers couldn't edit their own events
        match UserClaims.tryGetMailAddress this.User with
        | None -> return this.Forbid() :> IActionResult
        | Some mailAddress ->
        match DtoMapping.CreateEventData.tryToDomain [| mailAddress |] data with
        | None -> return this.BadRequest()
        | Some event when String.IsNullOrWhiteSpace event.Key || String.IsNullOrWhiteSpace event.Title -> return this.BadRequest()
        | Some event when not (event.Slots |> Array.forall (Domain.Slot.isInFuture (timeProvider.GetLocalNow().DateTime))) -> return this.BadRequest()
        | Some event when not (Domain.EventData.hasRequiredRequestConfirmationMail event) -> return this.BadRequest()
        | Some event when not (Domain.RegistrationField.isValidFieldList event.RegistrationFields) -> return this.BadRequest()
        | Some event ->
            match! eventStore.CreateEvent event with
            | Ok () -> return this.Created(getEventUrl event.Key, null)
            | Error Domain.EventKeyAlreadyExists -> return this.Conflict()
    }

    [<HttpPatch("{eventKey}")>]
    member _.PatchEvent(eventKey: string, [<FromBody>]data: DataTransfer.PatchEventData) = async {
        match! checkEventAuthorization eventKey "UpdateEvent" with
        | EventNotFound -> return this.NotFound() :> IActionResult
        | EventAuthorizationFailed _ -> return this.Forbid()
        | EventAuthorizationSucceeded event ->
            match DtoMapping.PatchEventData.tryToDomain data with
            | None -> return this.BadRequest()
            | Some update when not (Domain.EventPermissions.allowsUpdate (timeProvider.GetLocalNow().DateTime) (Domain.Event.fromEventData timeProvider event) update) -> return this.BadRequest()
            | Some update when not (Domain.SlotUpdate.createsOnlyFutureSlots (timeProvider.GetLocalNow().DateTime) update.Slots) -> return this.BadRequest()
            | Some update when not (Domain.EventData.applyUpdate update event |> Domain.EventData.hasRequiredRequestConfirmationMail) -> return this.BadRequest()
            | Some update when not (update.RegistrationFields |> Option.forall Domain.RegistrationField.isValidFieldList) -> return this.BadRequest()
            | Some update ->
                do! eventStore.UpdateEvent event.Key update
                return this.Ok()
    }

    [<HttpDelete("{eventKey}")>]
    member _.DeleteEvent(eventKey: string) = async {
        match! checkEventAuthorization eventKey "UpdateEvent" with
        | EventNotFound -> return this.NotFound() :> IActionResult
        | EventAuthorizationFailed _ -> return this.Forbid()
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
            | EventAuthorizationFailed _ -> return this.Forbid()
            | EventAuthorizationSucceeded _ ->
                do! eventStore.CancelEventRegistration registrationId (timeProvider.GetLocalNow().DateTime)
                return this.Ok()
    }
