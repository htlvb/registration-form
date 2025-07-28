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
        let eventType, data, permissions = 
            match event with
            | Domain.DraftEvent v -> "draft", v, {| CanDelete = true; CanEditEventData = true; CanAddSlot = true; CanEditSlot = true; CanDeleteSlot = true; CanViewRegistrations = false |}
            | Domain.ReleasedEvent v -> "released", v, {| CanDelete = false; CanEditEventData = true; CanAddSlot = true; CanEditSlot = false; CanDeleteSlot = false; CanViewRegistrations = true |}
            | Domain.ArchivedEvent v -> "archived", v, {| CanDelete = false; CanEditEventData = false; CanAddSlot = false; CanEditSlot = false; CanDeleteSlot = false; CanViewRegistrations = true |}
        {
            Type = eventType
            Key = data.Key
            Title = data.Title
            InfoText = data.InfoText
            ReservationStartTime = data.ReservationStartTime
            Slots = data.Slots |> Array.map (fun v -> let slotRegistrationsUrl = getSlotRegistrationsUrl v in Slot.fromDomain slotRegistrationsUrl v)
            RegistrationConfirmationMail = MailTemplate.fromDomain data.RegistrationConfirmationMail
            RequestConfirmationMail = Option.map MailTemplate.fromDomain data.RequestConfirmationMail
            Url = eventUrl
            CanDelete = permissions.CanDelete
            CanEditEventData = permissions.CanEditEventData
            CanAddSlot = permissions.CanAddSlot
            CanEditSlot = permissions.CanEditSlot
            CanDeleteSlot = permissions.CanDeleteSlot
            CanViewRegistrations = permissions.CanViewRegistrations

        }

module PatchMailTemplate =
    let toDomain (v: DataTransfer.PatchMailTemplate) : Domain.MailTemplate =
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
