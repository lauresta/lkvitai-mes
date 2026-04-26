# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/LKvitai.MES.sln .
COPY Directory.Build.props .
COPY Directory.Packages.props .

COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/ BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
COPY src/Modules/Portal/LKvitai.MES.Modules.Portal.WebUI/LKvitai.MES.Modules.Portal.WebUI.csproj Modules/Portal/LKvitai.MES.Modules.Portal.WebUI/

RUN dotnet restore Modules/Portal/LKvitai.MES.Modules.Portal.WebUI/LKvitai.MES.Modules.Portal.WebUI.csproj

COPY src/Modules/Portal/LKvitai.MES.Modules.Portal.WebUI/ Modules/Portal/LKvitai.MES.Modules.Portal.WebUI/

WORKDIR /src/Modules/Portal/LKvitai.MES.Modules.Portal.WebUI
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN groupadd -r appuser && useradd -r -g appuser appuser

COPY --from=build /app/publish .

RUN chown -R appuser:appuser /app
USER appuser

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "LKvitai.MES.Modules.Portal.WebUI.dll"]
