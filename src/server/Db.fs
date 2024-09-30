module HTLVB.RegistrationForm.Server.Db

open Dapper
open System
open Npgsql

type ConnectionString = ConnectionString of string

type Schedule = {
    time: DateTime
    quantity: int
    name: string
    mail_address: string
    phone_number: string
    time_stamp: DateTime
}

let private createConnection (ConnectionString connectionString) = async {
    let dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString)
    let dataSource = dataSourceBuilder.Build()
    return! dataSource.OpenConnectionAsync().AsTask() |> Async.AwaitTask
}

let getSchedule dbConfig = async {
    use! connection = createConnection dbConfig
    let! result = connection.QueryAsync<Schedule>("SELECT time, quantity, name, mail_address, phone_number, time_stamp FROM schedule") |> Async.AwaitTask
    return Seq.toList result
}

let book dbConfig maxQuantity (data: Schedule) = async {
    use! connection = createConnection dbConfig
    use! tx = connection.BeginTransactionAsync(Data.IsolationLevel.Serializable).AsTask() |> Async.AwaitTask
    let! sumQuantity = connection.QuerySingleAsync<int>("SELECT COALESCE(SUM(quantity), 0) FROM schedule WHERE time = @Time", {| Time = data.time |}) |> Async.AwaitTask
    let reservationsLeft = maxQuantity - (sumQuantity + data.quantity)
    if reservationsLeft < 0 then
        failwithf "Can't save booking because max quantity would be exceeded (%d + %d > %d)" sumQuantity data.quantity maxQuantity
    do! connection.ExecuteAsync("INSERT INTO schedule (time, quantity, name, mail_address, phone_number, time_stamp) VALUES (@time, @quantity, @name, @mail_address, @phone_number, @time_stamp)", data) |> Async.AwaitTask |> Async.Ignore
    do! tx.CommitAsync() |> Async.AwaitTask
    return reservationsLeft
}