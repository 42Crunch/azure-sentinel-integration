#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:3.1-alpine AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:3.1-alpine AS build
WORKDIR /src
COPY ["42c-fw-logs-to-log-analytics.csproj", "."]
RUN dotnet restore "./42c-fw-logs-to-log-analytics.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "42c-fw-logs-to-log-analytics.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "42c-fw-logs-to-log-analytics.csproj" -c Release -o /app/publish

RUN mkdir --parents /opt/guardian/logs

FROM base AS final
WORKDIR /app
RUN mkdir .state
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "42c-fw-logs-to-log-analytics.dll"]
