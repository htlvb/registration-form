-- Initial
DROP TABLE IF EXISTS schedule;
CREATE TABLE schedule(
    id SERIAL PRIMARY KEY,
    time TIMESTAMP NOT NULL,
    quantity INTEGER NOT NULL,
    name VARCHAR NOT NULL,
    mail_address VARCHAR NOT NULL,
    phone_number VARCHAR NOT NULL,
    time_stamp TIMESTAMP NOT NULL
);
