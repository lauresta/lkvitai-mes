# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/LKvitai.MES.sln .
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY global.json .

COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/LKvitai.MES.BuildingBlocks.PortalAuth.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
COPY src/Modules/Sales/LKvitai.MES.Modules.Sales.Contracts/LKvitai.MES.Modules.Sales.Contracts.csproj Modules/Sales/LKvitai.MES.Modules.Sales.Contracts/
COPY src/Modules/Sales/LKvitai.MES.Modules.Sales.WebUI/LKvitai.MES.Modules.Sales.WebUI.csproj Modules/Sales/LKvitai.MES.Modules.Sales.WebUI/

RUN dotnet restore Modules/Sales/LKvitai.MES.Modules.Sales.WebUI/LKvitai.MES.Modules.Sales.WebUI.csproj

COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/ BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
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

ENTRYPOINT ["dotnet", "LKvitai.MES.Modules.Sales.WebUI.dll"]
