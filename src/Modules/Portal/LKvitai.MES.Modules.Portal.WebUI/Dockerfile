# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/LKvitai.MES.sln .
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY global.json .

# Two-phase copy mirrors Sales/Frontline Dockerfiles:
#   1. csproj-only copy → dotnet restore → docker layer cached when only
#      source (not project structure) changes.
#   2. full source copy → dotnet publish --no-restore.
# BuildingBlocks.WebUI is needed because Pages/Home.razor + Shared/MainLayout.razor
# render PortalModuleShell from that project (the static index.html no longer exists).
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/LKvitai.MES.BuildingBlocks.PortalAuth.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/LKvitai.MES.BuildingBlocks.WebUI.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/
COPY src/Modules/Portal/LKvitai.MES.Modules.Portal.WebUI/LKvitai.MES.Modules.Portal.WebUI.csproj Modules/Portal/LKvitai.MES.Modules.Portal.WebUI/

RUN dotnet restore Modules/Portal/LKvitai.MES.Modules.Portal.WebUI/LKvitai.MES.Modules.Portal.WebUI.csproj

COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/ BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/ BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/
COPY src/Modules/Portal/LKvitai.MES.Modules.Portal.WebUI/ Modules/Portal/LKvitai.MES.Modules.Portal.WebUI/

WORKDIR /src/Modules/Portal/LKvitai.MES.Modules.Portal.WebUI
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Use the base image's built-in non-root `app` user (uid 1654, gid 1654).
# This is the standard .NET 8 convention; the host bind-mounts for
# /app/auth-keys and /app/logs must be chowned to 1654:1654 so
# DataProtection can persist its key ring and Serilog can write log
# files (see deploy-test.yml). Published binaries deliberately stay
# root-owned (read-only for the runtime user, Sonar docker:S6504).
COPY --from=build /app/publish .
USER app

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

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

ENTRYPOINT ["dotnet", "LKvitai.MES.Modules.Portal.WebUI.dll"]
