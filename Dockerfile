#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim-amd64 AS build-server
WORKDIR /src
COPY ["./src/server/HTLVB.RegistrationForm.Server.fsproj", "./server/"]
RUN dotnet restore server
COPY ./src/server ./server
WORKDIR /src/server
RUN dotnet build -c Release -o /app/build

FROM build-server AS publish-server
RUN dotnet publish -c Release -o /app/publish

FROM node:20 AS build-client
WORKDIR /src
COPY src/client/package.json src/client/package-lock.json ./
RUN npm ci
COPY src/client .
RUN npm run build

FROM base AS final
WORKDIR /app
COPY --from=publish-server /app/publish .
COPY --from=build-client /src/dist ./wwwroot
ENTRYPOINT ["dotnet", "HTLVB.RegistrationForm.Server.dll"]
