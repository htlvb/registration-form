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
        member _.TryBook eventKey data = async {
            let remainingCapacity =
                match Map.tryFind eventKey events with
                | None -> Some 0
                |Some event ->
                    event.Slots
                    |> Seq.tryFind (fun v -> v.Time = data.time)
                    |> function
                    | Some slot -> slot.RemainingCapacity
                    | None -> Some 0
            match remainingCapacity with
            | Some remainingCapacity when data.quantity > remainingCapacity ->
                return Error (Domain.CapacityExceeded remainingCapacity)
            | remainingCapacity ->
                let newRemainingCapacity = remainingCapacity |> Option.map (fun v -> v - data.quantity)
                events <-
                    events
                    |> Map.map (fun key event ->
                        if key = eventKey then
                            { event with
                                Slots =
                                    event.Slots
                                    |> Array.map (fun slot ->
                                        if slot.Time = data.time then
                                            { slot with RemainingCapacity = newRemainingCapacity }
                                        else slot
                                    )
                            }
                        else event
                    )
                eventRegistrations <-
                    eventRegistrations
                    |> Map.map (fun key eventRegistrations ->
                        if key = eventKey then eventRegistrations @ [ data ]
                        else eventRegistrations
                    )
                return Ok newRemainingCapacity
        }
