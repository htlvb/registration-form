namespace HTLVB.RegistrationForm.Server

open MailKit.Net.Smtp
open MailKit.Security
open MimeKit
open MimeKit.Text
open Microsoft.Graph

type MailUser = {
    Name: string
    MailAddress: string
}

type SmtpMailSettings = {
    SmtpAddress: string
    MailboxUserName: string
    MailboxPassword: string
    Sender: MailUser
    BccRecipient: MailUser option
}

type MSGraphMailSettings = {
    MailboxUserName: string
    Sender: MailUser
    BccRecipients: MailUser list
}

type MailSettings = {
    Recipient: MailUser
    Subject: string
    Content: string
}

type IBookingConfirmationSender =
    abstract member SendBookingConfirmation: MailSettings -> Async<unit>

type SmtpBookingConfirmationSender(settings: SmtpMailSettings) =
    interface IBookingConfirmationSender with
        member _.SendBookingConfirmation mail = async {
            use message = new MimeMessage()
            message.From.Add(MailboxAddress(settings.Sender.Name, settings.Sender.MailAddress))
            message.To.Add(MailboxAddress(mail.Recipient.Name, mail.Recipient.MailAddress))
            settings.BccRecipient |> Option.iter (fun user -> message.Bcc.Add(MailboxAddress(user.Name, user.MailAddress)))
            message.Subject <- mail.Subject
            message.Body <- new TextPart(TextFormat.Plain, Text = mail.Content)

            use smtp = new SmtpClient()
            let! ct = Async.CancellationToken
            do! smtp.ConnectAsync(settings.SmtpAddress, 587, SecureSocketOptions.StartTls, ct) |> Async.AwaitTask
            do! smtp.AuthenticateAsync(settings.MailboxUserName, settings.MailboxPassword, ct) |> Async.AwaitTask
            do! smtp.SendAsync(message, ct) |> Async.AwaitTask |> Async.Ignore
            do! smtp.DisconnectAsync(true, ct) |> Async.AwaitTask
        }

type MSGraphBookingConfirmationSender(graphServiceClient: GraphServiceClient, settings: MSGraphMailSettings) =
    interface IBookingConfirmationSender with
        member _.SendBookingConfirmation mailSettings = async {
            let mail =
                Users.Item.SendMail.SendMailPostRequestBody(
                    Message =
                        Models.Message(
                            From = Models.Recipient(EmailAddress = Models.EmailAddress(Address = settings.Sender.MailAddress, Name = settings.Sender.Name)),
                            ToRecipients = ([ Models.Recipient(EmailAddress = Models.EmailAddress(Address = mailSettings.Recipient.MailAddress, Name = mailSettings.Recipient.Name)) ] |> System.Collections.Generic.List),
                            BccRecipients = (
                                settings.BccRecipients
                                |> List.map (fun v -> Models.Recipient(EmailAddress = Models.EmailAddress(Address = v.MailAddress, Name = v.Name)))
                                |> System.Collections.Generic.List
                            ),
                            Subject = mailSettings.Subject,
                            Body = Models.ItemBody(Content = mailSettings.Content, ContentType = Models.BodyType.Text)
                        ),
                    SaveToSentItems = false
                )
            do! graphServiceClient.Users.[settings.MailboxUserName].SendMail.PostAsync(mail) |> Async.AwaitTask
        }
