# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/LKvitai.MES.sln .
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY global.json .

# Two-phase copy mirrors Sales/Frontline Dockerfiles. PortalAuth is needed
# because Portal.Api now wires PortalStructuredBearer for /api/portal/v1
# (status stays AllowAnonymous, modules + news require auth).
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/LKvitai.MES.BuildingBlocks.PortalAuth.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
COPY src/Modules/Portal/LKvitai.MES.Modules.Portal.Api/LKvitai.MES.Modules.Portal.Api.csproj Modules/Portal/LKvitai.MES.Modules.Portal.Api/

RUN dotnet restore Modules/Portal/LKvitai.MES.Modules.Portal.Api/LKvitai.MES.Modules.Portal.Api.csproj

COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/ BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
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

# Build metadata surfaced by GET /api/portal/v1/status. Filled in by the
# CI build (build-and-push.yml) via --build-arg so the dashboard can stop
# hardcoding the version. Locally these stay empty and the API gracefully
# omits the corresponding fields.
ARG APP_VERSION
ARG RELEASE_TAG
ARG GIT_SHA
ARG BUILD_DATE
ARG BRANCH_NAME
ARG PR_NUMBER
ENV APP_VERSION=${APP_VERSION}
ENV RELEASE_TAG=${RELEASE_TAG}
ENV GIT_SHA=${GIT_SHA}
ENV BUILD_DATE=${BUILD_DATE}
ENV BRANCH_NAME=${BRANCH_NAME}
ENV PR_NUMBER=${PR_NUMBER}

ENTRYPOINT ["dotnet", "LKvitai.MES.Modules.Portal.Api.dll"]
