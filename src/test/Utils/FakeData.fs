module HTLVB.RegistrationForm.Server.Tests.Utils.FakeData

open HTLVB.RegistrationForm.Server.Domain
open System

let events =
    [
        {
            Key = "tdot-2025"
            Title = "Tag der offenen Tür 2025"
            InfoText = "**Besondere Umstände erfordern besondere Maßnahmen**. Bitte reservieren Sie für den diesjährigen Tag der offenen Tür einen freien Termin."
            ReservationStartTime = DateTime(2024, 9, 29, 0, 0, 0)
            Slots = [|
                { Time = DateTime(2024, 10, 15, 13, 0, 0); Duration = None; ClosingDate = None; MaxQuantityPerBooking = Some 5; RemainingCapacity = Some 20 }
                { Time = DateTime(2024, 10, 15, 14, 0, 0); Duration = None; ClosingDate = None; MaxQuantityPerBooking = None; RemainingCapacity = Some 15 }
                { Time = DateTime(2024, 10, 15, 15, 0, 0); Duration = None; ClosingDate = None; MaxQuantityPerBooking = None; RemainingCapacity = Some 10 }
                { Time = DateTime(2024, 10, 16, 13, 0, 0); Duration = None; ClosingDate = None; MaxQuantityPerBooking = None; RemainingCapacity = None }
                { Time = DateTime(2024, 10, 16, 14, 0, 0); Duration = None; ClosingDate = None; MaxQuantityPerBooking = None; RemainingCapacity = Some 40 }
                { Time = DateTime(2024, 10, 16, 15, 0, 0); Duration = None; ClosingDate = None; MaxQuantityPerBooking = None; RemainingCapacity = Some 50 }
            |]
            MailSubject = "Anmeldung zum Tag der offenen Tür der HTL Vöcklabruck"
            MailContentTemplate = "{{{FullName}}},\n\nvielen Dank für die Anmeldung zum Tag der offenen Tür der HTL Vöcklabruck am {{{Date}}}.\nAufgrund von COVID-19 bitten wir Sie, pünktlich um {{{Time}}} zu Ihrer persönlichen Führung zu erscheinen.\nBei Änderungswünschen oder im Falle einer Verhinderung bitten wir Sie außerdem, uns sobald wie möglich Bescheid zu geben.\nAntworten Sie dafür auf diese E-Mail bzw. kontaktieren Sie uns telefonisch unter 07672/24605.\n\nWir freuen uns, Sie bei uns begrüßen zu dürfen."
        }
        {
            Key = "lets-code-2425"
            Title = "Let's code 2024/25"
            InfoText = "Tauche ein in die faszinierende Welt der **Bits und Bytes**!\n\nProgrammieren ist der **Schlüssel zur Zukunft** – und jetzt hast du die Chance, den ersten Schritt in diese spannende Welt zu machen. In unserem Workshop lernst du die Grundlagen des Programmierens und entdeckst, wie Computer denken und arbeiten.\n\nEgal, ob du neugierig auf die Technologie hinter Apps, Webseiten oder Spielen bist – hier erfährst du, wie du deine eigenen digitalen Ideen umsetzen kannst.\n\nKomm vorbei und entdecke, wie viel Spaß es macht, die digitale Welt aktiv mitzugestalten!"
            ReservationStartTime = DateTime(2024, 12, 1, 8, 0, 0)
            Slots = [|
                { Time = DateTime(2024, 12, 15, 13, 0, 0); Duration = None; ClosingDate = Some(DateTime(2024, 12, 9, 0, 0, 0)); MaxQuantityPerBooking = Some 1; RemainingCapacity = Some 15 }
                { Time = DateTime(2025, 1, 16, 8, 0, 0); Duration = None; ClosingDate = Some(DateTime(2025, 1, 10, 0, 0, 0)); MaxQuantityPerBooking = Some 1; RemainingCapacity = Some 20 }
            |]
            MailSubject = "Anmeldung zum Programmierworkshop \"Let's code\""
            MailContentTemplate = "{{{FullName}}},\n\nvielen Dank für die Anmeldung zum Programmierworkshop \"Let's code\" der HTL Vöcklabruck am {{{Date}}}.\nBei Änderungswünschen oder im Falle einer Verhinderung bitten wir dich, uns sobald wie möglich Bescheid zu geben.\Du kannst dafür auf diese E-Mail antworten oder uns unter 07672/24605 anrufen.\n\nWir freuen uns auf dich."
        }
    ]
    |> List.map (fun v -> v.Key, v)
    |> Map.ofList

let eventRegistrations = Map.empty
