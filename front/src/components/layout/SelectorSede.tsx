import { ChevronDown, Lock, MapPin } from 'lucide-react';
import { useSede } from '../../hooks/useSede';

/** Valor del `<option>` que representa "sin filtro". Un `<select>` solo maneja cadenas. */
const VALOR_TODAS = 'todas';

/**
 * El selector de sede de la barra superior.
 *
 * ES EL CONTROL MAS IMPORTANTE DE LA INTERFAZ: casi toda consulta se acota por
 * el. Su comportamiento depende del rol:
 *
 *   AdminGeneral      desplegable con "Todas las sedes" y cada sede.
 *   Gerente/Operador  fijo en la sede de su token, con un candado.
 *
 * ESTO NO ES UN CONTROL DE SEGURIDAD. Quien de verdad impide ver otra sede es la
 * API, que responde 403 si el `sucursalId` de la consulta no coincide con el del
 * token. Bloquear el selector solo evita que alguien se tope con ese 403 sin
 * entender por que; quien edite la peticion a mano sigue chocando con el servidor.
 *
 * Va sobre el verde petroleo de la barra, asi que todo el color es claro sobre
 * oscuro. Un control deshabilitado sin explicacion se lee como un fallo: por eso
 * el candado lleva `title` y un texto para lectores de pantalla.
 */
export function SelectorSede() {
  const { sucursales, sedeActiva, nombreSedeActiva, puedeCambiarSede, cambiarSede, cargando } =
    useSede();

  if (!puedeCambiarSede) {
    return (
      <div
        className="flex h-10 items-center gap-2 rounded-lg border border-white/15 bg-white/5 px-3"
        title="Tu sesión está asignada a esta sede y no se puede cambiar."
      >
        <MapPin size={15} className="shrink-0 text-petroleo-200" aria-hidden="true" />
        <span className="max-w-[13rem] truncate text-sm font-semibold text-white">
          {cargando ? 'Cargando…' : nombreSedeActiva}
        </span>
        <Lock size={13} className="shrink-0 text-petroleo-300" aria-hidden="true" />
        <span className="sr-only">
          Sede fija según tu rol. Solo la administración general puede cambiar de sede.
        </span>
      </div>
    );
  }

  return (
    <div className="flex h-10 items-center gap-2 rounded-lg border border-white/20 bg-white/10 px-3 transition focus-within:border-terracota-400 hover:bg-white/15">
      <MapPin size={15} className="shrink-0 text-petroleo-200" aria-hidden="true" />

      <label htmlFor="selector-sede" className="sr-only">
        Sede por la que se filtran las consultas
      </label>

      {/*
        `appearance-none` quita la flecha nativa, que en un fondo oscuro se pinta
        con el color del sistema y suele salir casi invisible. La flecha propia va
        al lado, marcada como decorativa.

        Las `<option>` llevan color explicito: la lista desplegada la dibuja el
        sistema operativo y no hereda el fondo del `<select>`, asi que sin esto
        saldria texto blanco sobre fondo blanco en varios navegadores.
      */}
      <select
        id="selector-sede"
        className="cursor-pointer appearance-none border-none bg-transparent pr-1 text-sm font-semibold text-white outline-none disabled:cursor-wait"
        value={sedeActiva === null ? VALOR_TODAS : String(sedeActiva)}
        disabled={cargando}
        onChange={(evento) => {
          const valor = evento.target.value;
          cambiarSede(valor === VALOR_TODAS ? null : Number(valor));
        }}
      >
        <option value={VALOR_TODAS} className="bg-petroleo-800 text-white">
          Todas las sedes
        </option>
        {sucursales.map((sede) => (
          <option key={sede.id} value={sede.id} className="bg-petroleo-800 text-white">
            {sede.nombre} — {sede.ciudad}
          </option>
        ))}
      </select>

      <ChevronDown size={15} className="shrink-0 text-petroleo-200" aria-hidden="true" />
    </div>
  );
}
