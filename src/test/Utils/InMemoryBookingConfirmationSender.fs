namespace HTLVB.RegistrationForm.Server.Tests.Utils

open HTLVB.RegistrationForm.Server

type InMemoryBookingConfirmationSender() =
    let mutable sentMails : MailSettings list = []
    member _.SentMails with get() = sentMails

    interface IBookingConfirmationSender with
        member _.SendBookingConfirmation mailSettings =  async {
            sentMails <- sentMails @ [ mailSettings ]
        }