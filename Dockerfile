FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["global.json", "./"]
COPY ["Company.Product.Template.sln", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["nuget.config", "./"]

COPY ["src/Company.Product.Template.Api/Company.Product.Template.Api.csproj", "src/Company.Product.Template.Api/"]
COPY ["src/Company.Product.Template.Application/Company.Product.Template.Application.csproj", "src/Company.Product.Template.Application/"]
COPY ["src/Company.Product.Template.Domain/Company.Product.Template.Domain.csproj", "src/Company.Product.Template.Domain/"]
COPY ["src/Company.Product.Template.Infrastructure/Company.Product.Template.Infrastructure.csproj", "src/Company.Product.Template.Infrastructure/"]
COPY ["src/Company.Product.Template.Shared/Company.Product.Template.Shared.csproj", "src/Company.Product.Template.Shared/"]

COPY ["tests/Company.Product.Template.UnitTests/Company.Product.Template.UnitTests.csproj", "tests/Company.Product.Template.UnitTests/"]
COPY ["tests/Company.Product.Template.IntegrationTests/Company.Product.Template.IntegrationTests.csproj", "tests/Company.Product.Template.IntegrationTests/"]
COPY ["tests/Company.Product.Template.ArchitectureTests/Company.Product.Template.ArchitectureTests.csproj", "tests/Company.Product.Template.ArchitectureTests/"]

RUN dotnet restore "Company.Product.Template.sln"

COPY . .

WORKDIR "/src/src/Company.Product.Template.Api"
# --no-restore: las dependencias son las que resolvió la capa de restore, con nuget.config
# y los manifiestos; el resto del contexto, copiado después, no puede cambiarlas.
RUN dotnet publish "Company.Product.Template.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
# La imagen es lo que se despliega: la documentación queda apagada salvo que el entorno la
# pida (SWAGGER_ENABLED=true, como hace el .env de ejemplo para desarrollo).
ENV SWAGGER_ENABLED=false
EXPOSE 8080

COPY --from=build /app/publish .

# La imagen base define el usuario `app` (uid 1654) y expone su uid en APP_UID. Se usa
# el número, no el nombre: es lo que un orquestador comprueba con `runAsNonRoot`.
USER $APP_UID

# Sonda para Docker/Compose/ECS (Kubernetes la ignora: usa sus propias probes contra
# /health/live y /health/ready). La imagen no trae curl ni wget y no conviene instalarlos
# solo para esto, así que la petición se hace con bash y /dev/tcp. Si se cambia el puerto
# de ASPNETCORE_URLS hay que cambiarlo también aquí.
HEALTHCHECK --interval=30s --timeout=3s --start-period=15s --retries=3 \
  CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /health/live HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n' >&3 && head -n 1 <&3 | grep -q ' 200 '"]

ENTRYPOINT ["dotnet", "Company.Product.Template.Api.dll"]