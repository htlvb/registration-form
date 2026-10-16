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

type RegistrationFieldType =
    | TextField
    | MailField
    | PhoneField
type RegistrationFieldValidator =
    | NoValidation
    | NotEmpty
type RegistrationField = {
    /// e.g. used as placeholder in mail templates
    Key: string
    Label: string
    Type: RegistrationFieldType
    Validator: RegistrationFieldValidator
}
module RegistrationField =
    let defaultFields = [|
        { Key = "FullName"; Label = "Name"; Type = TextField; Validator = NotEmpty }
        { Key = "MailAddress"; Label = "E-Mail-Adresse"; Type = MailField; Validator = NotEmpty }
        { Key = "PhoneNumber"; Label = "Telefonnummer"; Type = PhoneField; Validator = NotEmpty }
    |]

    /// placeholders in mail templates that are filled by the user app
    let reservedKeys = set [ "Date"; "Time" ]

    let isValidKey (key: string) =
        Text.RegularExpressions.Regex.IsMatch(key, @"^[A-Za-z][A-Za-z0-9_]*$") &&
        not (reservedKeys.Contains key)

    /// confirmation mails are sent to the first mail field, so it must always be filled
    let isValidFieldList (fields: RegistrationField[]) =
        fields |> Array.forall (fun v -> isValidKey v.Key && not (String.IsNullOrWhiteSpace v.Label)) &&
        (fields |> Array.distinctBy _.Key).Length = fields.Length &&
        fields |> Array.tryFind (fun v -> v.Type = MailField) |> Option.exists (fun v -> v.Validator = NotEmpty)

type EventData = {
    Key: string
    Title: string
    InfoText: string
    ReservationStartTime: DateTime
    Slots: Slot[]
    RegistrationConfirmationMail: MailTemplate
    RequestConfirmationMail: MailTemplate option
    RegistrationFields: RegistrationField[]
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
    RegistrationFields: RegistrationField[] option
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
            RegistrationFields = data.RegistrationFields |> Option.defaultValue event.RegistrationFields
            EditorIds = event.EditorIds
        }

    /// requests get a confirmation mail, so it must exist as soon as one slot allows requests
    let hasRequiredRequestConfirmationMail (event: EventData) =
        event.RequestConfirmationMail.IsSome ||
        event.Slots |> Array.forall (fun v -> not v.CanRequestIfFullyBooked)

type SlotEditPermission =
    | SlotOpen
    /// everything except start and end time can be edited
    | SlotTimesLocked
    | SlotLocked
module SlotEditPermission =
    let fromSlot (now: DateTime) event (slot: Slot) =
        match event with
        | DraftEvent _ -> SlotOpen
        // slot time can't change once there might be registrations for it
        | ReleasedEvent _ when Slot.isInFuture now slot -> SlotTimesLocked
        | ReleasedEvent _
        | ArchivedEvent _ -> SlotLocked

type EventPermissions = {
    CanDelete: bool
    CanEditEventData: bool
    CanAddSlot: bool
    CanDeleteSlot: bool
    CanEditRegistrationFields: bool
    CanViewRegistrations: bool
}
module EventPermissions =
    let fromEvent = function
        // registration fields can't change once there might be registrations
        | DraftEvent _ -> { CanDelete = true; CanEditEventData = true; CanAddSlot = true; CanDeleteSlot = true; CanEditRegistrationFields = true; CanViewRegistrations = false }
        | ReleasedEvent _ -> { CanDelete = false; CanEditEventData = true; CanAddSlot = true; CanDeleteSlot = false; CanEditRegistrationFields = false; CanViewRegistrations = true }
        | ArchivedEvent _ -> { CanDelete = false; CanEditEventData = false; CanAddSlot = false; CanDeleteSlot = false; CanEditRegistrationFields = false; CanViewRegistrations = true }

    /// for users that aren't allowed to update the event
    let withoutUpdates permissions =
        { permissions with CanDelete = false; CanEditEventData = false; CanAddSlot = false; CanDeleteSlot = false; CanEditRegistrationFields = false }

    let allowsUpdate (now: DateTime) (event: Event) (update: EventUpdateData) =
        let permissions = fromEvent event
        let eventData =
            match event with
            | DraftEvent v | ReleasedEvent v | ArchivedEvent v -> v
        let changesEventData =
            update.Key.IsSome ||
            update.Title.IsSome ||
            update.InfoText.IsSome ||
            update.ReservationStartTime.IsSome ||
            update.RegistrationConfirmationMail.IsSome ||
            update.RequestConfirmationMail.IsSome
        let allowsSlotUpdate = function
            | CreateSlot _ -> permissions.CanAddSlot
            | UpdateSlot (time, data) ->
                match eventData.Slots |> Array.tryFind (fun v -> v.Time = time) with
                | Some slot ->
                    match SlotEditPermission.fromSlot now event slot with
                    | SlotOpen -> true
                    | SlotTimesLocked -> data.Time.IsNone && data.Duration.IsNone
                    | SlotLocked -> false
                | None -> false
            | DeleteSlot _ -> permissions.CanDeleteSlot
        (not changesEventData || permissions.CanEditEventData) &&
        (update.RegistrationFields.IsNone || permissions.CanEditRegistrationFields) &&
        update.Slots |> Array.forall allowsSlotUpdate

type EventRegistration = {
    Id: string
    EventKey: string
    Time: DateTime
    Quantity: int
    /// values of registration fields by key
    Data: Map<string, string>
    Timestamp: DateTime
    IsRequest: bool
    DeregistrationTime: DateTime option
}
