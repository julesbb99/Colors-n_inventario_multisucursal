import { useMemo, useState } from 'react';
import { Plus, Search } from 'lucide-react';
import { useAuth } from '../../hooks/useAuth';
import { useSede } from '../../hooks/useSede';
import { useConsulta } from '../../hooks/useConsulta';
import { obtenerExistencias } from '../../services/inventario';
import type { ExistenciaDto } from '../../models/inventario';
import { TablaExistencias } from '../../components/inventario/TablaExistencias';
import { FormularioMovimiento } from '../../components/inventario/FormularioMovimiento';
import { Alerta } from '../../components/ui/Alerta';
import { Boton } from '../../components/ui/Boton';

type Pestana = 'todas' | 'alerta' | 'agotadas';

const PESTANAS: { id: Pestana; etiqueta: string }[] = [
  { id: 'todas', etiqueta: 'Todas' },
  { id: 'alerta', etiqueta: 'En alerta' },
  { id: 'agotadas', etiqueta: 'Agotadas' },
];

const SIN_DATOS: ExistenciaDto[] = [];

export function Existencias() {
  const { sedeActiva, nombreSedeActiva } = useSede();
  const { esSupervision } = useAuth();
  const [pestana, setPestana] = useState<Pestana>('todas');
  const [busqueda, setBusqueda] = useState('');
  const [abierto, setAbierto] = useState(false);

  const {
    datos: existencias,
    cargando,
    error,
    esPermisos,
    recargar,
  } = useConsulta(() => obtenerExistencias(sedeActiva), [sedeActiva], SIN_DATOS);

  // El filtrado es en memoria a propósito: el catálogo de esta red son tres
  // productos por tres sedes, y el endpoint no acepta más filtro que la sede. El
  // día que crezca, esto pasa a ser parámetros de la consulta.
  const conteos = useMemo(
    () => ({
      todas: existencias.length,
      alerta: existencias.filter((f) => f.enAlerta).length,
      agotadas: existencias.filter((f) => f.cantidadBase <= 0).length,
    }),
    [existencias],
  );

  const filtradas = useMemo(() => {
    const porPestana = existencias.filter((fila) => {
      if (pestana === 'alerta') {
        return fila.enAlerta;
      }
      if (pestana === 'agotadas') {
        return fila.cantidadBase <= 0;
      }
      return true;
    });

    const texto = busqueda.trim().toLowerCase();
    if (texto === '') {
      return porPestana;
    }

    return porPestana.filter(
      (fila) =>
        fila.productoNombre.toLowerCase().includes(texto) ||
        fila.sucursalNombre.toLowerCase().includes(texto),
    );
  }, [existencias, pestana, busqueda]);

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center gap-3 border-b border-slate-200">
        {PESTANAS.map(({ id, etiqueta }) => {
          const activa = id === pestana;

          return (
            <button
              key={id}
              type="button"
              onClick={() => setPestana(id)}
              aria-current={activa ? 'page' : undefined}
              className={`-mb-px flex h-11 items-center gap-2 border-b-2 px-1 text-sm transition ${
                activa
                  ? 'border-terracota-500 font-bold text-slate-900'
                  : 'border-transparent font-medium text-slate-500 hover:text-slate-800'
              }`}
            >
              {etiqueta}
              <span
                className={`inline-flex min-w-[22px] items-center justify-center rounded-full px-1.5 py-0.5 text-[11px] font-bold tabular-nums ${
                  activa ? 'bg-petroleo-800 text-white' : 'bg-slate-200 text-slate-600'
                }`}
              >
                {conteos[id]}
              </span>
            </button>
          );
        })}
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <p className="min-w-0 flex-1 text-sm text-slate-500">{nombreSedeActiva}</p>

        <div className="flex h-11 w-full items-center gap-2 rounded-lg border border-slate-300 bg-white px-3 focus-within:border-petroleo-500 sm:w-80">
          <Search size={17} className="shrink-0 text-slate-400" aria-hidden="true" />
          <label htmlFor="buscar-existencias" className="sr-only">
            Buscar producto o sede
          </label>
          <input
            id="buscar-existencias"
            type="search"
            value={busqueda}
            onChange={(evento) => setBusqueda(evento.target.value)}
            placeholder="Buscar producto o sede"
            className="min-w-0 flex-1 border-none bg-transparent text-sm text-slate-900 outline-none placeholder:text-slate-400"
          />
        </div>

        {/* Un movimiento a mano es la única vía que cambia el stock sin un
            documento detrás: solo supervisión, igual que en la API. */}
        {esSupervision ? (
          <Boton onClick={() => setAbierto(true)}>
            <Plus size={17} aria-hidden="true" />
            Registrar movimiento
          </Boton>
        ) : null}
      </div>

      {abierto ? (
        <FormularioMovimiento onCerrar={() => setAbierto(false)} onRegistrado={recargar} />
      ) : null}

      {error ? (
        <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta>
      ) : (
        <TablaExistencias existencias={filtradas} cargando={cargando} />
      )}
    </div>
  );
}
