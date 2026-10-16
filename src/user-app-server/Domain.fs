namespace HTLVB.RegistrationForm.Server.Domain

open System

type SlotData = {
    Time: DateTime
    Duration: TimeSpan option
    ClosingDate: DateTime option
    MaxQuantityPerBooking: int option
    RemainingCapacity: int option
    CanRequestIfFullyBooked: bool
}
type SlotType =
    | SlotTypeFree of {| ClosingDate: DateTime option; MaxQuantityPerBooking: int option; RemainingCapacity: int option; CanRequestIfFullyBooked: bool |}
    | SlotTypeTakenWithRequestPossible of {| ClosingDate: DateTime option; MaxQuantityPerBooking: int option |}
    | SlotTypeTaken
    | SlotTypeClosed
module SlotType =
    let setCapacity slotType remainingCapacity =
        match slotType, remainingCapacity with
        | SlotTypeFree data, None -> SlotTypeFree {| data with RemainingCapacity = None |}
        | SlotTypeFree data, Some remainingCapacity when remainingCapacity > 0 -> SlotTypeFree {| data with RemainingCapacity = Some remainingCapacity |}
        | SlotTypeFree v, _ when v.CanRequestIfFullyBooked -> SlotTypeTakenWithRequestPossible {| ClosingDate = v.ClosingDate; MaxQuantityPerBooking = v.MaxQuantityPerBooking |}
        | SlotTypeFree _, _ -> SlotTypeTaken
        | (SlotTypeTakenWithRequestPossible _ as v), _
        | (SlotTypeTaken as v), _
        | (SlotTypeClosed as v), _ -> v

    let canBookQuantity quantity slotType =
        let maxQuantity =
            match slotType with
            | SlotTypeFree data -> Some data.MaxQuantityPerBooking
            | SlotTypeTakenWithRequestPossible data -> Some data.MaxQuantityPerBooking
            | SlotTypeTaken -> None
            | SlotTypeClosed -> None
        match maxQuantity with
        | None -> false
        | Some None -> true
        | Some (Some maxQuantity) -> quantity <= maxQuantity

    let fromSlotData (timeProvider: TimeProvider) slotData =
        let closingDate = slotData.ClosingDate |> Option.defaultValue slotData.Time
        if timeProvider.GetLocalNow().DateTime >= closingDate then SlotTypeClosed
        else
            match slotData.RemainingCapacity with
            | Some v when v <= 0 ->
                if slotData.CanRequestIfFullyBooked then
                    SlotTypeTakenWithRequestPossible {|
                        ClosingDate = slotData.ClosingDate
                        MaxQuantityPerBooking = slotData.MaxQuantityPerBooking
                    |}
                else SlotTypeTaken
            | remainingCapacity ->
                SlotTypeFree {|
                    ClosingDate = slotData.ClosingDate
                    MaxQuantityPerBooking = slotData.MaxQuantityPerBooking
                    RemainingCapacity = remainingCapacity
                    CanRequestIfFullyBooked = slotData.CanRequestIfFullyBooked
                |}
type Slot = {
    StartTime: DateTime
    Duration: TimeSpan option
    Type: SlotType
}
module Slot =
    let fromSlotData (timeProvider: TimeProvider) slotData =
        { StartTime = slotData.Time; Duration = slotData.Duration; Type = SlotType.fromSlotData timeProvider slotData }

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

type EventData = {
    Key: string
    Title: string
    InfoText: string
    ReservationStartTime: DateTime
    Slots: SlotData[]
    RegistrationConfirmationMail: MailTemplate
    RequestConfirmationMail: MailTemplate option
    RegistrationFields: RegistrationField[]
}
type HiddenEventData = {
    Title: string
    ReservationStartTime: DateTime
}
type ReleasedEventData = {
    Key: string
    Title: string
    InfoText: string
    Slots: Slot[]
    RegistrationConfirmationMail: MailTemplate
    RequestConfirmationMail: MailTemplate option
    RegistrationFields: RegistrationField[]
}
type Event =
    | HiddenEvent of HiddenEventData
    | ReleasedEvent of ReleasedEventData
module Event =
    let tryReleased = function
        | HiddenEvent _ -> None
        | ReleasedEvent event -> Some event

    let fromEventData (timeProvider: TimeProvider) (eventData: EventData) =
        if eventData.ReservationStartTime > timeProvider.GetLocalNow().DateTime then
            HiddenEvent { Title = eventData.Title; ReservationStartTime = eventData.ReservationStartTime }
        else
            ReleasedEvent {
                Key = eventData.Key
                Title = eventData.Title
                InfoText = eventData.InfoText
                Slots =
                    eventData.Slots
                    |> Array.map (Slot.fromSlotData timeProvider)
                    |> Array.sortBy _.StartTime
                RegistrationConfirmationMail = eventData.RegistrationConfirmationMail
                RequestConfirmationMail = eventData.RequestConfirmationMail
                RegistrationFields = eventData.RegistrationFields
            }

type PositiveInteger = private PositiveInteger of int with
    member this.Value = let (PositiveInteger v) = this in v
    static member TryCreate v = if v > 0 then Some (PositiveInteger v) else None

module RegistrationField =
    let private isMailAddress (v: string) =
        try
            Net.Mail.MailAddress v |> ignore
            true
        with _ -> false

    let isValidValue (field: RegistrationField) (value: string) =
        let isEmpty = String.IsNullOrWhiteSpace value
        value.Length <= 100 &&
        (field.Validator <> NotEmpty || not isEmpty) &&
        (field.Type <> MailField || isEmpty || isMailAddress value)

/// values of registration fields, validated against the fields of an event
type SubscriberData = private SubscriberData of Map<string, string> with
    member this.Value = let (SubscriberData v) = this in v
    /// keys of invalid fields as error, values of unknown fields are dropped
    static member TryCreate (fields: RegistrationField[]) (data: Map<string, string>) =
        let values = [
            for field in fields ->
                let value = data |> Map.tryFind field.Key |> Option.defaultValue "" |> _.Trim()
                field, value
        ]
        match values |> List.filter (fun (field, value) -> not (RegistrationField.isValidValue field value)) with
        | [] -> Ok (SubscriberData (values |> List.map (fun (field, value) -> field.Key, value) |> Map.ofList))
        | invalidValues -> Error (invalidValues |> List.map (fst >> _.Key))

type Subscriber = {
    Quantity: PositiveInteger
    Data: SubscriberData
}

type BookingData = {
    EventKey: string
    SlotTime: DateTime
    Subscriber: Subscriber
    Timestamp: DateTime
}

type BookingValidationError =
    | EventNotFound
    | EventNotReleased
    | SlotNotFound
    | SlotUnavailable of Slot
    | InvalidSubscriptionQuantity
    | InvalidRegistrationField of key: string
    | MaxQuantityPerBookingExceeded
type BookingError =
    | CapacityExceeded of remainingCapacity: int

type MailUser = {
    Name: string
    MailAddress: string
}

type BookingConfirmationData = {
    Recipient: MailUser
    Subject: string
    Content: string
}
