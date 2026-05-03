# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/LKvitai.MES.sln .
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY global.json .

COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/LKvitai.MES.BuildingBlocks.SharedKernel.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/LKvitai.MES.BuildingBlocks.ModuleStartup.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/
COPY src/Modules/Frontline/LKvitai.MES.Modules.Frontline.Contracts/LKvitai.MES.Modules.Frontline.Contracts.csproj Modules/Frontline/LKvitai.MES.Modules.Frontline.Contracts/
COPY src/Modules/Frontline/LKvitai.MES.Modules.Frontline.Application/LKvitai.MES.Modules.Frontline.Application.csproj Modules/Frontline/LKvitai.MES.Modules.Frontline.Application/
COPY src/Modules/Frontline/LKvitai.MES.Modules.Frontline.Infrastructure/LKvitai.MES.Modules.Frontline.Infrastructure.csproj Modules/Frontline/LKvitai.MES.Modules.Frontline.Infrastructure/
COPY src/Modules/Frontline/LKvitai.MES.Modules.Frontline.Api/LKvitai.MES.Modules.Frontline.Api.csproj Modules/Frontline/LKvitai.MES.Modules.Frontline.Api/

RUN dotnet restore Modules/Frontline/LKvitai.MES.Modules.Frontline.Api/LKvitai.MES.Modules.Frontline.Api.csproj

COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/ BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/ BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/
COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/ BuildingBlocks/LKvitai.MES.BuildingBlocks.ModuleStartup/
COPY src/Modules/Frontline/LKvitai.MES.Modules.Frontline.Contracts/ Modules/Frontline/LKvitai.MES.Modules.Frontline.Contracts/
COPY src/Modules/Frontline/LKvitai.MES.Modules.Frontline.Application/ Modules/Frontline/LKvitai.MES.Modules.Frontline.Application/
COPY src/Modules/Frontline/LKvitai.MES.Modules.Frontline.Infrastructure/ Modules/Frontline/LKvitai.MES.Modules.Frontline.Infrastructure/
COPY src/Modules/Frontline/LKvitai.MES.Modules.Frontline.Api/ Modules/Frontline/LKvitai.MES.Modules.Frontline.Api/

WORKDIR /src/Modules/Frontline/LKvitai.MES.Modules.Frontline.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl is needed for the docker-compose healthcheck.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Legacy LKvitaiDb (SQL Server 2008/2012) compatibility — same hack as
# Sales.Api/Dockerfile. The .NET 8 base image (Debian 12) ships OpenSSL 3
# with SECLEVEL=2 and MinProtocol=TLSv1.2. The legacy SQL Server only offers
# TLS 1.0 with SHA1-signed self-signed certs during the TDS pre-login
# handshake (which is mandatory regardless of Encrypt=False), so the
# connection dies with "SSL Provider, error: 31 - Encryption(ssl/tls)
# handshake failed" before any SQL is sent. Lower SECLEVEL and MinProtocol
# so the handshake can complete; the actual data exchange is still
# unencrypted because the connection string sets
# Encrypt=False;TrustServerCertificate=True.
RUN sed -i 's|^\[openssl_init\]|[openssl_init]\nssl_conf = ssl_sect|' /etc/ssl/openssl.cnf \
    && printf '\n[ssl_sect]\nsystem_default = system_default_sect\n\n[system_default_sect]\nMinProtocol = TLSv1\nCipherString = DEFAULT:@SECLEVEL=0\n' >> /etc/ssl/openssl.cnf

COPY --from=build /app/publish .
USER app

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "LKvitai.MES.Modules.Frontline.Api.dll"]
