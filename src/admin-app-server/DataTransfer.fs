namespace HTLVB.RegistrationForm.Admin.Server.DataTransfer

open HTLVB.RegistrationForm.Admin.Server
open System
open System.Text.Json.Serialization

type Slot = {
    StartTime: DateTime
    Duration: TimeSpan option
    ClosingDate: DateTime option
    MaxQuantityPerBooking: int option
    RemainingCapacity: int option
    Url: string
}

type MailTemplate = {
    Subject: string
    ContentTemplate: string
}
type Event = {
    Type: string
    Key: string
    Title: string
    InfoText: string
    ReservationStartTime: DateTime
    Slots: Slot[]
    RegistrationConfirmationMail: MailTemplate
    RequestConfirmationMail: MailTemplate option
    Url: string
}

[<AbstractClass>]
[<JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")>]
[<JsonDerivedType(typeof<PatchEventDataCreateSlot>, typeDiscriminator = "create")>]
[<JsonDerivedType(typeof<PatchEventDataUpdateSlot>, typeDiscriminator = "update")>]
[<JsonDerivedType(typeof<PatchEventDataDeleteSlot>, typeDiscriminator = "delete")>]
type PatchEventDataSlot(startTime: DateTime) =
    member _.StartTime = startTime
    abstract member ToDomain : unit -> Domain.SlotUpdate

and PatchEventDataCreateSlot(
    startTime: DateTime,
    duration: TimeSpan option,
    closingDate: DateTime option,
    maxQuantityPerBooking: int option,
    remainingCapacity: int option,
    canRequestIfFullyBooked: bool
) =
    inherit PatchEventDataSlot(startTime)
    member _.Duration = duration
    member _.ClosingDate = closingDate
    member _.MaxQuantityPerBooking = maxQuantityPerBooking
    member _.RemainingCapacity = remainingCapacity
    member _.CanRequestIfFullyBooked = canRequestIfFullyBooked
    override this.ToDomain(): Domain.SlotUpdate =
        Domain.CreateSlot {
            Time = this.StartTime
            Duration = this.Duration
            ClosingDate = this.ClosingDate
            MaxQuantityPerBooking = this.MaxQuantityPerBooking
            RemainingCapacity = this.RemainingCapacity
            CanRequestIfFullyBooked = this.CanRequestIfFullyBooked
        }

and PatchEventDataUpdateSlot(
    startTime: DateTime,
    newTime: DateTime option,
    duration: TimeSpan option,
    setDuration: bool,
    closingDate: DateTime option,
    setClosingDate: bool,
    maxQuantityPerBooking: int option,
    setMaxQuantityPerBooking: bool,
    remainingCapacity: int option,
    setRemainingCapacity: bool,
    canRequestIfFullyBooked: bool option
) =
    inherit PatchEventDataSlot(startTime)
    member _.NewTime = newTime
    member _.Duration = duration
    member _.SetDuration = setDuration
    member _.ClosingDate = closingDate
    member _.SetClosingDate = setClosingDate
    member _.MaxQuantityPerBooking = maxQuantityPerBooking
    member _.SetMaxQuantityPerBooking = setMaxQuantityPerBooking
    member _.RemainingCapacity = remainingCapacity
    member _.SetRemainingCapacity = setRemainingCapacity
    member _.CanRequestIfFullyBooked = canRequestIfFullyBooked
    override this.ToDomain() =
        Domain.UpdateSlot (startTime, {
            Time = this.NewTime
            Duration = if this.SetDuration then Some this.Duration else None
            ClosingDate = if this.SetClosingDate then Some this.ClosingDate else None
            MaxQuantityPerBooking = if this.SetMaxQuantityPerBooking then Some this.MaxQuantityPerBooking else None
            RemainingCapacity = if this.SetRemainingCapacity then Some this.RemainingCapacity else None
            CanRequestIfFullyBooked = this.CanRequestIfFullyBooked
        })

and PatchEventDataDeleteSlot(startTime: DateTime) =
    inherit PatchEventDataSlot(startTime)
    override _.ToDomain() = Domain.DeleteSlot startTime

type PatchMailTemplate = {
    Subject: string
    ContentTemplate: string
}
type PatchEventData = {
    Key: string option
    Title: string option
    InfoText: string option
    ReservationStartTime: DateTime option
    Slots: PatchEventDataSlot[]
    RegistrationConfirmationMail: PatchMailTemplate option
    RequestConfirmationMail: PatchMailTemplate option
    SetRequestConfirmationMail: bool
}
type PatchSlotData = {
    StartTime: DateTime
    Duration: TimeSpan option
    ClosingDate: DateTime option
    MaxQuantityPerBooking: int option
    RemainingCapacity: int option
}
