#!/usr/bin/env bash
# Publica el mock autocontenido y de un solo archivo para los dos SO que se usan en desarrollo.
# No necesita el runtime de .NET en la maquina de destino.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$root/src/OidcMock.Host/OidcMock.Host.csproj"

for runtime in linux-x64 win-x64; do
    output="$root/artifacts/publish/$runtime"
    rm -rf "$output"

    dotnet publish "$project" \
        --configuration Release \
        --runtime "$runtime" \
        --self-contained true \
        -p:PublishSingleFile=true \
        --output "$output"

    echo "publicado $runtime en $output"
done