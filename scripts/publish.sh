#!/usr/bin/env bash
# Publica el mock autocontenido y de un solo archivo para los dos SO que se usan en desarrollo.
# No necesita el runtime de .NET en la maquina de destino. La salida va a publish/<runtime>/,
# que esta en .gitignore: son binarios generados y no tienen nada que ver en el historico.
#
# Uso: ./scripts/publish.sh    (sin build ni tests; para eso esta ./scripts/publish.py)
set -euo pipefail

# Igual que en test.sh: sin esto, los nodos de MSBuild quedan vivos y se comen la RAM.
trap '{ dotnet build-server shutdown || true; pkill -f "[n]odemode:1" > /dev/null 2>&1 || true; }' EXIT

readonly root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly project="$root/src/OidcMock.Host/OidcMock.Host.csproj"
readonly runtimes=(linux-x64 win-x64)

for runtime in "${runtimes[@]}"; do
    output="$root/publish/$runtime"
    # Se borra antes: un publish parcial de una version anterior dejaria binarios viejos al lado.
    rm -rf "$output"

    dotnet publish "$project" \
        --configuration Release \
        --runtime "$runtime" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:ContinuousIntegrationBuild=true \
        -p:PathMap="$root=/_/" \
        --output "$output"

    echo "publicado $runtime en $output"
done