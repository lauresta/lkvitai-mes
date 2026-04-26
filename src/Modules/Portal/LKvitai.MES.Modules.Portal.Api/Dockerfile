# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/LKvitai.MES.sln .
COPY Directory.Build.props .
COPY Directory.Packages.props .

COPY src/Modules/Portal/LKvitai.MES.Modules.Portal.Api/LKvitai.MES.Modules.Portal.Api.csproj Modules/Portal/LKvitai.MES.Modules.Portal.Api/

RUN dotnet restore Modules/Portal/LKvitai.MES.Modules.Portal.Api/LKvitai.MES.Modules.Portal.Api.csproj

COPY src/Modules/Portal/LKvitai.MES.Modules.Portal.Api/ Modules/Portal/LKvitai.MES.Modules.Portal.Api/

WORKDIR /src/Modules/Portal/LKvitai.MES.Modules.Portal.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl is needed for the docker-compose healthcheck.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Use the base image's built-in non-root `app` user (uid 1654, gid 1654).
# Standard .NET 8 convention. Published binaries deliberately stay
# root-owned (read+exec for `app`, no write) — Sonar docker:S6504. Any
# writable directory the runtime needs (auth-keys, logs, …) is provided
# via host bind-mounts whose ownership is set in deploy-test.yml.
COPY --from=build /app/publish .
USER app

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "LKvitai.MES.Modules.Portal.Api.dll"]
