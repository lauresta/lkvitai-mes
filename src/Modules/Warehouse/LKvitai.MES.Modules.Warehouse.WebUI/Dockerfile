# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file
COPY src/LKvitai.MES.sln .
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY global.json .

# Copy all project files for restore (optimize layer caching)
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

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Copy published files
COPY --from=build /app/publish .

# Change ownership to non-root user
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "LKvitai.MES.Modules.Warehouse.WebUI.dll"]
