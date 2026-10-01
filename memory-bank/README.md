# Memory Bank — OidcMock

Contexto persistente del proyecto. **Única fuente de verdad entre sesiones**: antes de tocar
código, lee `active-context.md` y `progress.md`; después de cada cambio, actualiza ambos.

## Estructura

| Archivo | Contenido | Se actualiza |
|---|---|---|
| [project-brief.md](project-brief.md) | Qué es OidcMock, objetivo, alcance y reglas innegociables | rara vez |
| [product-context.md](product-context.md) | Para qué sirve, a quién, qué NO hace (límites) | rara vez |
| [system-design.md](system-design.md) | Arquitectura, proyectos, flujo de datos, endpoints previstos | cada cambio estructural |
| [tech-decisions.md](tech-decisions.md) | Decisiones técnicas con su porqué (ADRs) | al decidir |
| [file-index.md](file-index.md) | Índice de archivos relevantes y su rol | al agregar/eliminar archivos |
| [progress.md](progress.md) | Qué se ha hecho, qué falta, estado de tests | cada ciclo |
| [active-context.md](active-context.md) | Foco actual: la tarea en curso y el siguiente paso | cada ciclo |
| [tasks/](tasks/) | Tareas pendientes detailadas | al planificar |

## Convenciones

- Todo el contenido en **español**, alineado con `AGENTS.md` y `docs/decisions.md`.
- Los ADRs de detalle viven en [`docs/decisions.md`](../docs/decisions.md); `tech-decisions.md`
  resume el estado actual y apunta allí para el histórico.
- TDD estricto: `test:` → ciclo rojo → `fix:`/`feat:`/`refactor:` → verde. Un commit por paso.
- Antes de dar cualquier tarea por terminada: `dotnet build` y los runners de test en verde.
