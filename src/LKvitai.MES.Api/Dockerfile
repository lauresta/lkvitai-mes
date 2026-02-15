# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file
COPY src/LKvitai.MES.sln .

# Copy all project files for restore (optimize layer caching)
COPY src/LKvitai.MES.Api/LKvitai.MES.Api.csproj LKvitai.MES.Api/
COPY src/LKvitai.MES.Application/LKvitai.MES.Application.csproj LKvitai.MES.Application/
COPY src/LKvitai.MES.Infrastructure/LKvitai.MES.Infrastructure.csproj LKvitai.MES.Infrastructure/
COPY src/LKvitai.MES.Projections/LKvitai.MES.Projections.csproj LKvitai.MES.Projections/
COPY src/LKvitai.MES.Sagas/LKvitai.MES.Sagas.csproj LKvitai.MES.Sagas/
COPY src/LKvitai.MES.Integration/LKvitai.MES.Integration.csproj LKvitai.MES.Integration/
COPY src/LKvitai.MES.Domain/LKvitai.MES.Domain.csproj LKvitai.MES.Domain/
COPY src/LKvitai.MES.Contracts/LKvitai.MES.Contracts.csproj LKvitai.MES.Contracts/
COPY src/LKvitai.MES.SharedKernel/LKvitai.MES.SharedKernel.csproj LKvitai.MES.SharedKernel/

# Restore dependencies
RUN dotnet restore LKvitai.MES.Api/LKvitai.MES.Api.csproj

# Copy source code
COPY src/LKvitai.MES.Api/ LKvitai.MES.Api/
COPY src/LKvitai.MES.Application/ LKvitai.MES.Application/
COPY src/LKvitai.MES.Infrastructure/ LKvitai.MES.Infrastructure/
COPY src/LKvitai.MES.Projections/ LKvitai.MES.Projections/
COPY src/LKvitai.MES.Sagas/ LKvitai.MES.Sagas/
COPY src/LKvitai.MES.Integration/ LKvitai.MES.Integration/
COPY src/LKvitai.MES.Domain/ LKvitai.MES.Domain/
COPY src/LKvitai.MES.Contracts/ LKvitai.MES.Contracts/
COPY src/LKvitai.MES.SharedKernel/ LKvitai.MES.SharedKernel/

# Build and publish
WORKDIR /src/LKvitai.MES.Api
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
