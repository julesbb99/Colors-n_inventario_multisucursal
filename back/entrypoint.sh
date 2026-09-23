#!/bin/sh
# =============================================================================
# Arranque de la API.
#
# SU UNICO TRABAJO ES LA CLAVE DE FIRMA DE LOS TOKENS, y existe por un choque
# real entre dos exigencias:
#
#   1. `docker compose up` tiene que funcionar sin que nadie configure nada.
#   2. La clave JWT NO SE VERSIONA. Quien la tenga puede fabricar un token de
#      cualquier usuario con cualquier rol, sin dejar rastro; es mas sensible
#      que la contrasena de la base.
#
# Ponerla en el compose satisfaria (1) y rompería (2): quedaria en Git para
# siempre, y ademas seria la MISMA en todas las copias del proyecto, que es lo
# peor de todo -una clave publica compartida no es una clave-.
#
# La salida: si nadie la da, se genera una aqui, aleatoria y distinta en cada
# despliegue, y se guarda en un volumen para que sobreviva a los reinicios. Si
# no sobreviviera, cada `docker compose restart` invalidaria las sesiones de
# todo el mundo.
#
# EN PRODUCCION se pasa por variable de entorno y esto no hace nada: quien
# despliega manda, y la clave deberia venir de un gestor de secretos, no de un
# archivo dentro del contenedor.
# =============================================================================
set -e

CLAVE_GUARDADA=/var/lib/colorsin/jwt.key

if [ -z "${JwtSettings__SecretKey}" ]; then

  if [ ! -f "${CLAVE_GUARDADA}" ]; then
    # 64 bytes de /dev/urandom. El minimo que exige HMAC-SHA256 son 32; se
    # toman 64 porque no cuesta mas y deja margen.
    #
    # `-w 0` evita que base64 parta la salida en lineas de 76 caracteres: un
    # salto de linea en mitad de la clave la convierte en dos variables y la
    # API no levanta.
    head -c 64 /dev/urandom | base64 -w 0 > "${CLAVE_GUARDADA}"
    chmod 600 "${CLAVE_GUARDADA}"

    echo "colorsin: clave de firma JWT generada en ${CLAVE_GUARDADA}."
    echo "colorsin: es de DESARROLLO. En produccion pasa JwtSettings__SecretKey"
    echo "colorsin: como variable de entorno desde un gestor de secretos."
  fi

  JwtSettings__SecretKey="$(cat "${CLAVE_GUARDADA}")"
  export JwtSettings__SecretKey
fi

# `exec` REEMPLAZA este shell por dotnet, en vez de dejarlo como padre. Asi la
# API queda como PID 1 y recibe directamente el SIGTERM de `docker stop`; sin
# esto lo recibiria el shell, que no lo reenvia, y Docker acabaria matando el
# proceso a los diez segundos en mitad de lo que estuviera haciendo.
exec dotnet colorsin.Api.dll "$@"
