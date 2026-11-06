FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/LKvitai.MES.BuildingBlocks.ModuleStartup.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/LKvitai.MES.BuildingBlocks.PortalAuth.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
COPY src/Modules/Finance/LKvitai.MES.Modules.Finance.Api/LKvitai.MES.Modules.Finance.Api.csproj Modules/Finance/LKvitai.MES.Modules.Finance.Api/
RUN dotnet restore Modules/Finance/LKvitai.MES.Modules.Finance.Api/LKvitai.MES.Modules.Finance.Api.csproj
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/ BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/ BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
COPY src/Modules/Finance/LKvitai.MES.Modules.Finance.Api/ Modules/Finance/LKvitai.MES.Modules.Finance.Api/
WORKDIR /src/Modules/Finance/LKvitai.MES.Modules.Finance.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
USER app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "LKvitai.MES.Modules.Finance.Api.dll"]
