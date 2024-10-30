-- Initial
CREATE TABLE schedule(
    id SERIAL PRIMARY KEY,
    time TIMESTAMP NOT NULL,
    quantity INTEGER NOT NULL,
    name VARCHAR NOT NULL,
    mail_address VARCHAR NOT NULL,
    phone_number VARCHAR NOT NULL,
    time_stamp TIMESTAMP NOT NULL
);

-- Allow simultaneous events
CREATE TABLE event(
    key VARCHAR PRIMARY KEY,
    title VARCHAR NOT NULL,
    info_text VARCHAR NOT NULL,
    reservation_start_time TIMESTAMP NOT NULL,
    mail_subject VARCHAR NOT NULL,
    mail_content_template VARCHAR NOT NULL
);
CREATE TABLE event_slot(
    event_key VARCHAR NOT NULL REFERENCES event(key) ON UPDATE CASCADE,
    time TIMESTAMP NOT NULL,
    max_quantity_per_booking INT,
    remaining_capacity INT,
    PRIMARY KEY(event_key, time)
);

ALTER TABLE schedule RENAME TO event_registration;
ALTER TABLE event_registration ADD COLUMN event_key VARCHAR NOT NULL;
ALTER TABLE event_registration ADD FOREIGN KEY (event_key) REFERENCES event(key);

-- Add closing date
ALTER TABLE event_slot ADD COLUMN closing_date TIMESTAMP;

-- Add optional slot duration
ALTER TABLE event_slot ADD COLUMN duration INTERVAL;

-- Clean up
/*
DROP TABLE event_registration;
DROP TABLE event_slot;
DROP TABLE event;
*/
