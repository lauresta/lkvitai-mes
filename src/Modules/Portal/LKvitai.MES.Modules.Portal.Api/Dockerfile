# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/LKvitai.MES.sln .
COPY Directory.Build.props .
COPY Directory.Packages.props .

COPY src/Modules/Portal/LKvitai.MES.Modules.Portal.Api/LKvitai.MES.Modules.Portal.Api.csproj Modules/Portal/LKvitai.MES.Modules.Portal.Api/

RUN dotnet restore Modules/Portal/LKvitai.MES.Modules.Portal.Api/LKvitai.MES.Modules.Portal.Api.csproj

COPY src/Modules/Portal/LKvitai.MES.Modules.Portal.Api/ Modules/Portal/LKvitai.MES.Modules.Portal.Api/

WORKDIR /src/Modules/Portal/LKvitai.MES.Modules.Portal.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd -r appuser \
    && useradd -r -g appuser appuser

COPY --from=build /app/publish .

RUN chown -R appuser:appuser /app
USER appuser

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "LKvitai.MES.Modules.Portal.Api.dll"]
