#!/usr/bin/env bash
# Compila y ejecuta las tres suites, que es la regla 8 de AGENTS.md en un solo comando.
#
# Se compila en Release y no en Debug a proposito: los analizadores y la optimizacion se portan de
# forma distinta, y Release es la configuracion que se publica. TreatWarningsAsErrors ya viene de
# Directory.Build.props, asi que un aviso nuevo rompe el script.
#
# Las pruebas van con --no-build porque el runner de xunit.v3 no es VSTest y no encaja con
# `dotnet test` en varios proyectos a la vez.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${1:-Release}"

dotnet build "$root/OidcMock.slnx" --configuration "$configuration" --nologo

for project in OidcMock.UnitTests OidcMock.IntegrationTests OidcMock.ClientCompatibilityTests; do
    dotnet run --project "$root/tests/$project" \
        --configuration "$configuration" \
        --no-build
done
