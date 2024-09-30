namespace HTLVB.RegistrationForm.Server.Controllers

open HTLVB.RegistrationForm.Server
open HTLVB.RegistrationForm.Server.DataTransfer
open HTLVB.RegistrationForm.Server.Domain
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Options
open System
open System.Text.Json

[<ApiController>]
[<Route("/api/schedule")>]
type RegistrationController(appConfig: AppConfig, registrationStore: IRegistrationStore, bookingConfirmation: IBookingConfirmationSender, timeProvider: TimeProvider, jsonOptions: IOptions<JsonOptions>) =
    inherit ControllerBase()

    let getSlotStartTime (date: DateTimeOffset) (startTime: TimeSpan) (slotDuration: TimeSpan) slotNumber =
        date + startTime + slotDuration * (float <| slotNumber - 1)

    [<HttpGet>]
    member _.GetRegistrations () = async {
        if appConfig.ReservationStartTime > timeProvider.GetUtcNow() then
            return HiddenSchedule(appConfig.Title, appConfig.ReservationStartTime) :> Schedule
        else
            let! schedule = registrationStore.GetSchedule ()
            let scheduleEntries = [
                for date in appConfig.Dates do
                for slotNumber in [1..appConfig.NumberOfSlots] do
                    let startTime = getSlotStartTime date appConfig.StartTime appConfig.SlotDuration slotNumber
                    let quantityTaken =
                        schedule
                        |> List.filter (fun entry -> DateTimeOffset(entry.time) = startTime)
                        |> List.sumBy (fun entry -> entry.quantity)
                    let reservationsLeft = appConfig.ReservationsPerSlot - quantityTaken

                    {
                        StartTime = startTime
                        ReservationType =
                            if reservationsLeft <= 0 then ReservationTypeTaken() :> ReservationType
                            else ReservationTypeFree ($"api/schedule/%d{date.Year}/%d{date.Month}/%d{date.Day}/%d{slotNumber}", reservationsLeft)
                    }
            ]
            return ReleasedSchedule(appConfig.Title, appConfig.InfoText, scheduleEntries)
    }

    [<HttpPost("{year}/{month}/{day}/{slotNumber}")>]
    member this.CreateRegistration (year: int, month: int, day: int, slotNumber: int, [<FromBody()>] subscriber: DataTransfer.Subscriber) = async {
        let date = DateTimeOffset(DateTime(year, month, day))
        match Subscriber.validate subscriber with
        | Ok subscriber when appConfig.Dates |> List.contains date && slotNumber > 0 && slotNumber <= appConfig.NumberOfSlots -> 
            let slotStartTime = getSlotStartTime date appConfig.StartTime appConfig.SlotDuration slotNumber
            if DateTimeOffset.Now < appConfig.ReservationStartTime || DateTimeOffset.Now > slotStartTime then
                return this.BadRequest () :> IActionResult
            else
                let! reservationsLeft = registrationStore.Book appConfig.ReservationsPerSlot {
                    time = slotStartTime.DateTime
                    quantity = subscriber.Quantity
                    name = subscriber.Name
                    mail_address = subscriber.MailAddress
                    phone_number = subscriber.PhoneNumber
                    time_stamp = DateTime.Now
                }
                let newReservationType =
                    if reservationsLeft <= 0 then ReservationTypeTaken() :> ReservationType
                    else ReservationTypeFree ($"api/schedule/%d{date.Year}/%d{date.Month}/%d{date.Day}/%d{slotNumber}", reservationsLeft)
                let mailSettings = {
                    Recipient = {
                        Name = subscriber.Name
                        MailAddress = subscriber.MailAddress
                    }
                    Subject =  appConfig.MailConfig.Subject
                    Content =
                        let templateVars = [
                            "FullName", subscriber.Name
                            "Date", slotStartTime.ToString("d", appConfig.Culture)
                            "Time", slotStartTime.ToString("t", appConfig.Culture)
                        ]
                        (appConfig.MailConfig.ContentTemplate, templateVars)
                        ||> List.fold (fun text (varName, value) -> text.Replace(sprintf "{{{%s}}}" varName, value))
                }
                do! bookingConfirmation.SendBookingConfirmation mailSettings
                return this.Ok(JsonSerializer.SerializeToElement(newReservationType, jsonOptions.Value.JsonSerializerOptions))
        | _ -> return this.BadRequest()
    }