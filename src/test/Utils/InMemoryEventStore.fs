namespace HTLVB.RegistrationForm.Server.Tests

open HTLVB.RegistrationForm.Server

type InMemoryEventStore(events, eventRegistrations) =
    let mutable events = events
    let mutable eventRegistrations = eventRegistrations
    interface IEventStore with
        member _.TryGetEvent eventKey = async {
            return Map.tryFind eventKey events
        }
        member _.GetEventRegistrations eventKey = async {
            return eventRegistrations |> Map.tryFind eventKey |> Option.defaultValue []
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
                    time = bookingData.SlotTime
                    quantity = bookingData.Subscriber.Quantity.Value
                    name = bookingData.Subscriber.Name.Value
                    mail_address = bookingData.Subscriber.MailAddress.Value
                    phone_number = bookingData.Subscriber.PhoneNumber.Value
                    time_stamp = bookingData.Timestamp
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
