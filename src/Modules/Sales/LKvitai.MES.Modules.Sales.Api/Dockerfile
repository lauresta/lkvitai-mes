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
COPY src/Modules/Sales/LKvitai.MES.Modules.Sales.Contracts/LKvitai.MES.Modules.Sales.Contracts.csproj Modules/Sales/LKvitai.MES.Modules.Sales.Contracts/
COPY src/Modules/Sales/LKvitai.MES.Modules.Sales.Application/LKvitai.MES.Modules.Sales.Application.csproj Modules/Sales/LKvitai.MES.Modules.Sales.Application/
COPY src/Modules/Sales/LKvitai.MES.Modules.Sales.Infrastructure/LKvitai.MES.Modules.Sales.Infrastructure.csproj Modules/Sales/LKvitai.MES.Modules.Sales.Infrastructure/
COPY src/Modules/Sales/LKvitai.MES.Modules.Sales.Api/LKvitai.MES.Modules.Sales.Api.csproj Modules/Sales/LKvitai.MES.Modules.Sales.Api/

RUN dotnet restore Modules/Sales/LKvitai.MES.Modules.Sales.Api/LKvitai.MES.Modules.Sales.Api.csproj

COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/ BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/ BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/ BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/ BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/
COPY src/Modules/Sales/LKvitai.MES.Modules.Sales.Contracts/ Modules/Sales/LKvitai.MES.Modules.Sales.Contracts/
COPY src/Modules/Sales/LKvitai.MES.Modules.Sales.Application/ Modules/Sales/LKvitai.MES.Modules.Sales.Application/
COPY src/Modules/Sales/LKvitai.MES.Modules.Sales.Infrastructure/ Modules/Sales/LKvitai.MES.Modules.Sales.Infrastructure/
COPY src/Modules/Sales/LKvitai.MES.Modules.Sales.Api/ Modules/Sales/LKvitai.MES.Modules.Sales.Api/

WORKDIR /src/Modules/Sales/LKvitai.MES.Modules.Sales.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl is needed for the docker-compose healthcheck.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Legacy LKvitaiDb (SQL Server 2008/2012) compatibility:
# the .NET 8 base image (Debian 12) ships OpenSSL 3 with SECLEVEL=2 and
# MinProtocol=TLSv1.2. The legacy SQL Server only offers TLS 1.0 with
# SHA1-signed self-signed certs during the TDS pre-login handshake (which
# is mandatory regardless of Encrypt=False), so the connection dies with
# "SSL Provider, error: 31 - Encryption(ssl/tls) handshake failed" before
# any SQL is sent. Lower SECLEVEL and MinProtocol so the handshake can
# complete; the actual data exchange is still unencrypted because the
# connection string sets Encrypt=False;TrustServerCertificate=True.
RUN sed -i 's|^\[openssl_init\]|[openssl_init]\nssl_conf = ssl_sect|' /etc/ssl/openssl.cnf \
    && printf '\n[ssl_sect]\nsystem_default = system_default_sect\n\n[system_default_sect]\nMinProtocol = TLSv1\nCipherString = DEFAULT:@SECLEVEL=0\n' >> /etc/ssl/openssl.cnf

# Standard .NET 8 non-root app user (uid 1654). Same pattern as Warehouse + Portal.
COPY --from=build /app/publish .
USER app

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "LKvitai.MES.Modules.Sales.Api.dll"]
