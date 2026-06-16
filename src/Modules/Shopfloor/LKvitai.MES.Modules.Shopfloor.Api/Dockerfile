# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/LKvitai.MES.sln .
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY global.json .

# Project files first to maximize restore layer caching.
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/LKvitai.MES.BuildingBlocks.SharedKernel.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/LKvitai.MES.BuildingBlocks.ModuleStartup.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/LKvitai.MES.BuildingBlocks.PortalAuth.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
COPY src/Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Contracts/LKvitai.MES.Modules.Shopfloor.Contracts.csproj Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Contracts/
COPY src/Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Domain/LKvitai.MES.Modules.Shopfloor.Domain.csproj Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Domain/
COPY src/Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Application/LKvitai.MES.Modules.Shopfloor.Application.csproj Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Application/
COPY src/Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Infrastructure/LKvitai.MES.Modules.Shopfloor.Infrastructure.csproj Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Infrastructure/
COPY src/Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Api/LKvitai.MES.Modules.Shopfloor.Api.csproj Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Api/

RUN dotnet restore Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Api/LKvitai.MES.Modules.Shopfloor.Api.csproj

COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/ BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/ BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/ BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/ BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
COPY src/Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Contracts/ Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Contracts/
COPY src/Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Domain/ Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Domain/
COPY src/Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Application/ Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Application/
COPY src/Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Infrastructure/ Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Infrastructure/
COPY src/Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Api/ Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Api/

WORKDIR /src/Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl is needed for the docker-compose healthcheck.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Legacy LKvitaiDb (SQL Server 2008/2012) TLS compatibility — see Sales.Api
# Dockerfile for the full rationale. The legacy server only offers TLS 1.0 with
# SHA1-signed certs during the TDS pre-login handshake, so SECLEVEL/MinProtocol
# must be lowered for the handshake to complete.
RUN sed -i 's|^\[openssl_init\]|[openssl_init]\nssl_conf = ssl_sect|' /etc/ssl/openssl.cnf \
    && printf '\n[ssl_sect]\nsystem_default = system_default_sect\n\n[system_default_sect]\nMinProtocol = TLSv1\nCipherString = DEFAULT:@SECLEVEL=0\n' >> /etc/ssl/openssl.cnf

COPY --from=build /app/publish .
USER app

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "LKvitai.MES.Modules.Shopfloor.Api.dll"]
