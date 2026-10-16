namespace HTLVB.RegistrationForm.Admin.Server.DtoMapping

open HTLVB.RegistrationForm.Admin.Server

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

    let tryToDomain (v: DataTransfer.RegistrationField) : Domain.RegistrationField option =
        let fieldType =
            match v.Type with
            | "text" -> Some Domain.TextField
            | "mail" -> Some Domain.MailField
            | "phone" -> Some Domain.PhoneField
            | _ -> None
        let validator =
            match v.Validator with
            | "none" -> Some Domain.NoValidation
            | "notEmpty" -> Some Domain.NotEmpty
            | _ -> None
        match fieldType, validator with
        | Some fieldType, Some validator -> Some { Key = v.Key; Label = v.Label; Type = fieldType; Validator = validator }
        | _ -> None

    /// `None` if a field can't be parsed
    let tryToDomainList (fields: DataTransfer.RegistrationField[]) =
        let parsedFields = fields |> Array.choose tryToDomain
        if parsedFields.Length = fields.Length then Some parsedFields else None

module EventRegistrations =
    let fromDomain registrationUrl (registration: Domain.EventRegistration) : DataTransfer.EventRegistration =
        {
            Quantity = registration.Quantity
            Data = registration.Data
            Timestamp = registration.Timestamp
            IsRequest = registration.IsRequest
            DeregistrationTime = registration.DeregistrationTime
            Url = registrationUrl
        }

module Slot =
    let fromDomain (registrations: DataTransfer.EventRegistration list) (editPermission: Domain.SlotEditPermission) (slot: Domain.Slot) : DataTransfer.Slot = {
        StartTime = slot.Time
        Duration = slot.Duration
        ClosingDate = slot.ClosingDate
        MaxQuantityPerBooking = slot.MaxQuantityPerBooking
        RemainingCapacity = slot.RemainingCapacity
        CanRequestIfFullyBooked = slot.CanRequestIfFullyBooked
        Registrations = registrations
        CanEdit =
            match editPermission with
            | Domain.SlotOpen -> "open"
            | Domain.SlotTimesLocked -> "timesLocked"
            | Domain.SlotLocked -> "locked"
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
    let fromDomain now eventUrl publicRegistrationUrl getRegistrationUrl getSlotRegistrations isAllowedToViewRegistrations isAllowedToUpdate event : DataTransfer.Event =
        let eventType, data =
            match event with
            | Domain.DraftEvent v -> "draft", v
            | Domain.ReleasedEvent v -> "released", v
            | Domain.ArchivedEvent v -> "archived", v
        let permissions =
            Domain.EventPermissions.fromEvent event
            |> if isAllowedToUpdate then id else Domain.EventPermissions.withoutUpdates
        let canViewRegistrations = permissions.CanViewRegistrations && isAllowedToViewRegistrations
        let getSlot (slot: Domain.Slot) =
            let registrations =
                if canViewRegistrations then getSlotRegistrations slot |> List.map (fun v -> EventRegistrations.fromDomain (getRegistrationUrl v) v)
                else []
            let editPermission = if isAllowedToUpdate then Domain.SlotEditPermission.fromSlot now event slot else Domain.SlotLocked
            Slot.fromDomain registrations editPermission slot
        {
            Type = eventType
            Key = data.Key
            Title = data.Title
            InfoText = data.InfoText
            ReservationStartTime = data.ReservationStartTime
            Slots = data.Slots |> Array.map getSlot
            RegistrationConfirmationMail = MailTemplate.fromDomain data.RegistrationConfirmationMail
            RequestConfirmationMail = Option.map MailTemplate.fromDomain data.RequestConfirmationMail
            RegistrationFields = data.RegistrationFields |> Array.map RegistrationField.fromDomain
            Url = eventUrl
            PublicRegistrationUrl = publicRegistrationUrl
            CanDelete = permissions.CanDelete
            CanEditEventData = permissions.CanEditEventData
            CanAddSlot = permissions.CanAddSlot
            CanDeleteSlot = permissions.CanDeleteSlot
            CanEditRegistrationFields = permissions.CanEditRegistrationFields
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
    let tryToDomain editorIds (v: DataTransfer.CreateEventData) : Domain.EventData option =
        RegistrationField.tryToDomainList v.RegistrationFields
        |> Option.map (fun registrationFields -> {
            Key = v.Key
            Title = v.Title
            InfoText = v.InfoText
            ReservationStartTime = v.ReservationStartTime
            Slots = v.Slots |> Array.map CreateEventDataSlot.toDomain
            RegistrationConfirmationMail = MailTemplate.toDomain v.RegistrationConfirmationMail
            RequestConfirmationMail = Option.map MailTemplate.toDomain v.RequestConfirmationMail
            RegistrationFields = registrationFields
            EditorIds = editorIds
        })

module PatchMailTemplate =
    let toDomain (v: DataTransfer.PatchMailTemplate) : Domain.MailTemplate =
        {
            ContentTemplate = v.ContentTemplate
            Subject = v.Subject
        }

module PatchEventDataSlot =
    let toDomain (v: DataTransfer.PatchEventDataSlot) = v.ToDomain()

module PatchEventData =
    let tryToDomain (v: DataTransfer.PatchEventData) : Domain.EventUpdateData option =
        let registrationFields =
            match v.RegistrationFields with
            | Some fields -> RegistrationField.tryToDomainList fields |> Option.map Some
            | None -> Some None
        registrationFields
        |> Option.map (fun registrationFields -> {
            Key = v.Key
            Title = v.Title
            InfoText = v.InfoText
            ReservationStartTime = v.ReservationStartTime
            Slots = v.Slots |> Array.map PatchEventDataSlot.toDomain
            RegistrationConfirmationMail = Option.map PatchMailTemplate.toDomain v.RegistrationConfirmationMail
            RequestConfirmationMail = if v.SetRequestConfirmationMail then Some (Option.map PatchMailTemplate.toDomain v.RequestConfirmationMail) else None
            RegistrationFields = registrationFields
        })
