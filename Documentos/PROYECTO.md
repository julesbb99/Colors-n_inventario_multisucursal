# Colorsín S.A.S. — Sistema de Inventario Multisede

Sistema de gestión de inventario para una distribuidora de pinturas y productos
químicos con tres sedes en Colombia. Cubre el ciclo completo de la mercancía:
entra por una orden de compra, se rastrea por lote y caducidad, se mueve entre
sedes con traslados y sale por el mostrador en una venta.

---

## Tabla de contenido

1. [Qué resuelve](#1-qué-resuelve)
2. [Puesta en marcha](#2-puesta-en-marcha)
3. [Arquitectura](#3-arquitectura)
4. [Módulos implementados](#4-módulos-implementados)
5. [Decisiones de diseño](#5-decisiones-de-diseño)
6. [Seguridad](#6-seguridad)
7. [Estructura del repositorio](#7-estructura-del-repositorio)
8. [Estado actual y pendientes](#8-estado-actual-y-pendientes)
9. [Uso de inteligencia artificial en el desarrollo](#9-uso-de-inteligencia-artificial-en-el-desarrollo)

---

## 1. Qué resuelve

Colorsín vende recubrimientos y solventes desde tres sedes: **Cali** (principal),
**Armenia** (Eje Cafetero) y **Manizales**. El negocio tiene tres características
que mandan sobre el diseño del sistema:

**El producto caduca.** Una caneca de esmalte tiene vida útil. Vender la caja de
atrás mientras la de adelante se vence es plata que se bota, así que el sistema
despacha siempre por **FEFO** (*First Expired, First Out*): sale primero el lote
que vence antes, no el que entró antes.

**El producto se mide en varias unidades.** El mismo esmalte se compra por caneca,
se traslada por galón y se vende por litro. Guardar «5» sin decir de qué no
significa nada, así que **todo saldo vive en la unidad base del producto** y las
unidades de captura se convierten al entrar.

**Hay tres bodegas, no una.** Un gerente de Armenia no puede ver ni mover el
inventario de Cali. Eso no es una preferencia de interfaz: es una regla que se
aplica en el servidor, en cada consulta.

### Catálogo de unidades

| Unidad | Símbolo | Factor a litros |
|---|---|---|
| Litro | `L` | 1,000000 |
| Galón | `gal` | 3,785410 |
| Caneca 5 galones | `cn5` | 18,927050 |

El factor no se redondea a 3,785. Con cifras de inventario reales, ese redondeo
produce descuadres de decenas de litros al cabo de unos cientos de movimientos.

---

## 2. Puesta en marcha

### Opción A — Con Docker (recomendada)

**Único requisito: Docker Desktop.** No hace falta instalar .NET, Node ni MySQL.

```bash
docker compose up -d
```

Eso descarga las imágenes desde Docker Hub, crea la base de datos, la siembra y
levanta los tres servicios en orden.

| Servicio | URL | Notas |
|---|---|---|
| Aplicación | http://localhost:8080 | Por aquí se entra |
| API | http://localhost:5121 | Documentación en `/openapi/v1.json` |
| MySQL | `localhost:3307` | 3307 y no 3306, para no chocar con un MySQL local |

**No hay que crear ningún archivo de configuración.** Todas las variables tienen
un valor por defecto de desarrollo, y la clave de firma de los tokens se genera
sola dentro del contenedor.

Para detener sin perder datos:

```bash
docker compose down
```

Para empezar de cero, **borrando la base**:

```bash
docker compose down -v
```

#### Imágenes publicadas

| Imagen | Tamaño | Etiquetas |
|---|---|---|
| `juliana0212/colorsin-api` | 353 MB | `1.0.0`, `latest` |
| `juliana0212/colorsin-front` | 74,3 MB | `1.0.0`, `latest` |

#### Reconstruir desde el código

El `docker-compose.yml` solo nombra imágenes, a propósito: si declarara `build:`,
`docker compose up` compilaría en vez de descargar cuando la imagen no está, y
eso exigiría tener el SDK de .NET y Node instalados. Para recompilar:

```bash
docker compose -f docker-compose.yml -f docker-compose.build.yml up -d --build
```

### Opción B — Desarrollo local

Para trabajar sobre el código con recarga en caliente.

**Requisitos:** .NET 10 SDK, Node 20 o superior, Docker (solo para MySQL).

```bash
# 1. Base de datos
docker compose up -d mysql

# 2. Configuración de la API (una sola vez)
cd back/colorsin.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=localhost;Port=3307;Database=colorsin_inventario;Uid=colorsin;Pwd=TU_CLAVE;"
```

La clave de firma de los tokens hay que generarla, porque la API **no arranca sin
ella**:

```bash
# Linux / macOS
dotnet user-secrets set "JwtSettings:SecretKey" "$(openssl rand -base64 64)"
```

```powershell
# Windows PowerShell
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
$b = [byte[]]::new(64); $rng.GetBytes($b); $rng.Dispose()
dotnet user-secrets set "JwtSettings:SecretKey" ([Convert]::ToBase64String($b))
```

> **Ojo en PowerShell 5.1:** no use `RandomNumberGenerator::Fill`. Ese método no
> existe allí, el arreglo se queda en ceros y sale una clave de 88 caracteres
> formada solo por `A`. Pasa la validación de longitud y no protege de nada.

```bash
# 3. Arrancar
dotnet run --project back/colorsin.Api --launch-profile http   # http://localhost:5121
npm --prefix front run dev                                     # http://localhost:5173
```

El frontend necesita `front/.env.development` con `VITE_API_URL=http://localhost:5121/api`
(hay plantilla en `front/.env.example`).

### Credenciales de desarrollo

La base se siembra con cinco usuarios. **Están en el repositorio** y sirven para
probar; cámbielas antes de exponer la API fuera de su máquina.

| Rol | Correo | Contraseña | Sede |
|---|---|---|---|
| Administrador General | `marcela.ospina@colorsin.com.co` | `Colorsin.Dev.Admin1` | Todas |
| Gerente de Sucursal | `julian.restrepo@colorsin.com.co` | `Colorsin.Dev.Gerente1` | Cali |
| Gerente de Sucursal | `diana.carvajal@colorsin.com.co` | `Colorsin.Dev.Gerente2` | Armenia |
| Operador | `hector.zapata@colorsin.com.co` | `Colorsin.Dev.Operador1` | Cali |
| Operador | `paola.guerrero@colorsin.com.co` | `Colorsin.Dev.Operador2` | Manizales |

> El login admite **5 intentos por minuto y por IP**. Tras el quinto fallo la API
> bloquea la conexión un rato; no es que la cuenta esté mal.

---

## 3. Arquitectura

### Panorama

```
                    ┌──────────────────────────────┐
  Navegador ──────► │  nginx  (contenedor front)   │
                    │  :8080                       │
                    │                              │
                    │  /          → React (dist)   │
                    │  /api/*     → pasarela ──────┼──┐
                    └──────────────────────────────┘  │
                                                      │ red interna
                    ┌──────────────────────────────┐  │ de Compose
                    │  API .NET 10 (contenedor)    │ ◄┘
                    │  :8080  (publicado en 5121)  │
                    └──────────────┬───────────────┘
                                   │
                    ┌──────────────▼───────────────┐
                    │  MySQL 8.4 (contenedor)      │
                    │  :3306  (publicado en 3307)  │
                    └──────────────────────────────┘
```

El navegador **nunca llama a la API directamente** en el despliegue con Docker:
pide a `/api/...` sobre el mismo origen que sirvió la página y nginx reenvía. Eso
elimina el CORS por completo —no hay dos orígenes— y hace que la imagen del
frontend sirva en cualquier máquina y cualquier puerto sin recompilar.

### Backend — Clean Architecture

Cuatro proyectos, con las dependencias apuntando siempre hacia adentro:

```
colorsin.Api ──────► colorsin.Application ──────► colorsin.Domain
       │                      ▲                          ▲
       └──► colorsin.Infrastructure ──────────────────────┘
```

| Proyecto | Responsabilidad | No conoce |
|---|---|---|
| **Domain** | Entidades y enums. C# puro. | Nada. Ni EF, ni ASP.NET |
| **Application** | Reglas de negocio, servicios, DTOs, interfaces de repositorio | Cómo se persiste |
| **Infrastructure** | EF Core, repositorios, consultas | Quién la llama |
| **Api** | Endpoints, autenticación, mapeo a HTTP | Las reglas de negocio |

El punto de esa dirección: **las reglas de negocio no dependen de MySQL**. Cambiar
el motor de base de datos toca `Infrastructure` y nada más.

**Endpoints mínimos, sin controladores.** Toda la superficie HTTP vive en
`back/colorsin.Api/Endpoints/*.cs`, agrupada por módulo.

### Frontend

React 19 + Vite 6 + TypeScript 5.7 en modo estricto, con Tailwind 3.

```
front/src/
├── pages/          una por ruta del menú
├── components/     agrupados por módulo, más un ui/ compartido
├── services/       una función por endpoint; el único sitio que sabe de HTTP
├── models/         espejos de los DTO de la API
├── context/        Auth, Sede, Catálogos
├── hooks/          useConsulta, useEnvio, useAuth, useSede, useCatalogos
└── interceptors/   axios: firma cada llamada y detecta el token vencido
```

Dos ganchos concentran el 90 % del trabajo repetido:

- **`useConsulta`** — carga, error, recarga y cancelación al desmontar.
- **`useEnvio`** — estado de envío, error legible y detección de 403.

### Base de datos

El esquema **no lo generan las migraciones de EF**, sino scripts SQL numerados en
`infra/mysql/init/`, que MySQL ejecuta en orden la primera vez que arranca sobre
un volumen vacío.

| Script | Qué añade |
|---|---|
| `01`–`08` | Esquema base, datos semilla y auditoría |
| `09`–`11` | Lote único, recepción parcial, guías y estados de traslado |
| `12`–`16` | Baja lógica de existencias, proveedores y transportadoras; precio de venta |
| `17`–`18` | Traslado cerrado con faltante; cumplimiento logístico |
| `19` | Baja lógica de usuarios |
| `20` | Caducidad obligatoria en lotes |

Todos son **idempotentes**: se pueden volver a ejecutar sobre una base ya creada.

---

## 4. Módulos implementados

### Autenticación

Login con JWT válido **60 minutos**. El token lleva el id, el nombre, el correo,
el rol y —si aplica— la sede. Un perfil deshabilitado no entra.

### Dashboard

KPIs de la sede seleccionada, histórico de ventas por mes, rotación de productos
y comparativa entre sedes.

- **Rotación** clasificada por **días de cobertura**, no por cantidad vendida:
  «400 litros» no dice si es mucho sin saber cuánto hay en bodega.
- **Comparativa entre sedes** visible solo para administración y gerencia.

### Inventario

- **Existencias** — saldo por sede y producto, consultable en cualquier unidad.
  Es la única consulta del sistema que **no aísla por sede**: cualquier rol ve el
  saldo de toda la red, porque antes de pedir un traslado hay que saber quién
  tiene.
- **Lotes FEFO** — trazabilidad y caducidad, con pestañas por estado.

Los lotes **no se crean a mano**: nacen al recibir una compra (con el número
impreso en el envase) o al recibir un traslado, que recrea en el destino el que
salió del origen.

### Compras

Órdenes a proveedor, catálogo de proveedores con lista de precios y recepción de
mercancía con **entregas parciales**.

Al recibir, el **número de lote y la fecha de caducidad son obligatorios**: sin
ellos la mercancía entraría sin trazabilidad y sin la clave que ordena el FEFO.

### Ventas

Mostrador con descuento automático por FEFO, catálogo de clientes, lista de
precios de red y panel de margen que avisa si el precio escrito queda por debajo
del costo. Consulta de ventas por día con su comprobante.

### Traslados entre sedes

El flujo completo: lo **pide el destino**, lo **despacha el origen**.

```
Solicitada ──┬─► EnTransito ──┬─► Completada
             │                ├─► RecibidaParcial ──► Cerrada con faltante
             ├─► Rechazada    │
             └─► Cancelada    └─► (novedad abierta) ──► sigue «por recibir»
```

- El origen puede **ajustar la cantidad** si no tiene todo lo que le piden.
- Una recepción parcial registra la diferencia y **exige definir el tratamiento**:
  reenvío, reclamación o asumido.
- Con una novedad en reenvío o reclamación, el traslado **no se cierra**: sigue
  en «por recibir» hasta que alguien le dé desenlace, con su motivo.
- El botón **Recibir** no se habilita hasta la fecha estimada de llegada.
- **Informe de cumplimiento logístico** por sede y ruta, y traslado por traslado.

### Usuarios

Alta y baja lógica, con una jerarquía que vive en el código
(`ReglasGestionUsuario`) y no en la base:

| Quién | Puede deshabilitar |
|---|---|
| Administrador General | Gerentes y operadores, en cualquier sede |
| Gerente de Sucursal | Operadores **de su propia sede** |
| Operador | A nadie |

Más dos reglas de sentido común: nadie se deshabilita a sí mismo, y un disparador
en la base impide dejar el sistema sin ningún Administrador General activo.

---

## 5. Decisiones de diseño

### Los saldos viven en unidad base

Cada producto tiene su unidad base y **todo saldo se guarda ahí**. Las unidades de
captura se convierten al entrar. Sin esto, sumar «3 canecas + 2 galones + 10
litros» exige conocer el factor en cada consulta, y basta que un sitio lo olvide
para descuadrar el inventario.

Es también la razón de un error que estuvo a punto de colarse: la cantidad de un
lote va en unidad **base**, no en la del traslado. Rotular 18,9271 L con el «gal»
del traslado habría sido un error de factor 3,785.

### Dos ejes de autorización, no uno

El rol dice **qué** se puede hacer; la sede dice **sobre qué datos**. Un gerente
de Armenia y uno de Cali tienen el mismo rol y no deben ver lo mismo.

- **Rol** → `PoliticasAutorizacion`: `Supervision`, `SoloAdminGeneral`,
  `RecepcionMercancia`.
- **Sede** → `IUsuarioContexto`: `ResolverFiltroSucursal`,
  `ExigirAccesoASucursal`, `ExigirAccesoAAlgunaDe`.

Los dos se aplican **en el servidor**. Ocultar un botón no es control de acceso:
es cortesía con quien usa la aplicación.

### El stock solo se mueve con un documento detrás

Existió un «registrar movimiento» manual y **se quitó para todos los roles**, con
su ruta de API incluida. Era la única vía que cambiaba un saldo sin nada que lo
explicara —y por eso mismo, la vía por la que se tapa un faltante—.

Hoy el stock se mueve por **compras, ventas y traslados**, y cada movimiento
apunta a su documento de origen.

### Baja lógica en todo lo referenciado

Existencias, proveedores, transportadoras y usuarios se dan de baja con una
columna `activo`, nunca con `DELETE`. El motivo es concreto: media docena de
claves foráneas los referencian con `ON DELETE RESTRICT`, y eso es deliberado —la
bitácora no puede quedarse sin responsable—. Un `DELETE` fallaría contra la
primera, y forzarlo dejaría ventas firmadas por nadie.

### Los campos numéricos filtran al teclear

Hay **un solo componente numérico** en toda la aplicación, `CampoNumero`, y no es
un `<input type="number">`. Ese acepta cosas que aquí no significan nada y que no
se ven hasta que fallan: el signo menos, el más y la notación científica —`1e5`
es un número válido para el navegador—. Además su separador decimal depende de la
configuración del equipo.

`CampoNumero` filtra al teclear: dígitos y **una** coma. El punto del teclado
numérico se escribe como coma. Lo que no cumple no llega a escribirse, y pegar
texto inválido no deja nada.

### La caducidad es obligatoria, y la base lo garantiza

`lotes.fecha_vencimiento` es `NOT NULL`. La regla se aplica en tres capas —base,
API y pantalla— porque a `lotes` se escribe por tres puertas distintas, y una
regla que vive en un formulario no cubre las otras dos.

Un lote sin fecha era el peor de todos: no entraba en las alertas de vencimiento
y se iba al final de la cola FEFO. Es decir, el lote del que menos se sabía era el
último en despacharse y el único del que nadie avisaba.

### Semántica de los faltantes en traslados

Dos diferencias que parecen lo mismo y no lo son:

| Diferencia | Qué significa |
|---|---|
| `solicitada − despachada` | El origen no tenía todo. **No es una pérdida** |
| `despachada − recibida` | Se perdió en tránsito. **Esto es lo que se reclama** |

Igual con los cierres: **rechazado** es una decisión del origen y baja su
indicador de atención; **cancelado** lo retira quien pidió y no cuenta contra
nadie.

### El cumplimiento se mide en conteos, nunca en volúmenes

Un traslado de 5 canecas y otro de 2 litros pesan **lo mismo** en el porcentaje
de cumplimiento. Promediar volúmenes de productos distintos da un número que no
significa nada.

### Los enum entran como número y salen como texto

La API no registra `JsonStringEnumConverter`, así que al **enviar** hay que mandar
el ordinal; al **recibir**, las respuestas traen texto ya resuelto. Mandar
`"Ingreso"` donde va un `0` produce un 400 que no dice qué campo falló.

### La clave de firma nunca se versiona

`appsettings.json` lleva solo el marcador `__DEFINIR_EN_USER_SECRETS__`, y la API
**falla al arrancar** si la clave falta, es el marcador o mide menos de 32 bytes.

En el despliegue con Docker esto chocaba con «cero configuración manual». La
salida no fue poner la clave en el compose —quedaría en Git, y sería la misma en
todas las copias del proyecto—, sino **generarla dentro del contenedor** en el
primer arranque y guardarla en un volumen, para que sobreviva a los reinicios sin
invalidar las sesiones abiertas.

### CORS jamás con `*`

La API entrega datos comerciales. Sin orígenes configurados **no se registra
política de CORS**, que es el comportamiento seguro por defecto: el navegador
bloquea. En el despliegue con Docker el problema desaparece, porque nginx hace
que todo viva en el mismo origen.

---

## 6. Seguridad

| Medida | Dónde |
|---|---|
| Contraseñas con **BCrypt**, factor de trabajo 12 | `usuarios.password_hash` |
| JWT firmado con HMAC-SHA256, mínimo 32 bytes de clave | Validado al arrancar |
| Límite de **5 intentos de login por minuto y por IP** | Middleware de la API |
| Aislamiento por sede en cada consulta | `IUsuarioContexto` |
| Bitácora de eventos sensibles | `auditoria_eventos` |
| Sin `AllowAnyOrigin` | Política de CORS |

Un detalle que importa: **la comprobación de perfil deshabilitado va después de
verificar la contraseña**. Comprobarla antes convertiría el endpoint de login en
un detector de cuentas: respondería distinto para un correo que existe y uno que
no.

La bitácora registra quién hizo qué y cuándo, con los valores anterior y nuevo en
las correcciones. Es la única tabla que no se reescribe desde la aplicación.

### Antes de exponer la API

1. Cambiar las contraseñas de los cinco usuarios sembrados.
2. Definir `JWT_SECRET_KEY` desde un gestor de secretos.
3. Cambiar las contraseñas de MySQL —los valores por defecto del `docker-compose.yml`
   son de desarrollo y están a la vista a propósito—.
4. Poner los orígenes reales en `Cors__OrigenesPermitidos`.

---

## 7. Estructura del repositorio

```
inventario_colorsin/
├── back/
│   ├── colorsin.Domain/           entidades y enums, C# puro
│   ├── colorsin.Application/      servicios, DTOs, reglas
│   ├── colorsin.Infrastructure/   EF Core y repositorios
│   ├── colorsin.Api/              endpoints mínimos y autenticación
│   ├── Dockerfile                 compilación en dos etapas
│   └── entrypoint.sh              genera la clave JWT si no se la dan
├── front/
│   ├── src/                       React + TypeScript
│   ├── Dockerfile                 compila y sirve con nginx
│   └── nginx.conf                 SPA + pasarela hacia /api
├── infra/mysql/init/              esquema y datos, 20 scripts numerados
├── Documentos/                    este documento
├── docker-compose.yml             despliegue: un solo comando
├── docker-compose.build.yml       capa opcional para recompilar
└── .env.example                   plantilla; no hace falta para arrancar
```

---

## 8. Estado actual y pendientes

### Funciona y está verificado

Los siete módulos, con sus permisos por rol y sede probados contra la API en
vivo y en el navegador. El despliegue con Docker se validó borrando las imágenes
locales y descargándolas de nuevo desde Docker Hub.

### Limitaciones conocidas

| Tema | Detalle |
|---|---|
| **Migraciones de EF** | Desfasadas respecto del esquema real. El esquema lo mandan los scripts de `infra/mysql/init/`; la API no llama a `Migrate()` |
| **Pruebas automatizadas** | No hay proyecto de pruebas. La verificación fue manual contra la API y el navegador |
| **Recepción parcial de traslados** | No hay forma de recibir el **resto** de un traslado recibido a medias: la API solo recibe desde `EnTransito` |
| **Llegada anticipada** | Si un envío llega antes, nadie puede recibirlo hasta la fecha estimada, y esa fecha no se puede corregir tras el despacho |
| **Mermas y ajustes** | Al quitar el movimiento manual, no queda forma de registrar una merma, una devolución ni un ajuste por conteo físico. Si se necesita, conviene darle su propio flujo con motivo obligatorio |
| **Caducidad vencida al recibir** | Nada impide teclear una fecha ya pasada. Suele ser un error de digitación, pero también puede ser real |
| **Tamaño del paquete del frontend** | ~520 KB sin comprimir (~153 KB con gzip). Se puede partir con `manualChunks` |

### Convenciones que conviene conocer antes de tocar el código

**El rol se escribe de cuatro maneras distintas**, y las cuatro son correctas en
su sitio:

| Dónde | Cómo se escribe |
|---|---|
| ENUM de MySQL | `'Gerente de Sucursal'` |
| Enum del dominio | `GerenteDeSucursal` |
| Claim del token | `GerenteSucursal` |
| Ordinal | `1` |

**Los comentarios del código explican el porqué, no el qué.** Varios documentan
errores reales que costaron tiempo encontrar —la doble barra de desplazamiento,
el redondeo del factor de galones, el `toISOString()` que corre la fecha un día
en UTC−5—. Borrarlos invita a repetirlos.

---

## 9. Uso de inteligencia artificial en el desarrollo

### 9.1 Herramienta y alcance

| | |
|---|---|
| **Herramienta** | Claude Code (Anthropic), en la aplicación de escritorio de Claude |
| **Modelo** | Claude Opus 5 |
| **Periodo** | 16 – 23 de septiembre de 2026 (8 días, 32 commits) |
| **Modo de trabajo** | Conversacional, con acceso a los archivos del proyecto, a la terminal, a la base de datos y a un navegador integrado para verificar en pantalla |

La asistencia no se limitó a completar código: la herramienta ejecutaba comandos,
consultaba MySQL, llamaba a la API en vivo y abría la aplicación en un navegador
para comprobar lo que acababa de escribir.

### 9.2 Etapas y su cobertura

Reconstruidas desde el historial de Git:

| Fecha | Etapa | Participación de la IA |
|---|---|---|
| 16 sep | Esquema MySQL, datos semilla, solución .NET, Clean Architecture, entidades, `DbContext`, migración inicial | Alta — generación completa a partir del modelo de negocio descrito |
| 17 sep | Los seis módulos de `Application`, autenticación JWT, roles, aislamiento por sede, CORS | Alta — implementación; las reglas de permisos las definió la desarrolladora |
| 18 sep | Andamiaje de React, layout, componentes reutilizables, vistas por módulo | Alta |
| 19 – 21 sep | Ajustes por módulo: precios de referencia, permisos del operario, transportadoras, dashboard, comprobantes, trazabilidad de lotes, cumplimiento logístico | Media/alta — dirigida por pruebas y reportes de fallos de la desarrolladora |
| 23 sep | Caducidad obligatoria, barrido de campos numéricos, retoques de interfaz, contenedores y despliegue, esta documentación | Alta |

### 9.3 Ejemplos concretos

#### Ejemplo 1 — Requisito de negocio extenso, implementado en varias capas

**Prompt (textual, abreviado):**

> «La sede de origen si no tiene la cantidad que le piden puede ajustar la
> cantidad que puede entregar, ejemplo piden 5 y yo solo puedo dar 3 [...] Si se
> recibe parcialmente se registra la diferencia, se genera una alerta y se define
> el tratamiento (reenvio o reclamación) [...] el boton de recibir en todos los
> roles no se pueden habilitar hasta que llegue la fecha de llegada real».

**Resultado:** un script de esquema (`18_traslados_cumplimiento.sql`) con tres
columnas nuevas y dos restricciones `CHECK`, cambios en el servicio de
transferencias, nuevos DTOs, dos endpoints de informe y cuatro componentes de
React. Fragmento del esquema generado:

```sql
ALTER TABLE `transferencias`
  ADD CONSTRAINT `chk_transf_despachada`
  CHECK (`cantidad_despachada` IS NULL
         OR `cantidad_despachada` <= `cantidad_solicitada`);
```

**Aporte real:** la traducción de un párrafo en lenguaje natural a un cambio
coherente en base de datos, backend y frontend a la vez, sin que ninguna capa
quedara desincronizada.

#### Ejemplo 2 — Reporte de fallo con captura, que destapó un error latente

**Prompt (textual):**

> «Me aparece esto en una de las filas de los traslados en la columna de
> novedades, si solo se puede marcar una novedad por qué aparecen 4? podrias
> organizar esto a que solo aparezca una novedad y hacer una prueba interna de
> qué pasaría si mi compra tiene más de una fila?»
> *(acompañado de una captura de pantalla)*

**Resultado:** la prueba interna que pidió el prompt encontró un fallo **distinto
del reportado**: una avería con tratamiento «ninguno» cerraba un traslado que
todavía tenía un faltante en reclamación abierto. La corrección:

```csharp
var pendientesPrevias = await _transferencias.ContarNovedadesAbiertasAsync(
    peticion.TransferenciaId, null, ct);
...
var cierra = !quedaAbierta && pendientesPrevias == 0
          && estadoAnterior == EstadoTransferencia.RecibidaParcial;
```

**Aporte real:** el valor no estuvo en arreglar lo que se veía, sino en que
pedir explícitamente «haz una prueba interna» destapó un caso que nadie había
mirado.

#### Ejemplo 3 — Síntoma vago que resultó ser una fuga entre sedes

**Prompt (textual):**

> «En el dashboard del operario muestra un producto en LOTE FEFO que ya se
> vendió, pero en el panel del dashboard aparece correcto, en 0 porque ya se
> vendió.»

**Resultado:** la contradicción entre dos cifras de la misma pantalla llevó a un
método de repositorio que ignoraba el filtro de sede —el único de los seis que lo
omitía—:

```csharp
// ESTE FILTRO FALTABA, y es el unico metodo del repositorio al que le faltaba.
if (sucursalId is int sid) { consulta = consulta.Where(l => l.SucursalId == sid); }
```

No era solo un dato mal mostrado: era una **fuga de aislamiento entre sedes**. Se
revisaron después los seis repositorios para confirmar que el fallo estaba
aislado.

#### Ejemplo 4 — Petición transversal convertida en decisión de arquitectura

**Prompt (textual):**

> «Podrías asegurarte de que en todos los espacios donde el ingreso son solo
> números no se pueda agregar ningún signo como *,+,: etc, solo numeros, en cada
> uno de los módulos.»

**Resultado:** en vez de parchear 17 campos, se unificaron en un solo componente.
Existían dos —uno filtrado y otro que era un `<input type="number">` del
navegador, que acepta `-1` y `1e5` sin protestar—. Se eliminó el segundo. Prueba
tecla a tecla en el navegador:

| Se escribe | Queda |
|---|---|
| `9` `*` `8` `.` `5` | `98,5` — el `*` no entra; el punto se vuelve coma |
| `-` `+` `:` `e` `espacio` `$` | sin cambios: ninguno pasa |
| `3` `,` `7` `,` `5` | `98,5375` — la segunda coma se descarta |

**Aporte real:** reconocer que la petición no era «filtrar 17 campos» sino «que
solo exista una forma de escribir un número».

#### Ejemplo 5 — Despliegue completo

**Prompt (textual, abreviado):**

> «Crea el dockerfile, luego construye la imagen y súbela a docker hub y
> descárgala, luego despliega esto para el back y el front [...] con un solo
> comando usando Docker Compose. No deben existir dependencias de configuración
> manual en el entorno local.»

**Resultado:** dos `Dockerfile` multietapa, `nginx.conf` con pasarela hacia la
API, `docker-compose.yml` y una capa aparte para reconstruir. La verificación
borró las imágenes locales y las descargó de nuevo desde Docker Hub.

Aquí apareció un **conflicto entre dos requisitos** que la IA señaló en vez de
resolver en silencio: «cero configuración manual» pedía una clave JWT en el
compose, y la regla del proyecto es que esa clave nunca se versiona. La salida
fue generarla dentro del contenedor:

```sh
head -c 64 /dev/urandom | base64 -w 0 > "${CLAVE_GUARDADA}"
```

Comprobado con un ciclo `down` + `up`: la huella SHA-256 de la clave no cambió,
así que las sesiones abiertas sobreviven a un reinicio.

### 9.4 Qué aportó la IA

- **Velocidad en lo repetitivo.** Un módulo completo —entidad, DTOs, repositorio,
  servicio, endpoints, servicio del frontend, página— en una sola pasada y con
  las capas coherentes entre sí.
- **Coherencia entre capas.** Un cambio de esquema arrastraba solo el DTO, el
  mapeo, el modelo de TypeScript y el componente. Es donde más fácil se olvida
  algo trabajando a mano.
- **Verificación, no solo escritura.** Cada cambio se probó contra la API en
  vivo y en el navegador. El despliegue se validó borrando las imágenes y
  volviéndolas a descargar.
- **Detección de efectos colaterales.** Al hacer obligatoria la caducidad,
  encontró que el endpoint de corrección de lotes permitía **borrar** la fecha:
  bastaba crear el lote con fecha y borrársela después para dejarlo fuera de las
  alertas. No estaba en la petición.
- **Documentación del porqué.** Los comentarios registran errores reales y las
  razones de cada decisión; son el 23 % del código de backend.
- **Disciplina con los datos.** Los datos de prueba se crearon y se revirtieron
  por id exacto, con conteos antes y después. Por ejemplo: una recepción de
  prueba que tocó cinco tablas se deshizo por completo y se verificó que el saldo
  volviera a `958,1021 L`.

### 9.5 Qué fue necesario ajustar manualmente

Los errores de la IA se corrigieron dentro de la misma conversación, pero fueron
reales y conviene dejarlos escritos:

| Qué pasó | Consecuencia |
|---|---|
| **Método de prueba defectuoso.** En PowerShell, `$r = Invoke-RestMethod` conserva el valor anterior si la llamada falla sin terminar. Tres tokens arrastraron silenciosamente el del usuario anterior | Pareció que el aislamiento por sede estaba roto. Hubo que rehacer la prueba decodificando los claims de cada token |
| **Conteo equivocado.** `@(Invoke-RestMethod)` sobre un arreglo JSON vacío cuenta 1, no 0 | Falsa alarma en una prueba de alertas de vencimiento. El fallo era de la comprobación, no de la aplicación |
| **Semántica sutil alterada.** Al refactorizar, se cambió `Number('')` —que es `0`— por un `null`, lo que habría modificado cuándo aparece un botón | Lo atajó el compilador de TypeScript; se restauró el comportamiento exacto |
| **Errores de compilación** | `DayNumber` existe en `DateOnly`, no en `DateTime`; `CS8602` por desreferencia nula; bloqueo `MSB3021` por compilar con la API corriendo |
| **Ruta equivocada** | Se probó `/recepcion` cuando el endpoint es `/recepciones`: cuatro respuestas 404 antes de mirar el enrutado real |

Un caso aparte, porque se detectó antes de romper nada: al mostrar los lotes de
un traslado, la cantidad de un lote va en la unidad **base** del producto, no en
la del traslado. Rotular 18,9271 L con el «gal» del traslado habría sido un error
de **factor 3,785** mostrado como si fuera un dato correcto.

### 9.6 Dónde no fue útil

- **Requisitos ambiguos.** La frase «en ningún rol debería dejar de "registrar
  movimiento"» admite dos lecturas opuestas —quitarlo para todos, o dárselo a
  todos—. La gramática apuntaba a una y el contexto a la otra. La IA no podía
  resolverlo y **tuvo que preguntar**. Decidir mal habría significado borrar una
  función o abrirle a los operarios la única vía que cambia el stock sin
  documento.

- **Conocer la operación real.** Si el operario debe o no reportar una merma no
  se deduce del código: depende de cómo trabaja la bodega. La IA puede exponer el
  costo de cada opción; la decisión es del negocio.

- **Juzgar lo visual sin verlo.** Fallos como la doble barra de desplazamiento o
  las cuatro novedades repetidas en una fila **los encontró la desarrolladora**,
  no la IA, y hubo que aportar capturas de pantalla para que fueran accionables.

- **Credenciales y acciones de cuenta.** La sesión de Docker Hub y las claves las
  gestiona la persona. La IA no introduce contraseñas.

- **Validar contra la realidad del negocio.** Que la caducidad deba ser
  obligatoria porque «todo lo que vende Colorsín caduca» es un hecho del
  catálogo. La IA lo habría dejado opcional indefinidamente.

### 9.7 Estimación del código generado con asistencia de IA

**Base de la estimación:** conteo de líneas del repositorio a 23 de septiembre de
2026, cruzado con el historial de Git y con el registro de las sesiones de
trabajo.

| Área | Líneas | % con asistencia de IA |
|---|---:|---:|
| Backend C# (sin `obj/`, `bin/`) | 33 783 | ~98 % |
| Frontend TypeScript / React | 14 168 | ~98 % |
| SQL (esquema y datos semilla) | 2 250 | ~95 % |
| Contenedores e infraestructura | 482 | 100 % |
| Documentación | 626 | 100 % |
| **Total** | **51 309** | **~98 %** |

De las 33 783 líneas de backend, **7 956 son comentarios** (23 %) y 4 989 líneas
en blanco; el código ejecutable son unas 20 800. En el frontend, 2 523 líneas son
comentarios.

#### Qué significa y qué no significa ese 98 %

El porcentaje mide **quién tecleó las líneas**, y en ese sentido es alto y honesto.
No mide la autoría del proyecto, que se reparte distinto:

| Aporte | Origen |
|---|---|
| Líneas escritas | IA, casi en su totalidad |
| Requisitos y reglas de negocio | Desarrolladora, en su totalidad |
| Detección de fallos en uso real | Desarrolladora, mayoritariamente |
| Decisiones técnicas | Mixto: la IA proponía y explicaba el costo; la aceptación fue siempre de la desarrolladora |
| Pruebas de aceptación | Desarrolladora |
| Decisiones ambiguas | Desarrolladora, tras consulta explícita |

Dicho de otro modo: la IA escribió casi todo el código, y **ninguna línea entró
sin que la desarrolladora pidiera el cambio, lo probara y lo aceptara**. Los tres
fallos más caros del proyecto —la fuga de aislamiento entre sedes, la doble barra
de desplazamiento y las novedades duplicadas— los detectó ella usando la
aplicación, no la IA revisando el código.

> **Nota sobre la precisión de esta sección.** Las etapas del 16 al 21 de
> septiembre están reconstruidas desde el historial de Git y los mensajes de
> commit; el detalle verbatim de los prompts corresponde a las sesiones del 21 al
> 23, de las que se conserva registro completo. Los porcentajes son una
> estimación fundamentada, no una medición automática.
