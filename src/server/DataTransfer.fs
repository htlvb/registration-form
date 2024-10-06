namespace HTLVB.RegistrationForm.Server.DataTransfer

open System
open System.Text.Json.Serialization

[<AbstractClass>]
[<JsonPolymorphic(TypeDiscriminatorPropertyName = "type")>]
[<JsonDerivedType(typeof<SlotTypeFree>, "free")>]
[<JsonDerivedType(typeof<SlotTypeClosed>, "closed")>]
[<JsonDerivedType(typeof<SlotTypeTaken>, "taken")>]
type SlotType() = class end
and SlotTypeFree(url: string, closingDate: Nullable<DateTime>, maxQuantityPerBooking: Nullable<int>, remainingCapacity: Nullable<int>) =
    inherit SlotType()
    member _.Url = url
    member _.ClosingDate = closingDate
    member _.MaxQuantityPerBooking = maxQuantityPerBooking
    member _.RemainingCapacity = remainingCapacity
and SlotTypeClosed() =
    inherit SlotType()
and SlotTypeTaken() =
    inherit SlotType()

type Slot = {
    StartTime: DateTime
    Type: SlotType
}

[<AbstractClass>]
[<JsonPolymorphic(TypeDiscriminatorPropertyName = "type")>]
[<JsonDerivedType(typeof<HiddenEvent>, "hidden")>]
[<JsonDerivedType(typeof<ReleasedEvent>, "released")>]
type Event() = class end
and HiddenEvent(title: string, reservationStartTime: DateTime) =
    inherit Event()
    member _.Title = title
    member _.ReservationStartTime = reservationStartTime
and ReleasedEvent(title: string, infoText: string, slots: Slot list) =
    inherit Event()
    member _.Title = title
    member _.InfoText = infoText
    member _.Slots = slots

type Subscriber = {
    Quantity: int
    Name: string
    MailAddress: string
    PhoneNumber: string
}

type BookingResult = {
    SlotType: SlotType
    MailSendError: bool
}