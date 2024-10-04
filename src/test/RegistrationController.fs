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
            | :? DataTransfer.ReservationTypeFree as v when v.RemainingCapacity.HasValue -> Some (v.Url, v.RemainingCapacity.Value)
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
            | :? DataTransfer.ReservationTypeFree as v when v.RemainingCapacity.HasValue -> Some (v.Url, v.RemainingCapacity.Value)
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
            | :? DataTransfer.ReservationTypeFree as v when v.RemainingCapacity.HasValue && v.RemainingCapacity.Value > 1 -> Some v.Url
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
    let! reservationType = response.Content.ReadFromJsonAsync<DataTransfer.ReservationType>() |> Async.AwaitTask
    let reservationTypeFree = reservationType :?> DataTransfer.ReservationTypeFree
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
            | :? DataTransfer.ReservationTypeFree as v when not v.RemainingCapacity.HasValue -> Some v.Url
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
    let! reservationType = response.Content.ReadFromJsonAsync<DataTransfer.ReservationType>() |> Async.AwaitTask
    let reservationTypeFree = reservationType :?> DataTransfer.ReservationTypeFree
    Assert.Equal(None, Option.ofNullable reservationTypeFree.RemainingCapacity)
}
