namespace HTLVB.RegistrationForm.Server

open Markdig
open Microsoft.Extensions.Configuration
open System
open System.Globalization

type MailConfig = {
    SmtpAddress: string
    MailboxUserName: string
    MailboxPassword: string
    Sender: MailUser
    BccRecipient: MailUser option
    Subject: string
    ContentTemplate: string
}

type AppConfig = {
    Culture: CultureInfo
    Title: string
    ReservationStartTime: DateTimeOffset
    ReservationsPerSlot: int
    Dates: DateTimeOffset list
    InfoText: string
    StartTime: TimeSpan
    SlotDuration: TimeSpan
    NumberOfSlots: int
    MailConfig: MailConfig
}
module AppConfig =
    let fromEnvironment (config: IConfiguration) =
        let tryParseInt (text: string) =
            match Int32.TryParse(text) with
            | (true, v) -> Some v
            | _ -> None
        let timeZone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time")
        let tryParseDateTime (format: string) (text: string) =
            match DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None) with
            | (true, v) -> Some (DateTimeOffset(v, timeZone.GetUtcOffset(v)))
            | _ -> None
        let tryParseTimeSpan (format: string) (text: string) =
            match TimeSpan.TryParseExact(text, format, CultureInfo.InvariantCulture) with
            | (true, v) -> Some v
            | _ -> None

        let optEnvVar =
            config.GetValue<string> >> Option.ofObj
        let envVar name =
            optEnvVar name |> Option.defaultWith (fun () -> failwithf "Environment variable \"%s\" not set" name)
        let envVarList (separator: string) name parse =
            let value = envVar name
            value.Split(separator)
            |> Seq.map (parse >> Option.defaultWith (fun () -> failwithf "Environment variable \"%s\" can't be parsed as list" name))
            |> Seq.toList
        let envVarAsInt name =
            envVar name
            |> tryParseInt
            |> Option.defaultWith (fun () -> failwithf "Environment variable \"%s\" can't be parsed as integer" name)
        let envVarAsDateTime name format =
            envVar name
            |> tryParseDateTime format
            |> Option.defaultWith (fun () -> failwithf "Environment variable \"%s\" can't be parsed as date time (format must be \"%s\")" name format)
        let envVarAsTimeSpan name format =
            envVar name
            |> tryParseTimeSpan format
            |> Option.defaultWith (fun () -> failwithf "Environment variable \"%s\" can't be parsed as time span (format must be \"%s\")" name format)

        let parseMarkdown (text: string) = Markdown.ToHtml(text)

        {
            Culture = CultureInfo.GetCultureInfo("de-AT")
            Title = envVar "APP_TITLE"
            ReservationStartTime = envVarAsDateTime "RESERVATION_START_TIME" "dd.MM.yyyy HH:mm:ss"
            ReservationsPerSlot = envVarAsInt "SCHEDULE_RESERVATIONS_PER_SLOT"
            Dates = envVarList "," "SCHEDULE_DATES" (tryParseDateTime "dd.MM.yyyy")
            InfoText = envVar "INFO_TEXT" |> parseMarkdown
            StartTime = envVarAsTimeSpan "SCHEDULE_START_TIME" "hh\\:mm"
            SlotDuration = envVarAsTimeSpan "SCHEDULE_SLOT_DURATION" "hh\\:mm"
            NumberOfSlots = envVarAsInt "SCHEDULE_NUMBER_OF_SLOTS"
            MailConfig = {
                SmtpAddress = envVar "MAILBOX_SMTP_ADDRESS"
                MailboxUserName = envVar "MAILBOX_USERNAME"
                MailboxPassword = envVar "MAILBOX_PASSWORD"
                Sender = {
                    Name = envVar "MAIL_SENDER_NAME"
                    MailAddress = envVar "MAIL_SENDER_MAIL_ADDRESS"
                }
                BccRecipient =
                    match optEnvVar "MAIL_BCC_RECIPIENT_NAME", optEnvVar "MAIL_BCC_RECIPIENT_MAIL_ADDRESS" with
                    | Some name, Some mailAddress -> Some { Name = name; MailAddress = mailAddress }
                    | _ -> None
                Subject = envVar "MAIL_SUBJECT"
                ContentTemplate = envVar "MAIL_CONTENT_TEMPLATE"
            }
        }