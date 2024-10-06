module HTLVB.RegistrationForm.Server.Tests.RegistrationController

open HTLVB.RegistrationForm.Server
open HTLVB.RegistrationForm.Server.Tests.Utils
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.TestHost
open System
open System.Net.Http.Json
open Xunit

[<Fact>]
let ``Can get released schedule`` () = async {
    use! server = InMemoryServer.start()
    use httpClient = server.GetTestClient()
    let reservationStartTime = FakeData.events.["tdot-2025"].ReservationStartTime
    server |> InMemoryServer.setTimeProviderTime reservationStartTime
    let! schedule = httpClient.GetFromJsonAsync<DataTransfer.Schedule>("/api/schedule/tdot-2025") |> Async.AwaitTask
    Assert.IsType<DataTransfer.ReleasedSchedule>(schedule) |> ignore
}

[<Fact>]
let ``Can get hidden schedule`` () = async {
    use! server = InMemoryServer.start()
    use httpClient = server.GetTestClient()
    let reservationStartTime = FakeData.events.["tdot-2025"].ReservationStartTime
    server |> InMemoryServer.setTimeProviderTime (reservationStartTime - TimeSpan.FromSeconds 1.)
    let! schedule = httpClient.GetFromJsonAsync<DataTransfer.Schedule>("/api/schedule/tdot-2025") |> Async.AwaitTask
    Assert.IsType<DataTransfer.HiddenSchedule>(schedule) |> ignore
}

[<Fact>]
let ``Doesn't find non-existing event`` () = async {
    use! server = InMemoryServer.start()
    use httpClient = server.GetTestClient()
    let! response = httpClient.GetAsync("/api/schedule/tdot-2026") |> Async.AwaitTask
    Assert.Equal(enum StatusCodes.Status404NotFound, response.StatusCode)
}

[<Fact>]
let ``Can make booking when enough capacity`` () = async {
    use! server = InMemoryServer.start()
    use httpClient = server.GetTestClient()
    let reservationStartTime = FakeData.events.["tdot-2025"].ReservationStartTime
    server |> InMemoryServer.setTimeProviderTime reservationStartTime
    let! schedule = httpClient.GetFromJsonAsync<DataTransfer.Schedule>("/api/schedule/tdot-2025") |> Async.AwaitTask
    let releasedSchedule = schedule :?> DataTransfer.ReleasedSchedule
    let (slotUrl, slotCapacity) =
        releasedSchedule.Entries
        |> List.tryPick (fun v ->
            match v.ReservationType with
            | :? DataTransfer.ReservationTypeFree as v when not v.MaxQuantityPerBooking.HasValue && v.RemainingCapacity.HasValue ->
                Some (v.Url, v.RemainingCapacity.Value)
            | _ -> None
        )
        |> Option.defaultWith (fun () -> Assert.Fail("No free slot found"); Unchecked.defaultof<_>)
    let reservationData : DataTransfer.Subscriber = {
        Name = "Albert"
        MailAddress = "albert@einstein.com"
        PhoneNumber = "07612/123456789"
        Quantity = slotCapacity
    }
    let! response = httpClient.PostAsJsonAsync(slotUrl, reservationData) |> Async.AwaitTask
    Assert.Equal(enum StatusCodes.Status200OK, response.StatusCode)
}

[<Fact>]
let ``Can't make booking when not enough capacity`` () = async {
    use! server = InMemoryServer.start()
    use httpClient = server.GetTestClient()
    let reservationStartTime = FakeData.events.["tdot-2025"].ReservationStartTime
    server |> InMemoryServer.setTimeProviderTime reservationStartTime
    let! schedule = httpClient.GetFromJsonAsync<DataTransfer.Schedule>("/api/schedule/tdot-2025") |> Async.AwaitTask
    let releasedSchedule = schedule :?> DataTransfer.ReleasedSchedule
    let (slotUrl, slotCapacity) =
        releasedSchedule.Entries
        |> List.tryPick (fun v ->
            match v.ReservationType with
            | :? DataTransfer.ReservationTypeFree as v when not v.MaxQuantityPerBooking.HasValue && v.RemainingCapacity.HasValue ->
                Some (v.Url, v.RemainingCapacity.Value)
            | _ -> None
        )
        |> Option.defaultWith (fun () -> Assert.Fail("No free slot found"); Unchecked.defaultof<_>)
    let reservationData : DataTransfer.Subscriber = {
        Name = "Albert"
        MailAddress = "albert@einstein.com"
        PhoneNumber = "07612/123456789"
        Quantity = slotCapacity + 1
    }
    let! response = httpClient.PostAsJsonAsync(slotUrl, reservationData) |> Async.AwaitTask
    Assert.Equal(enum StatusCodes.Status400BadRequest, response.StatusCode)
}

