# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/LKvitai.MES.sln .
COPY Directory.Build.props .
COPY Directory.Packages.props .

# Project files first to maximize restore layer caching.
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/LKvitai.MES.BuildingBlocks.SharedKernel.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/LKvitai.MES.BuildingBlocks.PortalAuth.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
COPY src/Modules/Sales/LKvitai.MES.Modules.Sales.Contracts/LKvitai.MES.Modules.Sales.Contracts.csproj Modules/Sales/LKvitai.MES.Modules.Sales.Contracts/
COPY src/Modules/Sales/LKvitai.MES.Modules.Sales.Application/LKvitai.MES.Modules.Sales.Application.csproj Modules/Sales/LKvitai.MES.Modules.Sales.Application/
COPY src/Modules/Sales/LKvitai.MES.Modules.Sales.Api/LKvitai.MES.Modules.Sales.Api.csproj Modules/Sales/LKvitai.MES.Modules.Sales.Api/

RUN dotnet restore Modules/Sales/LKvitai.MES.Modules.Sales.Api/LKvitai.MES.Modules.Sales.Api.csproj

COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/ BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/ BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/ BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
COPY src/Modules/Sales/LKvitai.MES.Modules.Sales.Contracts/ Modules/Sales/LKvitai.MES.Modules.Sales.Contracts/
COPY src/Modules/Sales/LKvitai.MES.Modules.Sales.Application/ Modules/Sales/LKvitai.MES.Modules.Sales.Application/
COPY src/Modules/Sales/LKvitai.MES.Modules.Sales.Api/ Modules/Sales/LKvitai.MES.Modules.Sales.Api/

WORKDIR /src/Modules/Sales/LKvitai.MES.Modules.Sales.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl is needed for the docker-compose healthcheck.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Standard .NET 8 non-root app user (uid 1654). Same pattern as Warehouse + Portal.
COPY --from=build /app/publish .
USER app

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "LKvitai.MES.Modules.Sales.Api.dll"]
