namespace HTLVB.RegistrationForm.Server.Domain

open HTLVB.RegistrationForm.Server
open System
open System.Net.Mail

type Slot = {
    Time: DateTime
    ClosingDate: DateTime option
    MaxQuantityPerBooking: int option
    RemainingCapacity: int option
}

type Event = {
    Key: string
    Title: string
    InfoText: string
    ReservationStartTime: DateTime
    Slots: Slot[]
    MailSubject: string
    MailContentTemplate: string
}

type Subscriber = {
    Quantity: int
    Name: string
    MailAddress: string
    PhoneNumber: string
}
module Subscriber =
    let validate (subscriber: DataTransfer.Subscriber) =
        let quantityIsValid = subscriber.Quantity > 0
        let subscriberNameIsValid = (not <| String.IsNullOrWhiteSpace subscriber.Name) && subscriber.Name.Length <= 100
        let mailAddressIsValid =
            let canParse =
                try
                    MailAddress subscriber.MailAddress |> ignore
                    true
                with _ -> false
            canParse && subscriber.MailAddress.Length <= 100
        let phoneNumberIsValid = (not <| String.IsNullOrWhiteSpace subscriber.PhoneNumber) && subscriber.PhoneNumber.Length <= 100
        if quantityIsValid && subscriberNameIsValid && mailAddressIsValid && phoneNumberIsValid
        then Ok { Quantity = subscriber.Quantity; Name = subscriber.Name; MailAddress = subscriber.MailAddress; PhoneNumber = subscriber.PhoneNumber }
        else Error ()

type BookingError =
    | CapacityExceeded of remainingCapacity: int
