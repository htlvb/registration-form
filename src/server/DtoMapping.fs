namespace HTLVB.RegistrationForm.Server.DtoMapping

open HTLVB.RegistrationForm.Server

module SlotType =
    let fromDomain slotUrl = function
        | Domain.SlotTypeFree v ->
            DataTransfer.SlotTypeFree(
                slotUrl,
                Option.toNullable v.ClosingDate,
                Option.toNullable v.MaxQuantityPerBooking,
                Option.toNullable v.RemainingCapacity
            ) :> DataTransfer.SlotType
        | Domain.SlotTypeTaken -> DataTransfer.SlotTypeTaken()
        | Domain.SlotTypeClosed -> DataTransfer.SlotTypeClosed()

module Slot =
    let fromDomain slotUrl (slot: Domain.Slot) : DataTransfer.Slot = {
        StartTime = slot.StartTime
        Duration = Option.toNullable slot.Duration
        Type = SlotType.fromDomain slotUrl slot.Type
    }

module Event =
    let fromDomain getSlotUrl = function
        | Domain.HiddenEvent v -> DataTransfer.HiddenEvent(v.Title, v.ReservationStartTime) :> DataTransfer.Event
        | Domain.ReleasedEvent v -> DataTransfer.ReleasedEvent(v.Title, v.InfoText, [| for slot in v.Slots -> Slot.fromDomain (getSlotUrl slot) slot |]) :> DataTransfer.Event

module BookingValidationError =
    let fromDomain getSlotUrl = function
        | Domain.EventNotFound -> {| Error = "event-not-found" |} :> obj
        | Domain.EventNotReleased -> {| Error = "event-not-released" |}
        | Domain.SlotNotFound -> {| Error = "slot-not-found" |}
        | Domain.SlotNotFree slot -> {| Error = "slot-not-free"; SlotType = Slot.fromDomain (getSlotUrl slot) slot |> _.Type |}
        | Domain.InvalidSubscriptionQuantity -> {| Error = "invalid-subscription-quantity" |}
        | Domain.InvalidSubscriberName -> {| Error = "invalid-subscriber-name" |}
        | Domain.InvalidMailAddress -> {| Error = "invalid-mail-address" |}
        | Domain.InvalidPhoneNumber -> {| Error = "invalid-phone-number" |}
        | Domain.MaxQuantityPerBookingExceeded -> {| Error = "max-quantity-per-booking-exceeded" |}
