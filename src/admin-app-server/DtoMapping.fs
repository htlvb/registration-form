namespace HTLVB.RegistrationForm.Admin.Server.DtoMapping

open HTLVB.RegistrationForm.Admin.Server

module Slot =
    let fromDomain slotRegistrationsUrl (slot: Domain.Slot) : DataTransfer.Slot = {
        StartTime = slot.Time
        Duration = slot.Duration
        ClosingDate = slot.ClosingDate
        MaxQuantityPerBooking = slot.MaxQuantityPerBooking
        RemainingCapacity = slot.RemainingCapacity
        RegistrationsUrl = slotRegistrationsUrl
    }

module MailTemplate =
    let fromDomain (mailTemplate: Domain.MailTemplate) : DataTransfer.MailTemplate = {
        Subject = mailTemplate.Subject
        ContentTemplate = mailTemplate.ContentTemplate
    }

module Event =
    let fromDomain eventUrl getSlotRegistrationsUrl event : DataTransfer.Event =
        match event with
        | Domain.DraftEvent v ->
            {
                Type = "draft"
                Key = v.Key
                Title = v.Title
                InfoText = v.InfoText
                ReservationStartTime = v.ReservationStartTime
                Slots = v.Slots |> Array.map (fun v -> let slotRegistrationsUrl = getSlotRegistrationsUrl v in Slot.fromDomain slotRegistrationsUrl v)
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
                Slots = v.Slots |> Array.map (fun v -> let slotRegistrationsUrl = getSlotRegistrationsUrl v in Slot.fromDomain slotRegistrationsUrl v)
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

module EventRegistrations =
    let fromDomain registrationUrl (registration: Domain.EventRegistration) : DataTransfer.EventRegistration =
        {
            Quantity = registration.Quantity
            Name = registration.Name
            MailAddress = registration.MailAddress
            PhoneNumber = registration.PhoneNumber
            Timestamp = registration.Timestamp
            IsRequest = registration.IsRequest
            DeregistrationTime = registration.DeregistrationTime
            Url = registrationUrl
        }
