# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file
COPY src/LKvitai.MES.sln .
COPY Directory.Build.props .
COPY Directory.Packages.props .

# Copy all project files for restore (optimize layer caching)
COPY src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/LKvitai.MES.Modules.Warehouse.Api.csproj Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/
COPY src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/LKvitai.MES.Modules.Warehouse.Application.csproj Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/
COPY src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/LKvitai.MES.Modules.Warehouse.Infrastructure.csproj Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/
COPY src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Projections/LKvitai.MES.Modules.Warehouse.Projections.csproj Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Projections/
COPY src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Sagas/LKvitai.MES.Modules.Warehouse.Sagas.csproj Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Sagas/
COPY src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Integration/LKvitai.MES.Modules.Warehouse.Integration.csproj Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Integration/
COPY src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Domain/LKvitai.MES.Modules.Warehouse.Domain.csproj Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Domain/
COPY src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Contracts/LKvitai.MES.Modules.Warehouse.Contracts.csproj Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Contracts/
COPY src/LKvitai.MES.SharedKernel/LKvitai.MES.SharedKernel.csproj LKvitai.MES.SharedKernel/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/

# Restore dependencies
RUN dotnet restore Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/LKvitai.MES.Modules.Warehouse.Api.csproj

# Copy source code
COPY src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/ Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/
COPY src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/ Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/
COPY src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/ Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/
COPY src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Projections/ Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Projections/
COPY src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Sagas/ Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Sagas/
COPY src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Integration/ Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Integration/
COPY src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Domain/ Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Domain/
COPY src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Contracts/ Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Contracts/
COPY src/LKvitai.MES.SharedKernel/ LKvitai.MES.SharedKernel/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/ BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/

# Build and publish
WORKDIR /src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api
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

ENTRYPOINT ["dotnet", "LKvitai.MES.Modules.Warehouse.Api.dll"]
