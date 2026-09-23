# =============================================================================
# Frontend React + Vite de Colorsin
#
# Contexto de construccion: la carpeta `front/`.
#   docker build -f front/Dockerfile front
#
# La imagen final es nginx con los archivos ya compilados. NO lleva Node: un
# sitio hecho de HTML, CSS y JavaScript no necesita un servidor de aplicaciones
# para servirse, y `vite preview` no esta pensado para eso.
# =============================================================================

# -----------------------------------------------------------------------------
# Etapa 1: compilar
# -----------------------------------------------------------------------------
FROM node:24-alpine AS build
WORKDIR /app

# El manifiesto primero, por lo mismo que los .csproj en la API: `npm ci` solo
# se repite cuando cambian las dependencias, no cada vez que se toca un
# componente.
#
# `npm ci` y no `npm install`: instala EXACTAMENTE lo que fija package-lock.json
# y falla si el lock no concuerda. `install` puede resolver otra version menor y
# producir una imagen distinta de la que se probo.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# LA URL DE LA API SE INCRUSTA AL COMPILAR, no se lee al arrancar. Vite
# sustituye `import.meta.env.VITE_API_URL` por su valor literal dentro del
# paquete, asi que cambiarla despues exige volver a compilar.
#
# Por eso el valor por defecto es RELATIVO, `/api`, y no `http://localhost:5121`:
# una ruta relativa hace que el navegador pida al MISMO origen desde el que
# cargo la pagina, y de ahi nginx la reenvia a la API. La imagen sirve entonces
# en cualquier maquina y en cualquier puerto sin recompilar, y de paso
# desaparece el CORS, porque deja de haber dos origenes.
ARG VITE_API_URL=/api
ENV VITE_API_URL=${VITE_API_URL}

# `npm run build` corre `tsc --noEmit && vite build`: si hay un error de tipos,
# la imagen no se construye. Es deliberado -mas vale romper aqui que publicar
# una imagen que falla en el navegador-.
RUN npm run build

# -----------------------------------------------------------------------------
# Etapa 2: servir
# -----------------------------------------------------------------------------
FROM nginx:1.27-alpine AS final

COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

# 8080 y no 80: por debajo de 1024 hacen falta privilegios de root para abrir el
# puerto. Publicarlo hacia fuera en el 8080 del host no cuesta nada.
EXPOSE 8080
