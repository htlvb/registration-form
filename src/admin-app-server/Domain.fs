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
type EventUpdateData = {
    Key: string option
    Title: string option
    InfoText: string option
    ReservationStartTime: DateTime option
    Slots: SlotUpdate[]
    RegistrationConfirmationMail: MailTemplate option
    RequestConfirmationMail: MailTemplate option option
}

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
