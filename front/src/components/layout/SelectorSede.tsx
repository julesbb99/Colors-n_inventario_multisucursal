import { Lock, MapPin } from 'lucide-react';
import { useSede } from '../../hooks/useSede';

/** Valor del `<option>` que representa "sin filtro". Un `<select>` solo maneja cadenas. */
const VALOR_TODAS = 'todas';

/**
 * El selector de sede de la cabecera.
 *
 * ES EL CONTROL MAS IMPORTANTE DE LA INTERFAZ: casi toda consulta se acota por
 * el. Su comportamiento depende del rol:
 *
 *   AdminGeneral      desplegable con "Todas las sedes" y cada sede.
 *   Gerente/Operador  deshabilitado y fijo en la sede de su token, con un
 *                     candado que explica por que no se puede tocar.
 *
 * Un control deshabilitado y sin explicacion se lee como un fallo; por eso el
 * candado lleva `title` y un texto para lectores de pantalla.
 */
export function SelectorSede() {
  const { sucursales, sedeActiva, nombreSedeActiva, puedeCambiarSede, cambiarSede, cargando } =
    useSede();

  if (!puedeCambiarSede) {
    return (
      <div
        className="flex h-11 items-center gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3"
        title="Tu sesion esta asignada a esta sede y no se puede cambiar."
      >
        <MapPin size={16} className="shrink-0 text-slate-500" aria-hidden="true" />
        <span className="text-sm font-semibold text-slate-700">
          {cargando ? 'Cargando…' : nombreSedeActiva}
        </span>
        <Lock size={14} className="shrink-0 text-slate-400" aria-hidden="true" />
        <span className="sr-only">
          Sede fija segun tu rol. Solo la administracion general puede cambiar de sede.
        </span>
      </div>
    );
  }

  return (
    <div className="flex h-11 items-center gap-2 rounded-lg border border-slate-300 bg-white px-3 focus-within:ring-2 focus-within:ring-colorsin-500">
      <MapPin size={16} className="shrink-0 text-slate-500" aria-hidden="true" />
      <label htmlFor="selector-sede" className="sr-only">
        Sede por la que se filtran las consultas
      </label>
      <select
        id="selector-sede"
        className="cursor-pointer border-none bg-transparent pr-1 text-sm font-semibold text-slate-800 outline-none"
        value={sedeActiva === null ? VALOR_TODAS : String(sedeActiva)}
        disabled={cargando}
        onChange={(evento) => {
          const valor = evento.target.value;
          cambiarSede(valor === VALOR_TODAS ? null : Number(valor));
        }}
      >
        <option value={VALOR_TODAS}>Todas las sedes</option>
        {sucursales.map((sede) => (
          <option key={sede.id} value={sede.id}>
            {sede.nombre} — {sede.ciudad}
          </option>
        ))}
      </select>
    </div>
  );
}
