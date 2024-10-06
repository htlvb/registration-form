module HTLVB.RegistrationForm.Server.Tests.RegistrationController

open HTLVB.RegistrationForm.Server
open HTLVB.RegistrationForm.Server.Tests.Utils
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.TestHost
open System
open System.Net.Http
open System.Net.Http.Json
open Xunit

let private getSampleEvent reservationStartTimeOffset = async {
    use! server = InMemoryServer.start()
    use httpClient = server.GetTestClient()
    let reservationStartTime = FakeData.events.["tdot-2025"].ReservationStartTime
    server |> InMemoryServer.setTimeProviderTime (reservationStartTime + reservationStartTimeOffset)
    return! httpClient.GetFromJsonAsync<DataTransfer.Event>("/api/event/tdot-2025") |> Async.AwaitTask
}

[<Fact>]
let ``Can get released event`` () = async {
    let! event = getSampleEvent TimeSpan.Zero
    Assert.IsType<DataTransfer.ReleasedEvent>(event) |> ignore
}

[<Fact>]
let ``Can get hidden event`` () = async {
    let! event = getSampleEvent (TimeSpan.FromSeconds -1.)
    Assert.IsType<DataTransfer.HiddenEvent>(event) |> ignore
}

[<Fact>]
let ``Doesn't find non-existing event`` () = async {
    use! server = InMemoryServer.start()
    use httpClient = server.GetTestClient()
    let! response = httpClient.GetAsync("/api/event/tdot-2026") |> Async.AwaitTask
    Assert.Equal(enum StatusCodes.Status404NotFound, response.StatusCode)
}

let private getSampleReservationData quantity : DataTransfer.Subscriber =
    {
        Name = "Albert"
        MailAddress = "albert@einstein.com"
        PhoneNumber = "07612/123456789"
        Quantity = quantity
    }

let private makeSampleBooking server (httpClient: HttpClient) pickBookingQuantity = async {
    let reservationStartTime = FakeData.events.["tdot-2025"].ReservationStartTime
    server |> InMemoryServer.setTimeProviderTime reservationStartTime
    let! event = httpClient.GetFromJsonAsync<DataTransfer.Event>("/api/event/tdot-2025") |> Async.AwaitTask
    let releasedEvent = event :?> DataTransfer.ReleasedEvent
    let (slotUrl, bookingQuantity) =
        releasedEvent.Slots
        |> List.tryPick (fun v ->
            match v.Type with
            | :? DataTransfer.SlotTypeFree as v ->
                match pickBookingQuantity v with
                | Some bookingCapacity -> Some (v.Url, bookingCapacity)
                | None -> None
            | _ -> None
        )
        |> Option.defaultWith (fun () -> Assert.Fail("No slot found"); Unchecked.defaultof<_>)
    return! httpClient.PostAsJsonAsync(slotUrl, getSampleReservationData bookingQuantity) |> Async.AwaitTask
}

let makeSampleBooking' pickBookingQuantity = async {
    use! server = InMemoryServer.start()
    use httpClient = server.GetTestClient()
    return! makeSampleBooking server httpClient pickBookingQuantity
}

[<Fact>]
let ``Can make booking when enough capacity`` () = async {
    let! response = makeSampleBooking' (fun slot ->
        if not slot.MaxQuantityPerBooking.HasValue && slot.RemainingCapacity.HasValue then
            Some slot.RemainingCapacity.Value
        else None
    )
    Assert.Equal(enum StatusCodes.Status200OK, response.StatusCode)
}

[<Fact>]
let ``Can't make booking when not enough capacity`` () = async {
    let! response = makeSampleBooking' (fun slot ->
        if not slot.MaxQuantityPerBooking.HasValue && slot.RemainingCapacity.HasValue then
            Some (slot.RemainingCapacity.Value + 1)
        else None
    )
    Assert.Equal(enum StatusCodes.Status400BadRequest, response.StatusCode)
}

[<Fact>]
let ``Can make two consecutive bookings when enough capacity`` () = async {
    use! server = InMemoryServer.start()
    use httpClient = server.GetTestClient()
    let! response = makeSampleBooking server httpClient (fun slot ->
        if slot.RemainingCapacity.HasValue && slot.RemainingCapacity.Value > 1 then
            Some 1
        else None
    )
    let! bookingResult = response.Content.ReadFromJsonAsync<DataTransfer.BookingResult>() |> Async.AwaitTask
    let slotTypeFree = bookingResult.SlotType :?> DataTransfer.SlotTypeFree
    let! response = httpClient.PostAsJsonAsync(slotTypeFree.Url, getSampleReservationData 1) |> Async.AwaitTask
    Assert.Equal(enum StatusCodes.Status200OK, response.StatusCode)
}

