# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/LKvitai.MES.sln .
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY global.json .

COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/LKvitai.MES.BuildingBlocks.ModuleStartup.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/
# #92 — Frontline.WebUI now wires AddPortalCookieAuthentication so the
# user-block surfaces the real signed-in admin name. PortalAuth has to be
# in the Docker context for both restore and publish.
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/LKvitai.MES.BuildingBlocks.PortalAuth.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/LKvitai.MES.BuildingBlocks.WebUI.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/
COPY src/Modules/Frontline/LKvitai.MES.Modules.Frontline.Contracts/LKvitai.MES.Modules.Frontline.Contracts.csproj Modules/Frontline/LKvitai.MES.Modules.Frontline.Contracts/
COPY src/Modules/Frontline/LKvitai.MES.Modules.Frontline.WebUI/LKvitai.MES.Modules.Frontline.WebUI.csproj Modules/Frontline/LKvitai.MES.Modules.Frontline.WebUI/

RUN dotnet restore Modules/Frontline/LKvitai.MES.Modules.Frontline.WebUI/LKvitai.MES.Modules.Frontline.WebUI.csproj

COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/ BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/ BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/ BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/
COPY src/Modules/Frontline/LKvitai.MES.Modules.Frontline.Contracts/ Modules/Frontline/LKvitai.MES.Modules.Frontline.Contracts/
COPY src/Modules/Frontline/LKvitai.MES.Modules.Frontline.WebUI/ Modules/Frontline/LKvitai.MES.Modules.Frontline.WebUI/

WORKDIR /src/Modules/Frontline/LKvitai.MES.Modules.Frontline.WebUI
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
USER app

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Build metadata surfaced by IBuildVersionAccessor → MainLayout → the shared
# topbar's "VER" badge. Filled in by build-and-push.yml via --build-arg so
# the env-badge stops showing an em-dash in test/prod. Same shape Sales/
# Portal Dockerfiles use; the four ARG names are part of build-and-push.yml's
# matrix-step contract — keep them synchronised across modules.
ARG APP_VERSION
ARG RELEASE_TAG
ARG GIT_SHA
ARG BUILD_DATE
ENV APP_VERSION=${APP_VERSION}
ENV RELEASE_TAG=${RELEASE_TAG}
ENV GIT_SHA=${GIT_SHA}
ENV BUILD_DATE=${BUILD_DATE}

ENTRYPOINT ["dotnet", "LKvitai.MES.Modules.Frontline.WebUI.dll"]
