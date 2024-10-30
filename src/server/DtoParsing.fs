namespace HTLVB.RegistrationForm.Server.DtoParsing

open FsToolkit.ErrorHandling
open HTLVB.RegistrationForm.Server
open System
open System.Globalization

module DateTime =
    let tryParse (text: string) =
        match DateTime.TryParseExact(text, "yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.None) with
        | (true, timestamp) -> Some timestamp
        | (false, _) -> None

module Subscriber =
    let parseDto (subscriber: DataTransfer.Subscriber) : Result<Domain.Subscriber, _> = validation {
        let! quantity = Domain.PositiveInteger.TryCreate subscriber.Quantity |> Result.requireSome Domain.InvalidSubscriptionQuantity
        let! name = Domain.SubscriberName.TryCreate subscriber.Name |> Result.requireSome Domain.InvalidSubscriberName
        let! mailAddress = Domain.MailAddress.TryCreate subscriber.MailAddress |> Result.requireSome Domain.InvalidMailAddress
        let! phoneNumber = Domain.PhoneNumber.TryCreate subscriber.PhoneNumber |> Result.requireSome Domain.InvalidPhoneNumber
        return { Quantity = quantity; Name = name; MailAddress = mailAddress; PhoneNumber = phoneNumber }
    }

    let parse timeProvider eventData slotTimeString subscriber = validation {
        let! event = eventData |> Option.map (Domain.Event.fromEventData timeProvider) |> Result.requireSome Domain.EventNotFound
        let! releasedEvent = event |> Domain.Event.tryReleased |> Result.requireSome Domain.EventNotReleased
        let! slotTime = DateTime.tryParse slotTimeString |> Result.requireSome Domain.SlotNotFound
        let! slot = releasedEvent.Slots |> Seq.tryFind (fun v -> v.StartTime = slotTime) |> Result.requireSome Domain.SlotNotFound
        let! freeSlot = slot.Type |> Domain.SlotType.tryFree |> Result.requireSome Domain.SlotNotFree
        let! subscriber = parseDto subscriber
        match freeSlot.MaxQuantityPerBooking with
        | Some maxQuantityPerBooking ->
            do! (subscriber.Quantity.Value <= maxQuantityPerBooking) |> Result.requireTrue Domain.MaxQuantityPerBookingExceeded
        | None -> ()
        return (releasedEvent, slot, subscriber)
    }
