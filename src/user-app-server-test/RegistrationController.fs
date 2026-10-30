module HTLVB.RegistrationForm.Server.Tests.RegistrationController

open HTLVB.RegistrationForm.Server
open HTLVB.RegistrationForm.Server.Tests.Utils
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection
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
        Data = Map.ofList [
            "FullName", "Albert"
            "MailAddress", "albert@einstein.com"
            "PhoneNumber", "07612/123456789"
        ]
        Quantity = quantity
    }

let private bookSlot server (httpClient: HttpClient) eventName pickSlot = async {
    let reservationStartTime = FakeData.events.[eventName].ReservationStartTime
    server |> InMemoryServer.setTimeProviderTime reservationStartTime
    let! event = httpClient.GetFromJsonAsync<DataTransfer.Event>($"/api/event/%s{eventName}") |> Async.AwaitTask
    let releasedEvent = event :?> DataTransfer.ReleasedEvent
    let (slotUrl: string, bookingQuantity) =
        releasedEvent.Slots
        |> Array.tryPick pickSlot
        |> Option.defaultWith (fun () -> Assert.Fail("No slot found"); Unchecked.defaultof<_>)
    return! httpClient.PostAsJsonAsync(slotUrl, getSampleReservationData bookingQuantity) |> Async.AwaitTask
}

let private bookFreeSlot server (httpClient: HttpClient) pickBookingQuantity = async {
    return! bookSlot server httpClient "tdot-2025" (fun v ->
        match v.Type with
        | :? DataTransfer.SlotTypeFree as v ->
            match pickBookingQuantity v with
            | Some bookingCapacity -> Some (v.Url, bookingCapacity)
            | None -> None
        | _ -> None
    )
}

let bookFreeSlot' pickBookingQuantity = async {
    use! server = InMemoryServer.start()
    use httpClient = server.GetTestClient()
    return! bookFreeSlot server httpClient pickBookingQuantity
}

let private requestSlot server (httpClient: HttpClient) pickBookingQuantity = async {
    return! bookSlot server httpClient "lets-code-2425" (fun v ->
        match v.Type with
        | :? DataTransfer.SlotTypeTakenWithRequestPossible as v ->
            match pickBookingQuantity v with
            | Some bookingCapacity -> Some (v.Url, bookingCapacity)
            | None -> None
        | _ -> None
    )
}

let requestSlot' pickBookingQuantity = async {
    use! server = InMemoryServer.start()
    use httpClient = server.GetTestClient()
    return! requestSlot server httpClient pickBookingQuantity
}

[<Fact>]
let ``Confirmation mail contains end of slot`` () = async {
    use! server = InMemoryServer.start()
    use httpClient = server.GetTestClient()
    let! response = bookSlot server httpClient "workshop-with-end" (fun v ->
        match v.Type with
        | :? DataTransfer.SlotTypeFree as v -> Some (v.Url, 1)
        | _ -> None
    )
    Assert.Equal(enum StatusCodes.Status200OK, response.StatusCode)
    let sender = server.Services.GetRequiredService<IBookingConfirmationSender>() :?> InMemoryBookingConfirmationSender
    let mail = Assert.Single(sender.SentMails)
    Assert.Equal("15.03.2025 22:30 - 16.03.2025 00:30", mail.Content)
}

[<Fact>]
let ``Can make booking when enough capacity`` () = async {
    let! response = bookFreeSlot' (fun slot ->
        if not slot.MaxQuantityPerBooking.HasValue && slot.RemainingCapacity.HasValue then
            Some slot.RemainingCapacity.Value
        else None
    )
    Assert.Equal(enum StatusCodes.Status200OK, response.StatusCode)
}

