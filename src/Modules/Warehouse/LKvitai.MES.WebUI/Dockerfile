# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file
COPY src/LKvitai.MES.sln .

# Copy all project files for restore (optimize layer caching)
COPY src/LKvitai.MES.WebUI/LKvitai.MES.WebUI.csproj LKvitai.MES.WebUI/

# Restore dependencies
RUN dotnet restore LKvitai.MES.WebUI/LKvitai.MES.WebUI.csproj

# Copy source code
COPY src/LKvitai.MES.WebUI/ LKvitai.MES.WebUI/

# Build and publish
WORKDIR /src/LKvitai.MES.WebUI
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

ENTRYPOINT ["dotnet", "LKvitai.MES.WebUI.dll"]
