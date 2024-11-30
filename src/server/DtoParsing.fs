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
        and! name = Domain.SubscriberName.TryCreate subscriber.Name |> Result.requireSome Domain.InvalidSubscriberName
        and! mailAddress = Domain.MailAddress.TryCreate subscriber.MailAddress |> Result.requireSome Domain.InvalidMailAddress
        and! phoneNumber = Domain.PhoneNumber.TryCreate subscriber.PhoneNumber |> Result.requireSome Domain.InvalidPhoneNumber
        return { Quantity = quantity; Name = name; MailAddress = mailAddress; PhoneNumber = phoneNumber }
    }

    type ParseSubscriberResult =
        | CanBook of Domain.BookingData * Domain.Slot * Domain.BookingConfirmationData
        | CanRequestBooking of Domain.BookingData * Domain.Slot * Domain.BookingConfirmationData option

    let parse timeProvider eventData slotTimeString subscriber = validation {
        let! event = eventData |> Option.map (Domain.Event.fromEventData timeProvider) |> Result.requireSome Domain.EventNotFound
        let! releasedEvent = event |> Domain.Event.tryReleased |> Result.requireSome Domain.EventNotReleased
        let! slotTime = DateTime.tryParse slotTimeString |> Result.requireSome Domain.SlotNotFound
        let! slot = releasedEvent.Slots |> Seq.tryFind (fun v -> v.StartTime = slotTime) |> Result.requireSome Domain.SlotNotFound
        match slot.Type with
        | Domain.SlotTypeFree _ ->
            let! subscriber = parseDto subscriber
            do! Domain.SlotType.canBookQuantity subscriber.Quantity.Value slot.Type |> Result.requireTrue Domain.MaxQuantityPerBookingExceeded
            let bookingData: Domain.BookingData = {
                EventKey = releasedEvent.Key
                SlotTime = slot.StartTime
                Subscriber = subscriber
                Timestamp = timeProvider.GetLocalNow().DateTime
            }
            let confirmationData: Domain.BookingConfirmationData = {
                Recipient = {
                    Name = subscriber.Name.Value
                    MailAddress = subscriber.MailAddress.Value
                }
                Subject = releasedEvent.RegistrationConfirmationMail.Subject
                Content =
                    let templateVars = [
                        "FullName", subscriber.Name.Value
                        "Date", slot.StartTime.ToString("d", CultureInfo.GetCultureInfo("de-AT"))
                        "Time", slot.StartTime.ToString("t", CultureInfo.GetCultureInfo("de-AT"))
                    ]
                    (releasedEvent.RegistrationConfirmationMail.ContentTemplate, templateVars)
                    ||> List.fold (fun text (varName, value) -> text.Replace(sprintf "{{{%s}}}" varName, value))
            }
            return CanBook (bookingData, slot, confirmationData)
        | Domain.SlotTypeTakenWithRequestPossible _ ->
            let! subscriber = parseDto subscriber
            do! Domain.SlotType.canBookQuantity subscriber.Quantity.Value slot.Type |> Result.requireTrue Domain.MaxQuantityPerBookingExceeded
            let bookingData: Domain.BookingData = {
                EventKey = releasedEvent.Key
                SlotTime = slot.StartTime
                Subscriber = subscriber
                Timestamp = timeProvider.GetLocalNow().DateTime
            }
            let requestConfirmationData: Domain.BookingConfirmationData option =
                match releasedEvent.RequestConfirmationMail with
                | Some mailTemplate ->
                    Some {
                        Recipient = {
                            Name = subscriber.Name.Value
                            MailAddress = subscriber.MailAddress.Value
                        }
                        Subject = mailTemplate.Subject
                        Content =
                            let templateVars = [
                                "FullName", subscriber.Name.Value
                                "Date", slot.StartTime.ToString("d", CultureInfo.GetCultureInfo("de-AT"))
                                "Time", slot.StartTime.ToString("t", CultureInfo.GetCultureInfo("de-AT"))
                            ]
                            (mailTemplate.ContentTemplate, templateVars)
                            ||> List.fold (fun text (varName, value) -> text.Replace(sprintf "{{{%s}}}" varName, value))
                    }
                | None -> None 
            return CanRequestBooking (bookingData, slot, requestConfirmationData)
        | _ -> return! Validation.error (Domain.SlotUnavailable slot)
    }
