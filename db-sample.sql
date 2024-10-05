INSERT INTO event (key, title, info_text, reservation_start_time, mail_subject, mail_content_template) VALUES
    (
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

Wir freuen uns, Sie bei uns begrüßen zu dürfen.'
    );
INSERT INTO event_slot (event_key, time, max_quantity_per_booking, remaining_capacity) VALUES
    ('schulfuehrungen-2025', '2025-01-28 14:00:00', NULL, 15),
    ('schulfuehrungen-2025', '2025-01-28 15:00:00', 10, 15),
    ('schulfuehrungen-2025', '2025-01-28 16:00:00', NULL, NULL),
    ('schulfuehrungen-2025', '2025-02-03 14:00:00', 25, 15),
    ('schulfuehrungen-2025', '2025-02-03 15:00:00', 15, 15),
    ('schulfuehrungen-2025', '2025-02-03 16:00:00', 15, 15);

INSERT INTO event (key, title, info_text, reservation_start_time, mail_subject, mail_content_template) VALUES
    (
        'lets-code-2425',
        'Let''s code 2024/25',
        'Tauche ein in die faszinierende Welt der **Bits und Bytes**!

Programmieren ist der **Schlüssel zur Zukunft** – und jetzt hast du die Chance, den ersten Schritt in diese spannende Welt zu machen. In unserem Workshop lernst du die Grundlagen des Programmierens und entdeckst, wie Computer denken und arbeiten.

Egal, ob du neugierig auf die Technologie hinter Apps, Webseiten oder Spielen bist – hier erfährst du, wie du deine eigenen digitalen Ideen umsetzen kannst.

Komm vorbei und entdecke, wie viel Spaß es macht, die digitale Welt aktiv mitzugestalten!',
        '2024-09-29 00:00:00',
        'Anmeldung zum Programmierworkshop "Let''s code"',
        '{{{FullName}}},

vielen Dank für die Anmeldung zum Programmierworkshop "Let''s code" der HTL Vöcklabruck am {{{Date}}}.
Bei Änderungswünschen oder im Falle einer Verhinderung bitten wir dich, uns sobald wie möglich Bescheid zu geben.\Du kannst dafür auf diese E-Mail antworten oder uns unter 07672/24605 anrufen.

Wir freuen uns auf dich.'
    );
INSERT INTO event_slot (event_key, time, max_quantity_per_booking, remaining_capacity) VALUES
    ('lets-code-2425', '2024-12-18 13:30:00', 1, 20),
    ('lets-code-2425', '2025-01-11 08:00:00', 1, 20);
