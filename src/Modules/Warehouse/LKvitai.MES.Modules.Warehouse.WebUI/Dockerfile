# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file
COPY src/LKvitai.MES.sln .
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY global.json .

# Copy referenced BuildingBlock + all project files for restore (optimize layer caching)
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/ BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
COPY src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/LKvitai.MES.Modules.Warehouse.WebUI.csproj Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/

# Restore dependencies
RUN dotnet restore Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/LKvitai.MES.Modules.Warehouse.WebUI.csproj

# Copy source code
COPY src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/ Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/

# Build and publish
WORKDIR /src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

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

ENTRYPOINT ["dotnet", "LKvitai.MES.Modules.Warehouse.WebUI.dll"]
