# Active Context — OidcMock

> Esta es la nota de trabajo de la sesión. **Actualízala al empezar y al terminar cada tarea.**

## Foco actual

**Tarea:** etapa 1 cerrada. Memory bank inicializado y capa de datos endurecida (T-01 a T-04).
**Siguiente tarea:** T-05, etapa 2 (discovery, JWKS y firma RS256) — ver
[`tasks/pending.md`](tasks/pending.md).

## Estado

- Build: ✅ verde (0 warnings, 0 errors con `TreatWarningsAsErrors`).
- Tests: ✅ **43/43** (8 unit + 35 integration).
- Host: ✅ `dotnet run` levanta en `http://localhost:5000` y valida la configuración al arrancar.
- Último commit de código: `38ac8c2 feat: seam de configuracion OidcMock:ConfigDirectory y ReloadOnChange`;
  después, `8b84dba docs:` con el cierre documental de la etapa 1.

## Decisiones tomadas en esta sesión

- Se adopta **comparación de contenido** (bytes) en lugar del sello `(LastWriteTimeUtc, Length)`
  para la recarga en caliente: el sello falla cuando el contenido cambia del mismo tamaño en la
  misma granularidad de timestamp, que es justo el caso de un editor de texto.
- El loader pasa a cachear el **dominio** mediante una función de proyección
  (`JsonFileLoader<TFile, TDomain>`), no el DTO.
- Con `reloadOnChange: false` el loader no hace ninguna syscall de disco tras la primera lectura.
- Se elimina `Thread.Sleep(200)` de los tests: con comparación de contenido no hace falta
  (el suite de integración bajó de 0.38 s a 0.19 s).
- Se agrega el seam `OidcMock:ConfigDirectory` / `OidcMock:ReloadOnChange` para poder arrancar el
  host contra un `config/` arbitrario (pruebas de integración y despliegues).

## Siguiente paso

1. T-05: `config/signing-key.json` + `JsonSigningKeyStore` + `ITokenSigner` (round-trip firmado
   verificado con `JsonWebTokenHandler` en unit tests).
2. T-05: `GET /personafisica/.well-known/openid-configuration` y `.../jwks`.
3. Cerrar con `dotnet build` + tests por proyecto y actualizar `progress.md` y `tasks/pending.md`.

## Cómo continuar otro prompt

1. Leer este archivo y [`progress.md`](progress.md).
2. Revisar el último commit (`git --no-pager log --oneline -5`) para saber dónde quedó el ciclo TDD.
3. Ejecutar los tests **antes** de tocar nada, para no atribuir a un cambio un fallo previo.
4. Al terminar: `docs/decisions.md` + este memory bank al día.
