wt `
    new-tab --startingDirectory . `-`- docker compose up`; `
    new-tab --startingDirectory .\src\user-app-server `-`- dotnet watch run --environment Development --urls "http://localhost:5050" `; `
    new-tab --startingDirectory .\src\user-app-client `-`- npm.cmd install `&`& npm.cmd run dev