# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/LKvitai.MES.sln .
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY global.json .

COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/LKvitai.MES.BuildingBlocks.ModuleStartup.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/LKvitai.MES.BuildingBlocks.PortalAuth.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/LKvitai.MES.BuildingBlocks.WebUI.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/
COPY src/Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.WebUI/LKvitai.MES.Modules.Shopfloor.WebUI.csproj Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.WebUI/

RUN dotnet restore Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.WebUI/LKvitai.MES.Modules.Shopfloor.WebUI.csproj

COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/ BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/ BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/ BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/
COPY src/Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.WebUI/ Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.WebUI/

WORKDIR /src/Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.WebUI
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

ARG APP_VERSION
ARG RELEASE_TAG
ARG GIT_SHA
ARG BUILD_DATE
ENV APP_VERSION=${APP_VERSION}
ENV RELEASE_TAG=${RELEASE_TAG}
ENV GIT_SHA=${GIT_SHA}
ENV BUILD_DATE=${BUILD_DATE}

ENTRYPOINT ["dotnet", "LKvitai.MES.Modules.Shopfloor.WebUI.dll"]
