# Frontend de Colorsin

React 19 + Vite 6 + TypeScript + Tailwind 3, contra la API de `back/colorsin.Api`.

## Antes de empezar

**Esta maquina no tiene Node instalado**, asi que el proyecto esta escrito pero
las dependencias no se instalaron y `npm run build` no se ha ejecutado nunca.

Instala Node 20 o superior y luego:

```bash
cd front
npm install
npm run build
```

`npm run build` corre `tsc --noEmit` antes de empaquetar, asi que cualquier
error de tipos sale ahi.

## Levantar en desarrollo

Hacen falta las tres piezas, en este orden:

```bash
docker compose up -d                          # MySQL en el puerto 3307
cd back/colorsin.Api && dotnet run            # API en el puerto 5121
cd front && npm run dev                       # frontend en el puerto 5173
```

El puerto 5173 no es negociable sin tocar el backend: es uno de los origenes de
la lista blanca de CORS (`Cors:OrigenesPermitidos` en
`back/colorsin.Api/appsettings.Development.json`). Si se cambia aqui, hay que
agregarlo alla o el navegador bloquea cada llamada.

## Configuracion

`.env.development` define la URL de la API. No se versiona: el `.gitignore` de
la raiz ignora `.env.*`. Copia `.env.example` en tu maquina.

```
VITE_API_URL=http://localhost:5121/api
```

## Credenciales de desarrollo

Estan en `infra/mysql/init/07_seed_data.sql` y **son de desarrollo**: cambialas
antes de exponer nada.

| Correo | Clave | Rol | Sede |
|---|---|---|---|
| marcela.ospina@colorsin.com.co | Colorsin.Dev.Admin1 | AdminGeneral | todas |
| julian.restrepo@colorsin.com.co | Colorsin.Dev.Gerente1 | GerenteSucursal | 1 |
| hector.zapata@colorsin.com.co | Colorsin.Dev.Operador1 | Operador | 1 |

Entra con Marcela para ver el selector de sede habilitado, y con Hector para
verlo bloqueado en su sede.

## Como esta organizado

```
src/
  models/         Interfaces que espejan los DTO de la API
  interceptors/   axiosInstance: token en cada peticion, 401/403/429 traducidos
  services/       Las llamadas HTTP, en un solo sitio
  context/        AuthContext (sesion) y SedeContext (sede activa)
  hooks/          useAuth, useSede
  router/         AppRouter y ProtectedRoute
  components/     layout/, dashboard/, inventario/, ui/
  pages/          auth/, dashboard/, inventario/
  utils/          formato: litros, pesos, fechas y chips de color
```

## Tres cosas que conviene saber antes de tocar el codigo

**El rol para permisos sale del token, no de `usuario.rol`.** La respuesta del
login trae `rol` con el texto de la base (`"Administrador General"`), mientras
que el claim del token dice `"AdminGeneral"`. Comparar el primero contra las
constantes de `ROLES` no falla: simplemente nunca da verdadero. Por eso
`AuthContext` reconstruye la sesion decodificando el token.

**Los claims van con las URI largas.** La API firma con `JsonWebTokenHandler` y
`MapInboundClaims = false`, asi que escribe los tipos de `ClaimTypes` tal cual:
`http://schemas.microsoft.com/ws/2008/06/identity/claims/role`, no `role`. Estan
en `models/auth.ts` como constantes; leerlos con los nombres cortos devuelve
`undefined` en silencio.

**El selector de sede no es seguridad.** Deshabilitarlo para gerentes y
operadores solo evita que se topen con un 403 sin entender por que. Quien impide
de verdad ver otra sede es la API.