[<Fact>]
let ``Can't make booking when not enough capacity`` () = async {
    let! response = bookFreeSlot' (fun slot ->
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
    let! response = bookFreeSlot server httpClient (fun slot ->
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
    let! response = bookFreeSlot' (fun slot ->
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
    let! response = bookFreeSlot' (fun slot ->
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
        |> Array.tryPick (fun v ->
            match v.Type with
            | :? DataTransfer.SlotTypeFree as v when v.ClosingDate.HasValue ->
                Some (v.Url, v.ClosingDate.Value)
            | _ -> None
        )
        |> Option.defaultWith (fun () -> Assert.Fail("No free slot found"); Unchecked.defaultof<_>)
    server |> InMemoryServer.setTimeProviderTime (closingDate + offset)
    let reservationData : DataTransfer.Subscriber = {
        Data = Map.ofList [
            "FullName", "Albert"
            "MailAddress", "albert@einstein.com"
            "PhoneNumber", "07612/123456789"
        ]
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
        |> Array.tryFind (fun v -> v.StartTime = slotTime)
        |> Option.defaultWith (fun () -> Assert.Fail("Slot no longer found"); Unchecked.defaultof<_>)
    Assert.IsType<DataTransfer.SlotTypeClosed>(slot.Type) |> ignore
}

[<Fact>]
let ``Can make booking request`` () = async {
    let! response = requestSlot' (fun slot -> Some 1)
    Assert.Equal(enum StatusCodes.Status200OK, response.StatusCode)
}

[<Fact>]
let ``Can have booking request suggested`` () = async {
    use! server = InMemoryServer.start()
    use httpClient = server.GetTestClient()
    let reservationStartTime = FakeData.events.["lets-code-2425"].ReservationStartTime
    server |> InMemoryServer.setTimeProviderTime reservationStartTime
    let! event = httpClient.GetFromJsonAsync<DataTransfer.Event>($"/api/event/lets-code-2425") |> Async.AwaitTask
    let releasedEvent = event :?> DataTransfer.ReleasedEvent
    let slotUrl =
        releasedEvent.Slots
        |> Array.tryPick (fun v ->
            match v.Type with
            | :? DataTransfer.SlotTypeFree as v when v.RemainingCapacity = Nullable<_> 1 -> Some v.Url
            | _ -> None
        )
        |> Option.defaultWith (fun () -> Assert.Fail("No slot found"); Unchecked.defaultof<_>)
    let! _ = httpClient.PostAsJsonAsync(slotUrl, getSampleReservationData 1) |> Async.AwaitTask
    let! response = httpClient.PostAsJsonAsync(slotUrl, getSampleReservationData 1) |> Async.AwaitTask
    Assert.Equal(enum StatusCodes.Status400BadRequest, response.StatusCode)
    let! responseMessage = response.Content.ReadFromJsonAsync<{| Error: string |}[]>() |> Async.AwaitTask
    Assert.Equal([{| Error = "slot-needs-request" |}], responseMessage)
}

[<Fact>]
let ``Can't make booking with invalid registration fields`` () = async {
    use! server = InMemoryServer.start()
    use httpClient = server.GetTestClient()
    let reservationStartTime = FakeData.events.["tdot-2025"].ReservationStartTime
    server |> InMemoryServer.setTimeProviderTime reservationStartTime
    let! event = httpClient.GetFromJsonAsync<DataTransfer.Event>("/api/event/tdot-2025") |> Async.AwaitTask
    let releasedEvent = event :?> DataTransfer.ReleasedEvent
    let slotUrl =
        releasedEvent.Slots
        |> Array.tryPick (fun v -> match v.Type with | :? DataTransfer.SlotTypeFree as v -> Some v.Url | _ -> None)
        |> Option.defaultWith (fun () -> Assert.Fail("No slot found"); Unchecked.defaultof<_>)
    let reservationData : DataTransfer.Subscriber = {
        Data = Map.ofList [ "FullName", " "; "MailAddress", "albert" ]
        Quantity = 1
    }
    let! response = httpClient.PostAsJsonAsync(slotUrl, reservationData) |> Async.AwaitTask
    Assert.Equal(enum StatusCodes.Status400BadRequest, response.StatusCode)
    let! responseMessage = response.Content.ReadFromJsonAsync<{| Error: string; Key: string |}[]>() |> Async.AwaitTask
    let expected = [|
        {| Error = "invalid-registration-field"; Key = "FullName" |}
        {| Error = "invalid-registration-field"; Key = "MailAddress" |}
        {| Error = "invalid-registration-field"; Key = "PhoneNumber" |}
    |]
    Assert.Equal<{| Error: string; Key: string |}[]>(expected, responseMessage)
}

[<Fact>]
let ``Can get registration fields of released event`` () = async {
    let! event = getSampleEvent TimeSpan.Zero
    let releasedEvent = event :?> DataTransfer.ReleasedEvent
    Assert.Equal<string[]>([| "FullName"; "MailAddress"; "PhoneNumber" |], releasedEvent.RegistrationFields |> Array.map _.Key)
}
