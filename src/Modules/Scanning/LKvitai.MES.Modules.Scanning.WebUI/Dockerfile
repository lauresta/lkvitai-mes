# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/LKvitai.MES.sln .
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY global.json .

COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/LKvitai.MES.BuildingBlocks.ModuleStartup.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/
COPY src/Modules/Scanning/LKvitai.MES.Modules.Scanning.Contracts/LKvitai.MES.Modules.Scanning.Contracts.csproj Modules/Scanning/LKvitai.MES.Modules.Scanning.Contracts/
COPY src/Modules/Scanning/LKvitai.MES.Modules.Scanning.WebUI/LKvitai.MES.Modules.Scanning.WebUI.csproj Modules/Scanning/LKvitai.MES.Modules.Scanning.WebUI/

RUN dotnet restore Modules/Scanning/LKvitai.MES.Modules.Scanning.WebUI/LKvitai.MES.Modules.Scanning.WebUI.csproj

COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/ BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/
COPY src/Modules/Scanning/LKvitai.MES.Modules.Scanning.Contracts/ Modules/Scanning/LKvitai.MES.Modules.Scanning.Contracts/
COPY src/Modules/Scanning/LKvitai.MES.Modules.Scanning.WebUI/ Modules/Scanning/LKvitai.MES.Modules.Scanning.WebUI/

WORKDIR /src/Modules/Scanning/LKvitai.MES.Modules.Scanning.WebUI
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

ENTRYPOINT ["dotnet", "LKvitai.MES.Modules.Scanning.WebUI.dll"]
