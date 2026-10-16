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
        | Domain.SlotTypeTakenWithRequestPossible v ->
            DataTransfer.SlotTypeTakenWithRequestPossible(
                slotUrl,
                Option.toNullable v.ClosingDate,
                Option.toNullable v.MaxQuantityPerBooking
            )
        | Domain.SlotTypeTaken -> DataTransfer.SlotTypeTaken()
        | Domain.SlotTypeClosed -> DataTransfer.SlotTypeClosed()

module Slot =
    let fromDomain slotUrl (slot: Domain.Slot) : DataTransfer.Slot = {
        StartTime = slot.StartTime
        Duration = Option.toNullable slot.Duration
        Type = SlotType.fromDomain slotUrl slot.Type
    }

module RegistrationField =
    let fromDomain (v: Domain.RegistrationField) : DataTransfer.RegistrationField = {
        Key = v.Key
        Label = v.Label
        Type =
            match v.Type with
            | Domain.TextField -> "text"
            | Domain.MailField -> "mail"
            | Domain.PhoneField -> "phone"
        Validator =
            match v.Validator with
            | Domain.NoValidation -> "none"
            | Domain.NotEmpty -> "notEmpty"
    }

module Event =
    let fromDomain getSlotUrl = function
        | Domain.HiddenEvent v -> DataTransfer.HiddenEvent(v.Title, v.ReservationStartTime) :> DataTransfer.Event
        | Domain.ReleasedEvent v ->
            DataTransfer.ReleasedEvent(
                v.Title,
                v.InfoText,
                [| for slot in v.Slots -> Slot.fromDomain (getSlotUrl slot) slot |],
                v.RegistrationFields |> Array.map RegistrationField.fromDomain
            ) :> DataTransfer.Event

module BookingValidationError =
    let fromDomain getSlotUrl = function
        | Domain.EventNotFound -> {| Error = "event-not-found" |} :> obj
        | Domain.EventNotReleased -> {| Error = "event-not-released" |}
        | Domain.SlotNotFound -> {| Error = "slot-not-found" |}
        | Domain.SlotUnavailable slot -> {| Error = "slot-unavailable"; SlotType = Slot.fromDomain (getSlotUrl slot) slot |> _.Type |}
        | Domain.InvalidSubscriptionQuantity -> {| Error = "invalid-subscription-quantity" |}
        | Domain.InvalidRegistrationField key -> {| Error = "invalid-registration-field"; Key = key |}
        | Domain.MaxQuantityPerBookingExceeded -> {| Error = "max-quantity-per-booking-exceeded" |}
