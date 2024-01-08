#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

MAINTAINER Peter Andersson

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER app
WORKDIR /app

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG APP_VERSION=0.0.0
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["GuardRelay/GuardRelay.csproj", "GuardRelay/"]
RUN dotnet restore "./GuardRelay/GuardRelay.csproj"
COPY . .
WORKDIR "/src/GuardRelay"
RUN dotnet build "./GuardRelay.csproj" -c $BUILD_CONFIGURATION -o /app/build /property:Version=${APP_VERSION}

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GuardRelay.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GuardRelay.dll"]