namespace HTLVB.RegistrationForm.Server.Tests

open HTLVB.RegistrationForm.Server
open System

type EventRegistration = {
    Time: DateTime
    Quantity: int
    Name: string
    MailAddress: string
    PhoneNumber: string
    Timestamp: DateTime
}

type InMemoryEventStore(events, eventRegistrations) =
    let mutable events = events
    let mutable eventRegistrations = eventRegistrations
    interface IEventStore with
        member _.TryGetEvent eventKey = async {
            return Map.tryFind eventKey events
        }
        member _.TryBook bookingData = async {
            let remainingCapacity =
                match Map.tryFind bookingData.EventKey events with
                | None -> Some 0
                | Some event ->
                    event.Slots
                    |> Seq.tryFind (fun v -> v.Time = bookingData.SlotTime)
                    |> function
                    | Some slot -> slot.RemainingCapacity
                    | None -> Some 0
            match remainingCapacity with
            | Some remainingCapacity when bookingData.Subscriber.Quantity.Value > remainingCapacity ->
                return Error (Domain.CapacityExceeded remainingCapacity)
            | remainingCapacity ->
                let newRemainingCapacity = remainingCapacity |> Option.map (fun v -> v - bookingData.Subscriber.Quantity.Value)
                events <-
                    events
                    |> Map.map (fun key event ->
                        if key = bookingData.EventKey then
                            { event with
                                Slots =
                                    event.Slots
                                    |> Array.map (fun slot ->
                                        if slot.Time = bookingData.SlotTime then
                                            { slot with RemainingCapacity = newRemainingCapacity }
                                        else slot
                                    )
                            }
                        else event
                    )
                let eventRegistration = {
                    Time = bookingData.SlotTime
                    Quantity = bookingData.Subscriber.Quantity.Value
                    Name = bookingData.Subscriber.Name.Value
                    MailAddress = bookingData.Subscriber.MailAddress.Value
                    PhoneNumber = bookingData.Subscriber.PhoneNumber.Value
                    Timestamp = bookingData.Timestamp
                }
                eventRegistrations <-
                    eventRegistrations
                    |> Map.map (fun key eventRegistrations ->
                        if key = bookingData.EventKey then
                            eventRegistrations @ [ eventRegistration ]
                        else eventRegistrations
                    )
                return Ok newRemainingCapacity
        }
        member _.AddBookingRequest bookingData = async {
            return ()
        }
