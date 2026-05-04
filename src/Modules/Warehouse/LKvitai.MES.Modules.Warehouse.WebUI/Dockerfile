# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file
COPY src/LKvitai.MES.sln .
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY global.json .

# Copy referenced BuildingBlock csproj files for restore (optimize layer caching)
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/LKvitai.MES.BuildingBlocks.PortalAuth.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/LKvitai.MES.BuildingBlocks.WebUI.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/
COPY src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/LKvitai.MES.Modules.Warehouse.WebUI.csproj Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/

# Restore dependencies
RUN dotnet restore Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/LKvitai.MES.Modules.Warehouse.WebUI.csproj

# Copy source code
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/ BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/ BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/
COPY src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/ Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/

# Build and publish
WORKDIR /src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl is needed for the docker-compose healthcheck.
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Use the base image's built-in non-root `app` user (uid 1654, gid 1654).
# This is the standard .NET 8 convention; the host bind-mount for
# /app/auth-keys must be chowned to 1654:1654 so DataProtection can
# persist its key ring (see deploy-test.yml). Published binaries
# deliberately stay root-owned (read-only for the runtime user,
# Sonar docker:S6504).
COPY --from=build /app/publish .
USER app

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Build metadata surfaced by IBuildVersionAccessor → MainLayout → the shared
# topbar's "VER" badge. Filled in by build-and-push.yml via --build-arg so
# the env-badge stops showing an em-dash in test/prod. Same shape as
# Sales/Portal/Frontline WebUI Dockerfiles; keep the four ARGs synchronised
# across modules so build-and-push.yml's matrix step can pass identical
# --build-args to every WebUI image.
ARG APP_VERSION
ARG RELEASE_TAG
ARG GIT_SHA
ARG BUILD_DATE
ENV APP_VERSION=${APP_VERSION}
ENV RELEASE_TAG=${RELEASE_TAG}
ENV GIT_SHA=${GIT_SHA}
ENV BUILD_DATE=${BUILD_DATE}

ENTRYPOINT ["dotnet", "LKvitai.MES.Modules.Warehouse.WebUI.dll"]
