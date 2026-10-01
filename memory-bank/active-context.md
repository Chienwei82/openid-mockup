# Active Context — OidcMock

> Esta es la nota de trabajo de la sesión. **Actualízala al empezar y al terminar cada tarea.**

## Foco actual

**Tarea:** endurecer la capa de datos JSON tras la revisión de la etapa 1 (T-01 a T-04).
El memory bank quedó inicializado en este mismo paso.

## Estado

- Build: ✅ verde (0 warnings, 0 errors con `TreatWarningsAsErrors`).
- Tests: ✅ 34/34 (8 unit + 26 integration), ejecutados por proyecto:
  `dotnet test tests/OidcMock.UnitTests/OidcMock.UnitTests.csproj`.
- Árbol de trabajo: limpio en `b343700 "prompt 1"` + los commits de este ciclo.

## Decisiones tomadas en esta sesión

- Se adopta **comparación de contenido** (bytes) en lugar del sello `(LastWriteTimeUtc, Length)`
  para la recarga en caliente: el sello falla cuando el contenido cambia del mismo tamaño en la
  misma granularidad de timestamp, que es justo el caso de un editor de texto.
- El loader pasa a cachear el **dominio** mediante una función de proyección
  (`JsonFileLoader<TFile, TDomain>`), no el DTO.
- Con `reloadOnChange: false` el loader no hace ninguna syscall de disco tras la primera lectura.
- Se elimina `Thread.Sleep(200)` de los tests: con comparación de contenido no hace falta.

## Siguiente paso

1. T-01 → escribir el test rojo de `JsonFileLoader` con recarga desactivada.
2. T-02 → caché del dominio.
3. T-03 → comparación de contenido y limpieza de los `Thread.Sleep`.
4. T-04 → tests de simetría y fail fast del host con `WebApplicationFactory<Program>`.
5. Cerrar con `dotnet build` + tests por proyecto y actualizar `progress.md` y `tasks/pending.md`.

## Cómo continuar otro prompt

1. Leer este archivo y [`progress.md`](progress.md).
2. Revisar el último commit (`git --no-pager log --oneline -5`) para saber dónde quedó el ciclo TDD.
3. Ejecutar los tests **antes** de tocar nada, para no atribuir a un cambio un fallo previo.
4. Al terminar: `docs/decisions.md` + este memory bank al día.
