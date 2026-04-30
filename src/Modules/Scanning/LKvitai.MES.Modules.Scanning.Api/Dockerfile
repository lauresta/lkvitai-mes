# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/LKvitai.MES.sln .
COPY Directory.Build.props .
COPY Directory.Packages.props .

COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/LKvitai.MES.BuildingBlocks.SharedKernel.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/
COPY src/Modules/Scanning/LKvitai.MES.Modules.Scanning.Contracts/LKvitai.MES.Modules.Scanning.Contracts.csproj Modules/Scanning/LKvitai.MES.Modules.Scanning.Contracts/
COPY src/Modules/Scanning/LKvitai.MES.Modules.Scanning.Application/LKvitai.MES.Modules.Scanning.Application.csproj Modules/Scanning/LKvitai.MES.Modules.Scanning.Application/
COPY src/Modules/Scanning/LKvitai.MES.Modules.Scanning.Api/LKvitai.MES.Modules.Scanning.Api.csproj Modules/Scanning/LKvitai.MES.Modules.Scanning.Api/

RUN dotnet restore Modules/Scanning/LKvitai.MES.Modules.Scanning.Api/LKvitai.MES.Modules.Scanning.Api.csproj

COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/ BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/ BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/
COPY src/Modules/Scanning/LKvitai.MES.Modules.Scanning.Contracts/ Modules/Scanning/LKvitai.MES.Modules.Scanning.Contracts/
COPY src/Modules/Scanning/LKvitai.MES.Modules.Scanning.Application/ Modules/Scanning/LKvitai.MES.Modules.Scanning.Application/
COPY src/Modules/Scanning/LKvitai.MES.Modules.Scanning.Api/ Modules/Scanning/LKvitai.MES.Modules.Scanning.Api/

WORKDIR /src/Modules/Scanning/LKvitai.MES.Modules.Scanning.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
USER app

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "LKvitai.MES.Modules.Scanning.Api.dll"]
