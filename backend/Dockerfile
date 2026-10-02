# syntax=docker/dockerfile:1

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so the layer is cached when only source code changes.
COPY StudyAbroad.sln ./
COPY src/StudyAbroad.Api/StudyAbroad.Api.csproj src/StudyAbroad.Api/
COPY src/StudyAbroad.Application/StudyAbroad.Application.csproj src/StudyAbroad.Application/
COPY src/StudyAbroad.Domain/StudyAbroad.Domain.csproj src/StudyAbroad.Domain/
COPY src/StudyAbroad.Infrastructure/StudyAbroad.Infrastructure.csproj src/StudyAbroad.Infrastructure/
RUN dotnet restore src/StudyAbroad.Api/StudyAbroad.Api.csproj

COPY src/ src/
RUN dotnet publish src/StudyAbroad.Api/StudyAbroad.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish .
EXPOSE 8080
# The aspnet image ships a non-root "app" user (UID 1654).
USER app
ENTRYPOINT ["dotnet", "StudyAbroad.Api.dll"]
