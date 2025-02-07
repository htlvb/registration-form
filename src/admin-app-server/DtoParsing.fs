namespace HTLVB.RegistrationForm.Admin.Server.DtoParsing

open System
open System.Globalization

module DateTime =
    let tryParse (text: string) =
        match DateTime.TryParseExact(text, "yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.None) with
        | (true, timestamp) -> Some timestamp
        | (false, _) -> None
