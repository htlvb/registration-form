namespace HTLVB.RegistrationForm.Admin.Server.DtoMapping

open HTLVB.RegistrationForm.Admin.Server

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

module Slot =
    let fromDomain (registrations: DataTransfer.EventRegistration list) (slot: Domain.Slot) : DataTransfer.Slot = {
        StartTime = slot.Time
        Duration = slot.Duration
        ClosingDate = slot.ClosingDate
        MaxQuantityPerBooking = slot.MaxQuantityPerBooking
        RemainingCapacity = slot.RemainingCapacity
        CanRequestIfFullyBooked = slot.CanRequestIfFullyBooked
        Registrations = registrations
    }

module MailTemplate =
    let fromDomain (mailTemplate: Domain.MailTemplate) : DataTransfer.MailTemplate = {
        Subject = mailTemplate.Subject
        ContentTemplate = mailTemplate.ContentTemplate
    }

    let toDomain (v: DataTransfer.MailTemplate) : Domain.MailTemplate = {
        Subject = v.Subject
        ContentTemplate = v.ContentTemplate
    }

module Event =
    let fromDomain eventUrl publicRegistrationUrl getRegistrationUrl getSlotRegistrations isAllowedToViewRegistrations event : DataTransfer.Event =
        let eventType, data =
            match event with
            | Domain.DraftEvent v -> "draft", v
            | Domain.ReleasedEvent v -> "released", v
            | Domain.ArchivedEvent v -> "archived", v
        let permissions = Domain.EventPermissions.fromEvent event
        let canViewRegistrations = permissions.CanViewRegistrations && isAllowedToViewRegistrations
        let getSlot (slot: Domain.Slot) =
            let registrations =
                if canViewRegistrations then getSlotRegistrations slot |> List.map (fun v -> EventRegistrations.fromDomain (getRegistrationUrl v) v)
                else []
            Slot.fromDomain registrations slot
        {
            Type = eventType
            Key = data.Key
            Title = data.Title
            InfoText = data.InfoText
            ReservationStartTime = data.ReservationStartTime
            Slots = data.Slots |> Array.map getSlot
            RegistrationConfirmationMail = MailTemplate.fromDomain data.RegistrationConfirmationMail
            RequestConfirmationMail = Option.map MailTemplate.fromDomain data.RequestConfirmationMail
            Url = eventUrl
            PublicRegistrationUrl = publicRegistrationUrl
            CanDelete = permissions.CanDelete
            CanEditEventData = permissions.CanEditEventData
            CanAddSlot = permissions.CanAddSlot
            CanEditSlot = permissions.CanEditSlot
            CanDeleteSlot = permissions.CanDeleteSlot
            CanViewRegistrations = canViewRegistrations

        }

module CreateEventDataSlot =
    let toDomain (v: DataTransfer.CreateEventDataSlot) : Domain.Slot = {
        Time = v.StartTime
        Duration = v.Duration
        ClosingDate = v.ClosingDate
        MaxQuantityPerBooking = v.MaxQuantityPerBooking
        RemainingCapacity = v.RemainingCapacity
        CanRequestIfFullyBooked = v.CanRequestIfFullyBooked
    }

module CreateEventData =
    let toDomain editorIds (v: DataTransfer.CreateEventData) : Domain.EventData = {
        Key = v.Key
        Title = v.Title
        InfoText = v.InfoText
        ReservationStartTime = v.ReservationStartTime
        Slots = v.Slots |> Array.map CreateEventDataSlot.toDomain
        RegistrationConfirmationMail = MailTemplate.toDomain v.RegistrationConfirmationMail
        RequestConfirmationMail = Option.map MailTemplate.toDomain v.RequestConfirmationMail
        EditorIds = editorIds
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