[<Fact>]
let ``Can make two consecutive bookings when enough capacity`` () = async {
    use! server = InMemoryServer.start()
    use httpClient = server.GetTestClient()
    let reservationStartTime = FakeData.events.["tdot-2025"].ReservationStartTime
    server |> InMemoryServer.setTimeProviderTime reservationStartTime
    let! schedule = httpClient.GetFromJsonAsync<DataTransfer.Schedule>("/api/schedule/tdot-2025") |> Async.AwaitTask
    let releasedSchedule = schedule :?> DataTransfer.ReleasedSchedule
    let slotUrl =
        releasedSchedule.Entries
        |> List.tryPick (fun v ->
            match v.ReservationType with
            | :? DataTransfer.ReservationTypeFree as v when v.RemainingCapacity.HasValue && v.RemainingCapacity.Value > 1 ->
                Some v.Url
            | _ -> None
        )
        |> Option.defaultWith (fun () -> Assert.Fail("No free slot found"); Unchecked.defaultof<_>)
    let reservationData : DataTransfer.Subscriber = {
        Name = "Albert"
        MailAddress = "albert@einstein.com"
        PhoneNumber = "07612/123456789"
        Quantity = 1
    }
    let! response = httpClient.PostAsJsonAsync(slotUrl, reservationData) |> Async.AwaitTask
    let! bookingResult = response.Content.ReadFromJsonAsync<DataTransfer.BookingResult>() |> Async.AwaitTask
    let reservationTypeFree = bookingResult.ReservationType :?> DataTransfer.ReservationTypeFree
    let! response = httpClient.PostAsJsonAsync(reservationTypeFree.Url, reservationData) |> Async.AwaitTask
    Assert.Equal(enum StatusCodes.Status200OK, response.StatusCode)
}

[<Fact>]
let ``Can make booking when no capacity limit`` () = async {
    use! server = InMemoryServer.start()
    use httpClient = server.GetTestClient()
    let reservationStartTime = FakeData.events.["tdot-2025"].ReservationStartTime
    server |> InMemoryServer.setTimeProviderTime reservationStartTime
    let! schedule = httpClient.GetFromJsonAsync<DataTransfer.Schedule>("/api/schedule/tdot-2025") |> Async.AwaitTask
    let releasedSchedule = schedule :?> DataTransfer.ReleasedSchedule
    let slotUrl =
        releasedSchedule.Entries
        |> List.tryPick (fun v ->
            match v.ReservationType with
            | :? DataTransfer.ReservationTypeFree as v when not v.MaxQuantityPerBooking.HasValue && not v.RemainingCapacity.HasValue ->
                Some v.Url
            | _ -> None
        )
        |> Option.defaultWith (fun () -> Assert.Fail("No free slot found"); Unchecked.defaultof<_>)
    let reservationData : DataTransfer.Subscriber = {
        Name = "Albert"
        MailAddress = "albert@einstein.com"
        PhoneNumber = "07612/123456789"
        Quantity = Int32.MaxValue
    }
    let! response = httpClient.PostAsJsonAsync(slotUrl, reservationData) |> Async.AwaitTask
    let! bookingResult = response.Content.ReadFromJsonAsync<DataTransfer.BookingResult>() |> Async.AwaitTask
    let reservationTypeFree = bookingResult.ReservationType :?> DataTransfer.ReservationTypeFree
    Assert.Equal(None, Option.ofNullable reservationTypeFree.RemainingCapacity)
}

[<Fact>]
let ``Can't make booking when quantity > max quantity per booking`` () = async {
    use! server = InMemoryServer.start()
    use httpClient = server.GetTestClient()
    let reservationStartTime = FakeData.events.["tdot-2025"].ReservationStartTime
    server |> InMemoryServer.setTimeProviderTime reservationStartTime
    let! schedule = httpClient.GetFromJsonAsync<DataTransfer.Schedule>("/api/schedule/tdot-2025") |> Async.AwaitTask
    let releasedSchedule = schedule :?> DataTransfer.ReleasedSchedule
    let (slotUrl, maxQuantityPerBooking) =
        releasedSchedule.Entries
        |> List.tryPick (fun v ->
            match v.ReservationType with
            | :? DataTransfer.ReservationTypeFree as v when v.MaxQuantityPerBooking.HasValue && (not v.RemainingCapacity.HasValue || v.RemainingCapacity.Value > v.MaxQuantityPerBooking.Value) ->
                Some (v.Url, v.MaxQuantityPerBooking.Value)
            | _ -> None
        )
        |> Option.defaultWith (fun () -> Assert.Fail("No free slot found"); Unchecked.defaultof<_>)
    let reservationData : DataTransfer.Subscriber = {
        Name = "Albert"
        MailAddress = "albert@einstein.com"
        PhoneNumber = "07612/123456789"
        Quantity = maxQuantityPerBooking + 1
    }
    let! response = httpClient.PostAsJsonAsync(slotUrl, reservationData) |> Async.AwaitTask
    Assert.Equal(enum StatusCodes.Status400BadRequest, response.StatusCode)
}

let makeBookingAroundClosingDate offset = async {
    use! server = InMemoryServer.start()
    use httpClient = server.GetTestClient()
    let reservationStartTime = FakeData.events.["lets-code-2425"].ReservationStartTime
    server |> InMemoryServer.setTimeProviderTime reservationStartTime
    let! schedule = httpClient.GetFromJsonAsync<DataTransfer.Schedule>("/api/schedule/lets-code-2425") |> Async.AwaitTask
    let releasedSchedule = schedule :?> DataTransfer.ReleasedSchedule
    let (slotUrl, closingDate) =
        releasedSchedule.Entries
        |> List.tryPick (fun v ->
            match v.ReservationType with
            | :? DataTransfer.ReservationTypeFree as v when v.ClosingDate.HasValue ->
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
    let! schedule = httpClient.GetFromJsonAsync<DataTransfer.Schedule>("/api/schedule/lets-code-2425") |> Async.AwaitTask
    let releasedSchedule = schedule :?> DataTransfer.ReleasedSchedule
    let scheduleEntry =
        releasedSchedule.Entries
        |> List.tryFind (fun v -> v.StartTime = slotTime)
        |> Option.defaultWith (fun () -> Assert.Fail("Slot no longer found"); Unchecked.defaultof<_>)
    Assert.IsType<DataTransfer.ReservationTypeClosed>(scheduleEntry.ReservationType) |> ignore
}
