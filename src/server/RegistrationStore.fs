namespace HTLVB.RegistrationForm.Server

open Dapper
open System
open Npgsql

type ConnectionString = PgsqlRegistrationStoreConnectionString of string

type Schedule = {
    time: DateTime
    quantity: int
    name: string
    mail_address: string
    phone_number: string
    time_stamp: DateTime
}

type IRegistrationStore =
    abstract member GetSchedule: unit -> Async<Schedule list>
    abstract member Book: maxQuantity: int -> data: Schedule -> Async<int>

type PgsqlRegistrationStore(connectionString: ConnectionString) =
    let dataSource =
        let (PgsqlRegistrationStoreConnectionString connectionString) = connectionString
        NpgsqlDataSource.Create(connectionString)

    interface IDisposable with
        member _.Dispose () = dataSource.Dispose()
    interface IAsyncDisposable with
        member _.DisposeAsync () = dataSource.DisposeAsync()

    interface IRegistrationStore with
        member _.GetSchedule () = async {
            use! connection = dataSource.OpenConnectionAsync().AsTask() |> Async.AwaitTask
            let! result = connection.QueryAsync<Schedule>("SELECT time, quantity, name, mail_address, phone_number, time_stamp FROM schedule") |> Async.AwaitTask
            return Seq.toList result
        }
        member _.Book maxQuantity data = async {
            use! connection = dataSource.OpenConnectionAsync().AsTask() |> Async.AwaitTask
            use! tx = connection.BeginTransactionAsync(Data.IsolationLevel.Serializable).AsTask() |> Async.AwaitTask
            let! sumQuantity = connection.QuerySingleAsync<int>("SELECT COALESCE(SUM(quantity), 0) FROM schedule WHERE time = @Time", {| Time = data.time |}) |> Async.AwaitTask
            let reservationsLeft = maxQuantity - (sumQuantity + data.quantity)
            if reservationsLeft < 0 then
                failwithf "Can't save booking because max quantity would be exceeded (%d + %d > %d)" sumQuantity data.quantity maxQuantity
            do! connection.ExecuteAsync("INSERT INTO schedule (time, quantity, name, mail_address, phone_number, time_stamp) VALUES (@time, @quantity, @name, @mail_address, @phone_number, @time_stamp)", data) |> Async.AwaitTask |> Async.Ignore
            do! tx.CommitAsync() |> Async.AwaitTask
            return reservationsLeft
        }
