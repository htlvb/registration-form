namespace HTLVB.RegistrationForm.Server.DataTransfer

open System
open System.Text.Json.Serialization

[<AbstractClass>]
[<JsonPolymorphic(TypeDiscriminatorPropertyName = "type")>]
[<JsonDerivedType(typeof<ReservationTypeFree>, "free")>]
[<JsonDerivedType(typeof<ReservationTypeTaken>, "taken")>]
type ReservationType() = class end
and ReservationTypeFree(url: string, maxQuantityPerBooking: Nullable<int>, remainingCapacity: Nullable<int>) =
    inherit ReservationType()
    member _.Url = url
    member _.MaxQuantityPerBooking = maxQuantityPerBooking
    member _.RemainingCapacity = remainingCapacity
and ReservationTypeTaken() =
    inherit ReservationType()

type ScheduleEntry = {
    StartTime: DateTime
    ReservationType: ReservationType
}

[<AbstractClass>]
[<JsonPolymorphic(TypeDiscriminatorPropertyName = "type")>]
[<JsonDerivedType(typeof<HiddenSchedule>, "hidden")>]
[<JsonDerivedType(typeof<ReleasedSchedule>, "released")>]
type Schedule() = class end
and HiddenSchedule(title: string, reservationStartTime: DateTime) =
    inherit Schedule()
    member _.Title = title
    member _.ReservationStartTime = reservationStartTime
and ReleasedSchedule(title: string, infoText: string, entries: ScheduleEntry list) =
    inherit Schedule()
    member _.Title = title
    member _.InfoText = infoText
    member _.Entries = entries

type Subscriber = {
    Quantity: int
    Name: string
    MailAddress: string
    PhoneNumber: string
}

type BookingResult = {
    ReservationType: ReservationType
    MailSendError: bool
}