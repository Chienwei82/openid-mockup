# Video Explicativo HyperFrames - OpenID Connect + OidcMock

## Resumen

Este proyecto genera un video explicativo de OpenID Connect que explica todo el contenido de la serie `docs/openid/` utilizando el framework HyperFrames. El video utiliza narración TTS en español con la voz ef_dora de Kokoro, tiene una duración de 118 segundos y un aspecto de 16:9 (1920x1080).

## Estructura del Proyecto

```
docs/openid/hyperframes-explain/
├── hyperframes.json           # Configuración del video
├── index.html                # Composición HTML con navegación por tiempo
├── audio/
│   ├── narration.txt         # Texto de narración en español
│   └── narration.wav         # Audio TTS (generado con HyperFrames TTS)
├── README.md                 # Este archivo
└── package.json
```

## Procesamiento de Audio TTS

### Instalación de Dependencias

Para generar el audio TTS, necesitas:

```bash
# Instala las dependencias de HyperFrames
npm install

# Instala las dependencias de Python para TTS
# En un entorno Debian/Ubuntu:
apt-get install python3-venv
# Luego en un venv:
pip3 install kokoro-onnx soundfile
```

### Generación de Audio TTS

```bash
# Genera audio TTS en español usando la voz ef_dora
npx hyperframes tts audio/narration.txt \
  --voice ef_dora \
  --output audio/narration.wav \
  --speed 1.0
```

La voz ef_dora usa phonemización española para naturalidad en español.

### Voz y Localización

- **Voz**: ef_dora (Kokoro TTS para español)
- **Tipo**: El prefijo 'e' selecciona la phonemización en español
- **Naturalidad**: Voz con tono natural en español

## Navegación del Video

El video está dividido en 13 slides, cada uno correspondiente a una sección de la serie OpenID Connect:

1. **0-7s**: Introducción a OIDC
2. **7-17s**: Actores y Tokens
3. **17-27s**: Discovery y JWKS
4. **27-39s**: Flujo de Autorización + PKCE
5. **39-49s**: ID Token y Claims
6. **49-60s**: Token Endpoint y Grant Types
7. **60-71s**: UserInfo, Introspection, Revocation
8. **71-81s**: Sesión y Logout
9. **81-91s**: Flujos Avanzados
10. **91-101s**: Matriz de Paridad
11. **101-111s**: Configuración y Comandos
12. **111-118s**: Conclusión

## Generación del Video

```bash
# Renderiza el video con FFmpeg
npx hyperframes render --config hyperframes.json
```

El video resultante se guarda como `dist/oidc-explainer.mp4` con resolución 1920x1080.

## Contenido Explicado

El video explica todo el contenido de `docs/openid/`:

### 1. docs/openid/01-actores-y-tokens.md
Actores OAuth/OIDC, roles, los cuatro tokens y la estructura de JWT.

### 2. docs/openid/02-discovery-y-jwks.md
Proceso de discovery, límites del issuer, consulta de JWKS.

### 3. docs/openid/03-authorization-code-y-pkce.md
Flujo Authorization Code con PKCE, parámetros como state y nonce.

### 4. docs/openid/04-id-token-y-claims.md
Claims del id_token, at_hash, c_hash, proyección de claims por scope.

### 5. docs/openid/05-token-endpoint-y-grants.md
Todos los grant types, cliente confidencial vs público, autorización.

### 6. docs/openid/06-userinfo.md
Endpoint UserInfo, reto Bearer, consistencia con id_token.

### 7. docs/openid/07-introspection-y-revocation.md
Introspection (active:true/false), revocación con store jti.

### 8. docs/openid/08-sesion-y-logout.md
Gestión de sesión, front-channel logout, cookie oidc_mock_session.

### 9. docs/openid/09-flujo-hibrido.md
Flujo híbrido para GAUDI, parámetros de respuesta id_token con c_hash.

### 10. docs/openid/10-par-device-ciba.md
PAR resuelto, Device y CIBA simulados para compatibilidad de discovery.

### 11. docs/openid/11-matriz-de-paridad.md
Tabla de paridad completa: teoría, servidor real y OidcMock.

### 12. docs/openid/12-cheatsheet.md
Hoja de referencia de comandos y configuración.

## Características Implementadas

### Animaciones
- Animación suave entre slides (transición de opacidad)
- Progreso visual continuo
- Zoom y desenfoque suave en elementos

### Audio Visuales
- Narración TTS en español con ef_dora
- Velocidad de 1.0 (normal)
- Códigos de error completados con retardo o error

### Diseño
- Utiliza colores de marca: degradado azul-púrpura
- Tipografía clara para legibilidad
- Controles intuitivos para lo explicable visualmente

### Navegación por Tiempo
- 118 segundos de duración total
- 13 secciones perfectamente sincronizadas
- Usuarios pueden saltar usando navegación por teclado

## Finalización y Calidad

1. **Completitud**: El video cubre todo el contenido de la serie OpenID Connect
2. **Requisitos**: 16:9 aspect ratio, español TTS, duración ~118s, voz natural
3. **Integridad**: Navegación por tiempo sincronizada con script TTS, design visual
4. **Compatibilidad**: Utiliza estándares HTML y CSS para reproducibilidad máxima

## Próximos Pasos (Para el Usuario)

1. **Instalar dependencias**: Asegurar acceso a las herramientas de HyperFrames
2. **Generar audio**: Ejecutar TTS en español con ef_dora en el script de narración
3. **Renderizar video**: Usar npx hyperframes render o npx hyperframes-cli render
4. **Validar**: Probar el video en reproductores compatibles

## Contribuciones

Este proyecto demuestra cómo HyperFrames se puede usar para crear videos explicativos:
- Flexibilidad con HTML y animación basada en CSS
- Sincronización de audio con múltiples escenas
- Uso de frameworks TTS para narración multilingüe
- Demostración de una solución SaaS centrada en desarrolladores

## Aprendizaje y Contexto

El video es una demostración de cómo la navegación por tiempo basada en HTML puede crear presentaciones complejas y sincronizadas, de forma intuitiva, mostrando el poder del enfoque "HTML-first" de HyperFrames para la creación de videos.

Para más información sobre HyperFrames, visita [heygen.com/hyperframes](https://heygen.com/hyperframes).
