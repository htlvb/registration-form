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
type RegistrationController(appConfig: AppConfig, timeProvider: TimeProvider, jsonOptions: IOptions<JsonOptions>) =
    inherit ControllerBase()

    let getSlotStartTime (date: DateTimeOffset) (startTime: TimeSpan) (slotDuration: TimeSpan) slotNumber =
        date + startTime + slotDuration * (float <| slotNumber - 1)

    [<HttpGet>]
    member _.GetRegistrations () = async {
        let! schedule = Db.getSchedule (appConfig.DbConnectionString)
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
        if appConfig.ReservationStartTime > timeProvider.GetUtcNow() then
            return HiddenSchedule(appConfig.Title, appConfig.ReservationStartTime) :> Schedule
        else
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
                let! reservationsLeft = Db.book appConfig.DbConnectionString appConfig.ReservationsPerSlot {
                    Db.Schedule.time = slotStartTime.DateTime
                    Db.Schedule.quantity = subscriber.Quantity
                    Db.Schedule.name = subscriber.Name
                    Db.Schedule.mail_address = subscriber.MailAddress
                    Db.Schedule.phone_number = subscriber.PhoneNumber
                    Db.Schedule.time_stamp = DateTime.Now
                }
                let newReservationType =
                    if reservationsLeft <= 0 then ReservationTypeTaken() :> ReservationType
                    else ReservationTypeFree ($"api/schedule/%d{date.Year}/%d{date.Month}/%d{date.Day}/%d{slotNumber}", reservationsLeft)
                let settings = {
                    Mail.Settings.SmtpAddress = appConfig.MailConfig.SmtpAddress
                    Mail.Settings.MailboxUserName =  appConfig.MailConfig.MailboxUserName
                    Mail.Settings.MailboxPassword =  appConfig.MailConfig.MailboxPassword
                    Mail.Settings.Sender =  appConfig.MailConfig.Sender
                    Mail.Settings.Recipient = {
                        Mail.User.Name = subscriber.Name
                        Mail.User.MailAddress = subscriber.MailAddress
                    }
                    Mail.Settings.BccRecipient =  appConfig.MailConfig.BccRecipient
                    Mail.Settings.Subject =  appConfig.MailConfig.Subject
                    Mail.Settings.Content =
                        let templateVars = [
                            "FullName", subscriber.Name
                            "Date", slotStartTime.ToString("d", appConfig.Culture)
                            "Time", slotStartTime.ToString("t", appConfig.Culture)
                        ]
                        (appConfig.MailConfig.ContentTemplate, templateVars)
                        ||> List.fold (fun text (varName, value) -> text.Replace(sprintf "{{{%s}}}" varName, value))
                }
                do! Mail.sendBookingConfirmation settings
                return this.Ok(JsonSerializer.SerializeToElement(newReservationType, jsonOptions.Value.JsonSerializerOptions))
        | _ -> return this.BadRequest()
    }