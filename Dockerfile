# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG APP_PROJECT

WORKDIR /src

COPY . .

RUN dotnet restore "${APP_PROJECT}"
RUN dotnet publish "${APP_PROJECT}" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime

WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet"]
