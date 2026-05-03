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
COPY src/Modules/Sales/LKvitai.MES.Modules.Sales.Contracts/LKvitai.MES.Modules.Sales.Contracts.csproj Modules/Sales/LKvitai.MES.Modules.Sales.Contracts/
COPY src/Modules/Sales/LKvitai.MES.Modules.Sales.WebUI/LKvitai.MES.Modules.Sales.WebUI.csproj Modules/Sales/LKvitai.MES.Modules.Sales.WebUI/

RUN dotnet restore Modules/Sales/LKvitai.MES.Modules.Sales.WebUI/LKvitai.MES.Modules.Sales.WebUI.csproj

COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/ BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/ BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/ BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/
COPY src/Modules/Sales/LKvitai.MES.Modules.Sales.Contracts/ Modules/Sales/LKvitai.MES.Modules.Sales.Contracts/
COPY src/Modules/Sales/LKvitai.MES.Modules.Sales.WebUI/ Modules/Sales/LKvitai.MES.Modules.Sales.WebUI/

WORKDIR /src/Modules/Sales/LKvitai.MES.Modules.Sales.WebUI
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
# the env-badge stops showing an em-dash in test/prod. Locally these stay
# empty and the accessor falls back to "—" (or "dev" if APP_VERSION is the
# 0.1.0-dev sentinel). Same shape the Portal-WebUI Dockerfile uses; keep
# the four ARGs synchronised across modules so build-and-push.yml's matrix
# step can pass identical --build-args to every WebUI image.
ARG APP_VERSION
ARG RELEASE_TAG
ARG GIT_SHA
ARG BUILD_DATE
ENV APP_VERSION=${APP_VERSION}
ENV RELEASE_TAG=${RELEASE_TAG}
ENV GIT_SHA=${GIT_SHA}
ENV BUILD_DATE=${BUILD_DATE}

ENTRYPOINT ["dotnet", "LKvitai.MES.Modules.Sales.WebUI.dll"]
