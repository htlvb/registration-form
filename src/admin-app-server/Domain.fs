namespace HTLVB.RegistrationForm.Admin.Server.Domain

open System

type Slot = {
    Time: DateTime
    Duration: TimeSpan option
    ClosingDate: DateTime option
    MaxQuantityPerBooking: int option
    RemainingCapacity: int option
    CanRequestIfFullyBooked: bool
}
module Slot =
    let isInFuture (now: DateTime) (slot: Slot) = slot.Time > now

type MailTemplate = {
    Subject: string
    ContentTemplate: string
}
type EventData = {
    Key: string
    Title: string
    InfoText: string
    ReservationStartTime: DateTime
    Slots: Slot[]
    RegistrationConfirmationMail: MailTemplate
    RequestConfirmationMail: MailTemplate option
    EditorIds: string[]
}
type Event =
    | DraftEvent of EventData
    | ReleasedEvent of EventData
    | ArchivedEvent of EventData
module Event =
    let fromEventData (timeProvider: TimeProvider) (eventData: EventData) =
        let now = timeProvider.GetLocalNow().DateTime
        if eventData.ReservationStartTime > now then
            DraftEvent eventData
        elif eventData.Slots |> Seq.forall (fun v -> v.Time < now) then
            ArchivedEvent eventData
        else
            ReleasedEvent eventData

type CreateEventError =
    | EventKeyAlreadyExists

type SlotUpdateData = {
    Time: DateTime option
    Duration: TimeSpan option option
    ClosingDate: DateTime option option
    MaxQuantityPerBooking: int option option
    RemainingCapacity: int option option
    CanRequestIfFullyBooked: bool option
}
type SlotUpdate =
    | CreateSlot of Slot
    | UpdateSlot of DateTime * SlotUpdateData
    | DeleteSlot of DateTime
module SlotUpdate =
    let createsOnlyFutureSlots (now: DateTime) (updates: SlotUpdate seq) =
        updates |> Seq.forall (function
            | CreateSlot slot -> Slot.isInFuture now slot
            | UpdateSlot _ | DeleteSlot _ -> true
        )

type EventUpdateData = {
    Key: string option
    Title: string option
    InfoText: string option
    ReservationStartTime: DateTime option
    Slots: SlotUpdate[]
    RegistrationConfirmationMail: MailTemplate option
    RequestConfirmationMail: MailTemplate option option
}

module EventData =
    let private updateSlot (slot: Slot) (update: SlotUpdateData) : Slot =
        {
            Time = update.Time |> Option.defaultValue slot.Time
            Duration = update.Duration |> Option.defaultValue slot.Duration
            ClosingDate = update.ClosingDate |> Option.defaultValue slot.ClosingDate
            MaxQuantityPerBooking = update.MaxQuantityPerBooking |> Option.defaultValue slot.MaxQuantityPerBooking
            RemainingCapacity = update.RemainingCapacity |> Option.defaultValue slot.RemainingCapacity
            CanRequestIfFullyBooked = update.CanRequestIfFullyBooked |> Option.defaultValue slot.CanRequestIfFullyBooked
        }

    let private applySlotUpdate (slots: Slot[]) (update: SlotUpdate) =
        match update with
        | CreateSlot v -> [| v; yield! slots |]
        | UpdateSlot (time, update) ->
            slots |> Array.map (fun v -> if v.Time = time then updateSlot v update else v)
        | DeleteSlot time -> slots |> Array.filter (fun v -> v.Time <> time)

    let applyUpdate (data: EventUpdateData) (event: EventData) : EventData =
        {
            Key = data.Key |> Option.defaultValue event.Key
            Title = data.Title |> Option.defaultValue event.Title
            InfoText = data.InfoText |> Option.defaultValue event.InfoText
            ReservationStartTime = data.ReservationStartTime |> Option.defaultValue event.ReservationStartTime
            // same order as in the db: delete, update, create
            Slots =
                let deletes = data.Slots |> Array.filter (function DeleteSlot _ -> true | _ -> false)
                let updates = data.Slots |> Array.filter (function UpdateSlot _ -> true | _ -> false)
                let creates = data.Slots |> Array.filter (function CreateSlot _ -> true | _ -> false)
                Array.concat [ deletes; updates; creates ] |> Array.fold applySlotUpdate event.Slots
            RegistrationConfirmationMail = data.RegistrationConfirmationMail |> Option.defaultValue event.RegistrationConfirmationMail
            RequestConfirmationMail = data.RequestConfirmationMail |> Option.defaultValue event.RequestConfirmationMail
            EditorIds = event.EditorIds
        }

    /// requests get a confirmation mail, so it must exist as soon as one slot allows requests
    let hasRequiredRequestConfirmationMail (event: EventData) =
        event.RequestConfirmationMail.IsSome ||
        event.Slots |> Array.forall (fun v -> not v.CanRequestIfFullyBooked)

type EventPermissions = {
    CanDelete: bool
    CanEditEventData: bool
    CanAddSlot: bool
    CanEditSlot: bool
    CanDeleteSlot: bool
    CanViewRegistrations: bool
}
module EventPermissions =
    let fromEvent = function
        | DraftEvent _ -> { CanDelete = true; CanEditEventData = true; CanAddSlot = true; CanEditSlot = true; CanDeleteSlot = true; CanViewRegistrations = false }
        | ReleasedEvent _ -> { CanDelete = false; CanEditEventData = true; CanAddSlot = true; CanEditSlot = false; CanDeleteSlot = false; CanViewRegistrations = true }
        | ArchivedEvent _ -> { CanDelete = false; CanEditEventData = false; CanAddSlot = false; CanEditSlot = false; CanDeleteSlot = false; CanViewRegistrations = true }

    let allowsUpdate (update: EventUpdateData) permissions =
        let changesEventData =
            update.Key.IsSome ||
            update.Title.IsSome ||
            update.InfoText.IsSome ||
            update.ReservationStartTime.IsSome ||
            update.RegistrationConfirmationMail.IsSome ||
            update.RequestConfirmationMail.IsSome
        let allowsSlotUpdate = function
            | CreateSlot _ -> permissions.CanAddSlot
            | UpdateSlot _ -> permissions.CanEditSlot
            | DeleteSlot _ -> permissions.CanDeleteSlot
        (not changesEventData || permissions.CanEditEventData) &&
        update.Slots |> Array.forall allowsSlotUpdate

type EventRegistration = {
    Id: string
    EventKey: string
    Time: DateTime
    Quantity: int
    Name: string
    MailAddress: string
    PhoneNumber: string
    Timestamp: DateTime
    IsRequest: bool
    DeregistrationTime: DateTime option
}
