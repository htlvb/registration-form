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
}
type DraftEventData = {
    Key: string
    Title: string
    InfoText: string
    ReservationStartTime: DateTime
    Slots: Slot[]
    RegistrationConfirmationMail: MailTemplate
    RequestConfirmationMail: MailTemplate option
}
type ReleasedEventData = {
    Key: string
    Title: string
    InfoText: string
    ReservationStartTime: DateTime
    Slots: Slot[]
    RegistrationConfirmationMail: MailTemplate
    RequestConfirmationMail: MailTemplate option
}
type Event =
    | DraftEvent of DraftEventData
    | ReleasedEvent of ReleasedEventData
module Event =
    let tryReleased = function
        | DraftEvent _ -> None
        | ReleasedEvent event -> Some event

    let fromEventData (timeProvider: TimeProvider) (eventData: EventData) =
        if eventData.ReservationStartTime > timeProvider.GetLocalNow().DateTime then
            DraftEvent {
                Key = eventData.Key
                Title = eventData.Title
                InfoText = eventData.InfoText
                ReservationStartTime = eventData.ReservationStartTime
                Slots = eventData.Slots |> Array.sortBy _.Time
                RegistrationConfirmationMail = eventData.RegistrationConfirmationMail
                RequestConfirmationMail = eventData.RequestConfirmationMail
            }
        else
            ReleasedEvent {
                Key = eventData.Key
                Title = eventData.Title
                InfoText = eventData.InfoText
                ReservationStartTime = eventData.ReservationStartTime
                Slots = eventData.Slots |> Array.sortBy _.Time
                RegistrationConfirmationMail = eventData.RegistrationConfirmationMail
                RequestConfirmationMail = eventData.RequestConfirmationMail
            }

type MailTemplateUpdateData = {
    Subject: string
    ContentTemplate: string
}

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
    RegistrationConfirmationMail: MailTemplateUpdateData option
    RequestConfirmationMail: MailTemplateUpdateData option option
}

type EventRegistration = {
    Id: string
    Quantity: int
    Name: string
    MailAddress: string
    PhoneNumber: string
    Timestamp: DateTime
    IsRequest: bool
    DeregistrationTime: DateTime option
}
