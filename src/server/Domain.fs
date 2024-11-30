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
        match slotData.ClosingDate with
        | Some closingDate when timeProvider.GetLocalNow().DateTime >= closingDate -> SlotTypeClosed
        // TODO no closing date, but now > start time
        | _ ->
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
type EventData = {
    Key: string
    Title: string
    InfoText: string
    ReservationStartTime: DateTime
    Slots: SlotData[]
    RegistrationConfirmationMail: MailTemplate
    RequestConfirmationMail: MailTemplate option
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
            }

type PositiveInteger = private PositiveInteger of int with
    member this.Value = let (PositiveInteger v) = this in v
    static member TryCreate v = if v > 0 then Some (PositiveInteger v) else None

type SubscriberName = private SubscriberName of string with
    member this.Value = let (SubscriberName v) = this in v
    static member TryCreate v =
        if not <| String.IsNullOrWhiteSpace v && v.Length < 100 then
            Some (SubscriberName v)
        else None

type MailAddress = private MailAddress of string with
    member this.Value = let (MailAddress v) = this in v
    static member TryCreate v =
        let canParse =
            try
                Net.Mail.MailAddress v |> ignore
                true
            with _ -> false
        if canParse && v.Length <= 100 then Some (MailAddress v)
        else None

type PhoneNumber = private PhoneNumber of string with
    member this.Value = let (PhoneNumber v) = this in v
    static member TryCreate v =
        if not <| String.IsNullOrWhiteSpace v && v.Length < 100 then
            Some (PhoneNumber v)
        else None

type Subscriber = {
    Quantity: PositiveInteger
    Name: SubscriberName
    MailAddress: MailAddress
    PhoneNumber: PhoneNumber
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
    | InvalidSubscriberName
    | InvalidMailAddress
    | InvalidPhoneNumber
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
