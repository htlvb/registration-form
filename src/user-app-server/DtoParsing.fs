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
    let parseDto (fields: Domain.RegistrationField[]) (subscriber: DataTransfer.Subscriber) : Validation<Domain.Subscriber, _> = validation {
        let! quantity = Domain.PositiveInteger.TryCreate subscriber.Quantity |> Result.requireSome Domain.InvalidSubscriptionQuantity
        and! data =
            Domain.SubscriberData.TryCreate fields (subscriber.Data |> Option.ofObj |> Option.defaultValue Map.empty)
            |> Result.mapError (List.map Domain.InvalidRegistrationField)
        return { Quantity = quantity; Data = data }
    }

    type ParseSubscriberResult =
        | CanBook of Domain.BookingData * Domain.Slot * Domain.BookingConfirmationData option
        | CanRequestBooking of Domain.BookingData * Domain.Slot * Domain.BookingConfirmationData option

    /// confirmation is sent to the first mail field, `None` if it's empty
    let private getConfirmationData (fields: Domain.RegistrationField[]) (subscriber: Domain.Subscriber) (slot: Domain.Slot) (mailTemplate: Domain.MailTemplate) : Domain.BookingConfirmationData option =
        let data = subscriber.Data.Value
        fields
        |> Array.tryFind (fun v -> v.Type = Domain.MailField)
        |> Option.bind (fun v -> data |> Map.tryFind v.Key)
        |> Option.filter (String.IsNullOrWhiteSpace >> not)
        |> Option.map (fun mailAddress ->
            let templateVars = [
                yield! data |> Map.toList
                "Date", slot.StartTime.ToString("d", CultureInfo.GetCultureInfo("de-AT"))
                "Time", slot.StartTime.ToString("t", CultureInfo.GetCultureInfo("de-AT"))
            ]
            {
                Recipient = { Name = ""; MailAddress = mailAddress }
                Subject = mailTemplate.Subject
                Content =
                    (mailTemplate.ContentTemplate, templateVars)
                    ||> List.fold (fun text (varName, value) -> text.Replace(sprintf "{{{%s}}}" varName, value))
            }
        )

    let parse timeProvider eventData slotTimeString subscriber = validation {
        let! event = eventData |> Option.map (Domain.Event.fromEventData timeProvider) |> Result.requireSome Domain.EventNotFound
        let! releasedEvent = event |> Domain.Event.tryReleased |> Result.requireSome Domain.EventNotReleased
        let! slotTime = DateTime.tryParse slotTimeString |> Result.requireSome Domain.SlotNotFound
        let! slot = releasedEvent.Slots |> Seq.tryFind (fun v -> v.StartTime = slotTime) |> Result.requireSome Domain.SlotNotFound
        match slot.Type with
        | Domain.SlotTypeFree _ ->
            let! subscriber = parseDto releasedEvent.RegistrationFields subscriber
            do! Domain.SlotType.canBookQuantity subscriber.Quantity.Value slot.Type |> Result.requireTrue Domain.MaxQuantityPerBookingExceeded
            let bookingData: Domain.BookingData = {
                EventKey = releasedEvent.Key
                SlotTime = slot.StartTime
                Subscriber = subscriber
                Timestamp = timeProvider.GetLocalNow().DateTime
            }
            let confirmationData = getConfirmationData releasedEvent.RegistrationFields subscriber slot releasedEvent.RegistrationConfirmationMail
            return CanBook (bookingData, slot, confirmationData)
        | Domain.SlotTypeTakenWithRequestPossible _ ->
            let! subscriber = parseDto releasedEvent.RegistrationFields subscriber
            do! Domain.SlotType.canBookQuantity subscriber.Quantity.Value slot.Type |> Result.requireTrue Domain.MaxQuantityPerBookingExceeded
            let bookingData: Domain.BookingData = {
                EventKey = releasedEvent.Key
                SlotTime = slot.StartTime
                Subscriber = subscriber
                Timestamp = timeProvider.GetLocalNow().DateTime
            }
            let requestConfirmationData =
                releasedEvent.RequestConfirmationMail
                |> Option.bind (getConfirmationData releasedEvent.RegistrationFields subscriber slot)
            return CanRequestBooking (bookingData, slot, requestConfirmationData)
        | _ -> return! Validation.error (Domain.SlotUnavailable slot)
    }
