FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-server
WORKDIR /src
COPY ["./src/admin-app-server/HTLVB.RegistrationForm.Admin.Server.fsproj", "./server/"]
RUN dotnet restore server
COPY ./src/admin-app-server ./server
WORKDIR /src/server
RUN dotnet build -c Release -o /app/build

FROM build-server AS publish-server
RUN dotnet publish -c Release -o /app/publish

FROM node:24 AS build-client
WORKDIR /src
COPY src/admin-app-client/package.json src/admin-app-client/package-lock.json ./
RUN npm ci
COPY src/admin-app-client .
RUN npm run build

FROM base AS final
WORKDIR /app
COPY --from=publish-server /app/publish .
COPY --from=build-client /src/dist ./wwwroot
ENTRYPOINT ["dotnet", "HTLVB.RegistrationForm.Admin.Server.dll"]