[<Fact>]
let ``Can make booking when no capacity limit`` () = async {
    let! response = makeSampleBooking' (fun slot ->
        if not slot.MaxQuantityPerBooking.HasValue && not slot.RemainingCapacity.HasValue then
            Some Int32.MaxValue
        else None
    )
    let! bookingResult = response.Content.ReadFromJsonAsync<DataTransfer.BookingResult>() |> Async.AwaitTask
    let slotTypeFree = bookingResult.SlotType :?> DataTransfer.SlotTypeFree
    Assert.Equal(None, Option.ofNullable slotTypeFree.RemainingCapacity)
}

[<Fact>]
let ``Can't make booking when quantity > max quantity per booking`` () = async {
    let! response = makeSampleBooking' (fun slot ->
        if slot.MaxQuantityPerBooking.HasValue && (not slot.RemainingCapacity.HasValue || slot.RemainingCapacity.Value > slot.MaxQuantityPerBooking.Value) then
            Some (slot.MaxQuantityPerBooking.Value + 1)
        else None
    )
    Assert.Equal(enum StatusCodes.Status400BadRequest, response.StatusCode)
}

let makeBookingAroundClosingDate offset = async {
    use! server = InMemoryServer.start()
    use httpClient = server.GetTestClient()
    let reservationStartTime = FakeData.events.["lets-code-2425"].ReservationStartTime
    server |> InMemoryServer.setTimeProviderTime reservationStartTime
    let! event = httpClient.GetFromJsonAsync<DataTransfer.Event>("/api/event/lets-code-2425") |> Async.AwaitTask
    let releasedEvent = event :?> DataTransfer.ReleasedEvent
    let (slotUrl, closingDate) =
        releasedEvent.Slots
        |> List.tryPick (fun v ->
            match v.Type with
            | :? DataTransfer.SlotTypeFree as v when v.ClosingDate.HasValue ->
                Some (v.Url, v.ClosingDate.Value)
            | _ -> None
        )
        |> Option.defaultWith (fun () -> Assert.Fail("No free slot found"); Unchecked.defaultof<_>)
    server |> InMemoryServer.setTimeProviderTime (closingDate + offset)
    let reservationData : DataTransfer.Subscriber = {
        Name = "Albert"
        MailAddress = "albert@einstein.com"
        PhoneNumber = "07612/123456789"
        Quantity = 1
    }
    return! httpClient.PostAsJsonAsync(slotUrl, reservationData) |> Async.AwaitTask
}

[<Fact>]
let ``Can make booking before closing date`` () = async {
    let! response = makeBookingAroundClosingDate (TimeSpan.FromSeconds -1.)
    Assert.Equal(enum StatusCodes.Status200OK, response.StatusCode)
}

[<Fact>]
let ``Can't make booking after closing date`` () = async {
    let! response = makeBookingAroundClosingDate (TimeSpan.Zero)
    Assert.Equal(enum StatusCodes.Status400BadRequest, response.StatusCode)
}

[<Fact>]
let ``Can get closed slot`` () = async {
    use! server = InMemoryServer.start()
    use httpClient = server.GetTestClient()
    let (slotTime, closingDate) =
        FakeData.events.["lets-code-2425"].Slots
        |> Array.tryPick (fun v -> match v.ClosingDate with | Some closingDate -> Some (v.Time, closingDate) | None -> None)
        |> Option.defaultWith (fun () -> Assert.Fail("No slot with closing date found"); Unchecked.defaultof<_>)
    server |> InMemoryServer.setTimeProviderTime closingDate
    let! event = httpClient.GetFromJsonAsync<DataTransfer.Event>("/api/event/lets-code-2425") |> Async.AwaitTask
    let releasedEvent = event :?> DataTransfer.ReleasedEvent
    let slot =
        releasedEvent.Slots
        |> List.tryFind (fun v -> v.StartTime = slotTime)
        |> Option.defaultWith (fun () -> Assert.Fail("Slot no longer found"); Unchecked.defaultof<_>)
    Assert.IsType<DataTransfer.SlotTypeClosed>(slot.Type) |> ignore
}
