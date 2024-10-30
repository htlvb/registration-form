namespace HTLVB.RegistrationForm.Server.Domain

open System

type SlotData = {
    Time: DateTime
    Duration: TimeSpan option
    ClosingDate: DateTime option
    MaxQuantityPerBooking: int option
    RemainingCapacity: int option
}
type SlotType =
    | SlotTypeFree of {| ClosingDate: DateTime option; MaxQuantityPerBooking: int option; RemainingCapacity: int option |}
    | SlotTypeTaken
    | SlotTypeClosed
module SlotType =
    let tryFree = function
        | SlotTypeFree slot -> Some slot
        | SlotTypeTaken -> None
        | SlotTypeClosed -> None

    let setCapacity slotType remainingCapacity =
        match slotType, remainingCapacity with
        | SlotTypeFree data, None -> SlotTypeFree {| data with RemainingCapacity = None |}
        | SlotTypeFree data, Some remainingCapacity when remainingCapacity > 0 -> SlotTypeFree {| data with RemainingCapacity = Some remainingCapacity |}
        | SlotTypeFree _, _ -> SlotTypeTaken
        | SlotTypeTaken, _ -> SlotTypeTaken
        | SlotTypeClosed, _ -> SlotTypeClosed

    let fromSlotData (timeProvider: TimeProvider) slotData =
        match slotData.ClosingDate with
        | Some closingDate when timeProvider.GetLocalNow().DateTime >= closingDate -> SlotTypeClosed
        // TODO no closing date, but now > start time
        | _ ->
            match slotData.RemainingCapacity with
            | Some v when v <= 0 -> SlotTypeTaken
            | remainingCapacity ->
                SlotTypeFree {|
                    ClosingDate = slotData.ClosingDate
                    MaxQuantityPerBooking = slotData.MaxQuantityPerBooking
                    RemainingCapacity = remainingCapacity
                |}
type Slot = {
    StartTime: DateTime
    Duration: TimeSpan option
    Type: SlotType
}
module Slot =
    let fromSlotData (timeProvider: TimeProvider) slotData =
        { StartTime = slotData.Time; Duration = slotData.Duration; Type = SlotType.fromSlotData timeProvider slotData }

type EventData = {
    Key: string
    Title: string
    InfoText: string
    ReservationStartTime: DateTime
    Slots: SlotData[]
    MailSubject: string
    MailContentTemplate: string
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
    MailSubject: string
    MailContentTemplate: string
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
                Slots = [| for slot in eventData.Slots -> Slot.fromSlotData timeProvider slot |]
                MailSubject = eventData.MailSubject
                MailContentTemplate = eventData.MailContentTemplate
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

type BookingValidationError =
    | EventNotFound
    | EventNotReleased
    | SlotNotFound
    | SlotNotFree of Slot
    | InvalidSubscriptionQuantity
    | InvalidSubscriberName
    | InvalidMailAddress
    | InvalidPhoneNumber
    | MaxQuantityPerBookingExceeded
type BookingError =
    | CapacityExceeded of remainingCapacity: int
