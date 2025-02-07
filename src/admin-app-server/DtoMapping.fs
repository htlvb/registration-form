namespace HTLVB.RegistrationForm.Admin.Server.DtoMapping

open HTLVB.RegistrationForm.Admin.Server

module Slot =
    let fromDomain slotUrl (slot: Domain.Slot) : DataTransfer.Slot = {
        StartTime = slot.Time
        Duration = slot.Duration
        Url = slotUrl
        ClosingDate = slot.ClosingDate
        MaxQuantityPerBooking = slot.MaxQuantityPerBooking
        RemainingCapacity = slot.RemainingCapacity
    }

module MailTemplate =
    let fromDomain (mailTemplate: Domain.MailTemplate) : DataTransfer.MailTemplate = {
        Subject = mailTemplate.Subject
        ContentTemplate = mailTemplate.ContentTemplate
    }

module Event =
    let fromDomain eventUrl getSlotUrl event : DataTransfer.Event =
        match event with
        | Domain.DraftEvent v ->
            {
                Type = "draft"
                Key = v.Key
                Title = v.Title
                InfoText = v.InfoText
                ReservationStartTime = v.ReservationStartTime
                Slots = v.Slots |> Array.map (fun v -> let slotUrl = getSlotUrl v in Slot.fromDomain slotUrl v)
                RegistrationConfirmationMail = MailTemplate.fromDomain v.RegistrationConfirmationMail
                RequestConfirmationMail = Option.map MailTemplate.fromDomain v.RequestConfirmationMail
                Url = eventUrl
            }
        | Domain.ReleasedEvent v ->
            {
                Type = "released"
                Key = v.Key
                Title = v.Title
                InfoText = v.InfoText
                ReservationStartTime = v.ReservationStartTime
                Slots = v.Slots |> Array.map (fun v -> let slotUrl = getSlotUrl v in Slot.fromDomain slotUrl v)
                RegistrationConfirmationMail = MailTemplate.fromDomain v.RegistrationConfirmationMail
                RequestConfirmationMail = Option.map MailTemplate.fromDomain v.RequestConfirmationMail
                Url = eventUrl
            }

module PatchMailTemplate =
    let toDomain (v: DataTransfer.PatchMailTemplate) : Domain.MailTemplateUpdateData =
        {
            ContentTemplate = v.ContentTemplate
            Subject = v.Subject
        }

module PatchEventDataSlot =
    let toDomain (v: DataTransfer.PatchEventDataSlot) = v.ToDomain()

module PatchEventData =
    let toDomain (v: DataTransfer.PatchEventData) : Domain.EventUpdateData =
        {
            Key = v.Key
            Title = v.Title
            InfoText = v.InfoText
            ReservationStartTime = v.ReservationStartTime
            Slots = v.Slots |> Array.map PatchEventDataSlot.toDomain
            RegistrationConfirmationMail = Option.map PatchMailTemplate.toDomain v.RegistrationConfirmationMail
            RequestConfirmationMail = if v.SetRequestConfirmationMail then Some (Option.map PatchMailTemplate.toDomain v.RequestConfirmationMail) else None
        }