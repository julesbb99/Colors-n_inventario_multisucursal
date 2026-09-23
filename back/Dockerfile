# =============================================================================
# API .NET 10 de Colorsin
#
# Contexto de construccion: la carpeta `back/`.
#   docker build -f back/Dockerfile back
#
# Dos etapas. La imagen final NO lleva el SDK ni el codigo fuente: solo el
# runtime de ASP.NET y los ensamblados publicados. Son ~110 MB frente a los
# ~900 MB del SDK, y sobre todo no reparte el codigo con la imagen.
# =============================================================================

# -----------------------------------------------------------------------------
# Etapa 1: compilar y publicar
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# LOS .csproj PRIMERO, SOLOS Y ANTES QUE EL CODIGO. No es manía: Docker cachea
# cada instruccion segun lo que copio, y `restore` es lo lento de esta etapa
# -baja NuGet entero-. Copiando antes solo los proyectos, ese paso solo se
# repite cuando cambian las DEPENDENCIAS. Si se copiara todo de una vez, tocar
# una linea de un servicio invalidaria la cache y volveria a descargar los
# paquetes en cada compilacion.
COPY colorsin.sln ./
COPY colorsin.Domain/colorsin.Domain.csproj                 colorsin.Domain/
COPY colorsin.Application/colorsin.Application.csproj       colorsin.Application/
COPY colorsin.Infrastructure/colorsin.Infrastructure.csproj colorsin.Infrastructure/
COPY colorsin.Api/colorsin.Api.csproj                       colorsin.Api/
RUN dotnet restore colorsin.sln

# Ahora si, el codigo.
COPY . .

# `--no-restore` porque ya se hizo arriba; sin esto se repetiria y el cacheo
# anterior no habria servido de nada.
RUN dotnet publish colorsin.Api/colorsin.Api.csproj \
      --configuration Release \
      --no-restore \
      --output /app/publicado

# -----------------------------------------------------------------------------
# Etapa 2: la imagen que se ejecuta
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# EL PUERTO DENTRO DEL CONTENEDOR ES EL 8080, no el 5121 del perfil de
# desarrollo. 5121 lo fija launchSettings.json, que es un archivo de Visual
# Studio y NO viaja en la imagen publicada.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publicado ./

# Donde se guarda la clave de firma generada. Ver `entrypoint.sh`.
#
# El usuario `app` (uid 1654) viene en la imagen de Microsoft y es con el que se
# ejecuta: un proceso que solo tiene que leer sus ensamblados no necesita ser
# root, y si alguien encuentra un fallo en la API, la diferencia entre root y no
# root es la diferencia entre un susto y un contenedor entero comprometido.
RUN mkdir -p /var/lib/colorsin && chown app:app /var/lib/colorsin
VOLUME ["/var/lib/colorsin"]

COPY --chmod=0755 entrypoint.sh /usr/local/bin/entrypoint.sh

USER app
ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
