INSERT INTO event (
        key,
        title,
        info_text,
        reservation_start_time,
        registration_confirmation_mail_subject,
        registration_confirmation_mail_content_template,
        can_request_if_fully_booked
    )
    VALUES (
        'schulfuehrungen-2025',
        'Schulführungen 2025',
        '**Besondere Umstände erfordern besondere Maßnahmen**. Bitte reservieren Sie für den diesjährigen Tag der offenen Tür einen freien Termin.',
        '2024-09-29 00:00:00',
        'Anmeldung zum Tag der offenen Tür der HTL Vöcklabruck',
        '{{{FullName}}},

vielen Dank für die Anmeldung zum Tag der offenen Tür der HTL Vöcklabruck am {{{Date}}}.
Aufgrund von COVID-19 bitten wir Sie, pünktlich um {{{Time}}} zu Ihrer persönlichen Führung zu erscheinen.
Bei Änderungswünschen oder im Falle einer Verhinderung bitten wir Sie außerdem, uns sobald wie möglich Bescheid zu geben.
Antworten Sie dafür auf diese E-Mail bzw. kontaktieren Sie uns telefonisch unter 07672/24605.

Wir freuen uns, Sie bei uns begrüßen zu dürfen.',
        FALSE
    );
INSERT INTO event_slot (event_key, time, max_quantity_per_booking, remaining_capacity) VALUES
    ('schulfuehrungen-2025', '2025-01-28 14:00:00', NULL, 15),
    ('schulfuehrungen-2025', '2025-01-28 15:00:00', 10, 15),
    ('schulfuehrungen-2025', '2025-01-28 16:00:00', NULL, NULL),
    ('schulfuehrungen-2025', '2025-02-03 14:00:00', 25, 15),
    ('schulfuehrungen-2025', '2025-02-03 15:00:00', 15, 15),
    ('schulfuehrungen-2025', '2025-02-03 16:00:00', 15, 15);

INSERT INTO event (
        key,
        title,
        info_text,
        reservation_start_time,
        registration_confirmation_mail_subject,
        registration_confirmation_mail_content_template,
        request_confirmation_mail_subject,
        request_confirmation_mail_content_template,
        can_request_if_fully_booked
    )
    VALUES (
        'lets-code-2425',
        'Let''s code 2024/25',
        'Tauche ein in die faszinierende Welt der Bits und Bytes!

In unserem Workshop lernst du die Grundlagen des Programmierens und entdeckst, wie Computer denken und arbeiten.

Egal, ob du neugierig auf die Technologie hinter Apps, Websites oder Spielen bist – hier erfährst du, wie die digitale Welt funktioniert.

Komm vorbei und sieh selber, wie viel Spaß es macht, eigene Computerprogramme zu entwickeln!',
        '2024-09-29 00:00:00',
        'Anmeldung zum Programmierworkshop "Let''s code"',
        '{{{FullName}}},

vielen Dank für die Anmeldung zum Programmierworkshop "Let''s code" der HTL Vöcklabruck am {{{Date}}} um {{{Time}}}.
Bei Änderungswünschen oder im Falle einer Verhinderung bitten wir dich, uns sobald wie möglich Bescheid zu geben.
Du kannst dafür auf diese E-Mail antworten oder uns unter 07672/24605 anrufen.

Wir freuen uns auf dich.',
        'Anfrage zum Programmierworkshop "Let''s code"',
        '{{{FullName}}},

vielen Dank für dein Interesse am Programmierworkshop "Let''s code" der HTL Vöcklabruck am {{{Date}}} um {{{Time}}}.
Zu dem Zeitpunkt sind leider keine Plätze mehr frei.
Wir melden uns aber bei dir, um gemeinsam eine Lösung zu finden.

Wir freuen uns auf dich.',
        TRUE
    );
INSERT INTO event_slot (event_key, time, duration, max_quantity_per_booking, remaining_capacity) VALUES
    ('lets-code-2425', '2024-12-17 14:00:00', '2 hours 30 minutes', 1, 20),
    ('lets-code-2425', '2025-01-11 08:00:00', '2 hours 15 minutes', 1, 20);
