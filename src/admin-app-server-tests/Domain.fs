module HTLVB.RegistrationForm.Admin.Server.Tests.Domain

open Expecto
open HTLVB.RegistrationForm.Admin.Server
open System

let private slot time canRequestIfFullyBooked : Domain.Slot =
    { Time = time; Duration = None; ClosingDate = None; MaxQuantityPerBooking = None; RemainingCapacity = None; CanRequestIfFullyBooked = canRequestIfFullyBooked }

let private event : Domain.EventData = {
    Key = "lets-code-2425"
    Title = "Let's code 2024/25"
    InfoText = ""
    ReservationStartTime = DateTime.Today
    Slots = [| slot (DateTime.Today.AddDays 1.) false |]
    RegistrationConfirmationMail = { Subject = "Anmeldung"; ContentTemplate = "Danke für deine Anmeldung" }
    RequestConfirmationMail = None
    EditorIds = [| "EINA@htlvb.at" |]
}

let private noUpdate : Domain.EventUpdateData = {
    Key = None
    Title = None
    InfoText = None
    ReservationStartTime = None
    Slots = [||]
    RegistrationConfirmationMail = None
    RequestConfirmationMail = None
}

[<Tests>]
let tests =
    testList "Domain" [
        test "Event without requests doesn't need request confirmation mail" {
            Expect.isTrue (Domain.EventData.hasRequiredRequestConfirmationMail event) "Event should be valid"
        }

        test "Event with requests needs request confirmation mail" {
            let event = { event with Slots = [| slot (DateTime.Today.AddDays 1.) true |] }
            Expect.isFalse (Domain.EventData.hasRequiredRequestConfirmationMail event) "Event without request confirmation mail should be invalid"
            let event = { event with RequestConfirmationMail = Some { Subject = "Anfrage"; ContentTemplate = "Danke für deine Anfrage" } }
            Expect.isTrue (Domain.EventData.hasRequiredRequestConfirmationMail event) "Event with request confirmation mail should be valid"
        }

        test "Allowing requests by updating slot needs request confirmation mail" {
            let update = {
                noUpdate with
                    Slots = [| Domain.UpdateSlot (DateTime.Today.AddDays 1., { Time = None; Duration = None; ClosingDate = None; MaxQuantityPerBooking = None; RemainingCapacity = None; CanRequestIfFullyBooked = Some true }) |]
            }
            let updatedEvent = Domain.EventData.applyUpdate update event
            Expect.isFalse (Domain.EventData.hasRequiredRequestConfirmationMail updatedEvent) "Updated event should be invalid"
        }

        test "Removing request confirmation mail of event with requests is invalid" {
            let event = { event with Slots = [| slot (DateTime.Today.AddDays 1.) true |]; RequestConfirmationMail = Some { Subject = "Anfrage"; ContentTemplate = "Danke für deine Anfrage" } }
            let updatedEvent = Domain.EventData.applyUpdate { noUpdate with RequestConfirmationMail = Some None } event
            Expect.isFalse (Domain.EventData.hasRequiredRequestConfirmationMail updatedEvent) "Updated event should be invalid"
        }

        test "Replacing slot that allows requests with one that doesn't is valid" {
            let time = DateTime.Today.AddDays 1.
            let event = { event with Slots = [| slot time true |] }
            let update = { noUpdate with Slots = [| Domain.CreateSlot (slot time false); Domain.DeleteSlot time |]; RequestConfirmationMail = Some None }
            let updatedEvent = Domain.EventData.applyUpdate update event
            Expect.equal updatedEvent.Slots [| slot time false |] "Slot should have been replaced"
            Expect.isTrue (Domain.EventData.hasRequiredRequestConfirmationMail updatedEvent) "Updated event should be valid"
        }
    ]
