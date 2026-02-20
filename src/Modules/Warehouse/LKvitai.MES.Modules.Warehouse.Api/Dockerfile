# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file
COPY src/LKvitai.MES.sln .
COPY Directory.Build.props .
COPY Directory.Packages.props .

# Copy all project files for restore (optimize layer caching)
COPY src/Modules/Warehouse/LKvitai.MES.Api/LKvitai.MES.Api.csproj Modules/Warehouse/LKvitai.MES.Api/
COPY src/Modules/Warehouse/LKvitai.MES.Application/LKvitai.MES.Application.csproj Modules/Warehouse/LKvitai.MES.Application/
COPY src/Modules/Warehouse/LKvitai.MES.Infrastructure/LKvitai.MES.Infrastructure.csproj Modules/Warehouse/LKvitai.MES.Infrastructure/
COPY src/Modules/Warehouse/LKvitai.MES.Projections/LKvitai.MES.Projections.csproj Modules/Warehouse/LKvitai.MES.Projections/
COPY src/Modules/Warehouse/LKvitai.MES.Sagas/LKvitai.MES.Sagas.csproj Modules/Warehouse/LKvitai.MES.Sagas/
COPY src/Modules/Warehouse/LKvitai.MES.Integration/LKvitai.MES.Integration.csproj Modules/Warehouse/LKvitai.MES.Integration/
COPY src/Modules/Warehouse/LKvitai.MES.Domain/LKvitai.MES.Domain.csproj Modules/Warehouse/LKvitai.MES.Domain/
COPY src/Modules/Warehouse/LKvitai.MES.Contracts/LKvitai.MES.Contracts.csproj Modules/Warehouse/LKvitai.MES.Contracts/
COPY src/LKvitai.MES.SharedKernel/LKvitai.MES.SharedKernel.csproj LKvitai.MES.SharedKernel/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/

# Restore dependencies
RUN dotnet restore Modules/Warehouse/LKvitai.MES.Api/LKvitai.MES.Api.csproj

# Copy source code
COPY src/Modules/Warehouse/LKvitai.MES.Api/ Modules/Warehouse/LKvitai.MES.Api/
COPY src/Modules/Warehouse/LKvitai.MES.Application/ Modules/Warehouse/LKvitai.MES.Application/
COPY src/Modules/Warehouse/LKvitai.MES.Infrastructure/ Modules/Warehouse/LKvitai.MES.Infrastructure/
COPY src/Modules/Warehouse/LKvitai.MES.Projections/ Modules/Warehouse/LKvitai.MES.Projections/
COPY src/Modules/Warehouse/LKvitai.MES.Sagas/ Modules/Warehouse/LKvitai.MES.Sagas/
COPY src/Modules/Warehouse/LKvitai.MES.Integration/ Modules/Warehouse/LKvitai.MES.Integration/
COPY src/Modules/Warehouse/LKvitai.MES.Domain/ Modules/Warehouse/LKvitai.MES.Domain/
COPY src/Modules/Warehouse/LKvitai.MES.Contracts/ Modules/Warehouse/LKvitai.MES.Contracts/
COPY src/LKvitai.MES.SharedKernel/ LKvitai.MES.SharedKernel/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/ BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/

# Build and publish
WORKDIR /src/Modules/Warehouse/LKvitai.MES.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

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

ENTRYPOINT ["dotnet", "LKvitai.MES.Api.dll"]
