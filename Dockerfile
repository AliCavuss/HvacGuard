# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached layer) – only project files and central package versions are needed.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/HvacGuard.Domain/HvacGuard.Domain.csproj src/HvacGuard.Domain/
COPY src/HvacGuard.Infrastructure/HvacGuard.Infrastructure.csproj src/HvacGuard.Infrastructure/
COPY src/HvacGuard.Web/HvacGuard.Web.csproj src/HvacGuard.Web/
RUN dotnet restore src/HvacGuard.Web/HvacGuard.Web.csproj

COPY src/ src/
RUN dotnet publish src/HvacGuard.Web/HvacGuard.Web.csproj -c Release -o /app/publish --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "HvacGuard.Web.dll"]
