# Imagen minima para el mock: build con el SDK completo, ejecucion con el runtime slim.
# El contenedor sirve HTTP plano porque el TLS lo termina el proxy que delante lo publique.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /source

# Primero solo los archivos de proyecto: mientras el csproj no cambia, la capa de restore se reusa.
COPY Directory.Build.props Directory.Packages.props OidcMock.slnx ./
COPY src/OidcMock.Core/OidcMock.Core.csproj src/OidcMock.Core/
COPY src/OidcMock.Host/OidcMock.Host.csproj src/OidcMock.Host/
RUN dotnet restore src/OidcMock.Host/OidcMock.Host.csproj \
    --runtime linux-x64 \
    -p:SelfContained=true

COPY config/ config/
COPY src/ src/

RUN dotnet publish src/OidcMock.Host/OidcMock.Host.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:ContinuousIntegrationBuild=true \
    --no-restore \
    --output /published

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final
WORKDIR /app

# El mock escribe config/signing-key.pem en el primer arranque, asi que necesita esa ruta escribible.
COPY --from=build /published ./

# Los JSON de configuracion se copian aparte para que en el contenedor esten en un sitio conocido y
# se puedan montar encima (docker run -v ./config:/app/config). Con single-file acabarian dentro del
# directorio de extraccion, que no es un sitio donde buscar ni sobreescribir configuracion.
COPY --from=build /source/config/ ./config/

ENV ASPNETCORE_ENVIRONMENT=Production \
    OidcMock__ConfigDirectory=/app/config \
    OidcMock__Serving__UseHttps=false \
    OidcMock__Serving__AllowHttp=true \
    OidcMock__Serving__HttpPort=8080

EXPOSE 8080

# El usuario sin privilegios por defecto de la imagen base.
USER $APP_UID

ENTRYPOINT ["./OidcMock.Host"]