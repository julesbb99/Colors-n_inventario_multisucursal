import { useEffect, useMemo, useState } from 'react';
import { Search } from 'lucide-react';
import { useSede } from '../../hooks/useSede';
import { obtenerExistencias } from '../../services/api';
import { esApiError, mensajeDeError } from '../../models/api';
import type { ExistenciaDto } from '../../models/inventario';
import { TablaExistencias } from '../../components/inventario/TablaExistencias';
import { Alerta } from '../../components/ui/Alerta';

type Pestana = 'todas' | 'alerta' | 'agotadas';

const PESTANAS: { id: Pestana; etiqueta: string }[] = [
  { id: 'todas', etiqueta: 'Todas' },
  { id: 'alerta', etiqueta: 'En alerta' },
  { id: 'agotadas', etiqueta: 'Agotadas' },
];

export function Existencias() {
  const { sedeActiva, nombreSedeActiva } = useSede();

  const [existencias, setExistencias] = useState<ExistenciaDto[]>([]);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [esPermisos, setEsPermisos] = useState(false);
  const [pestana, setPestana] = useState<Pestana>('todas');
  const [busqueda, setBusqueda] = useState('');

  useEffect(() => {
    let vigente = true;

    setCargando(true);
    setError(null);
    setEsPermisos(false);

    obtenerExistencias(sedeActiva)
      .then((datos) => {
        if (vigente) {
          setExistencias(datos);
        }
      })
      .catch((fallo: unknown) => {
        if (!vigente) {
          return;
        }
        setError(mensajeDeError(fallo));
        setEsPermisos(esApiError(fallo) && fallo.esPermisos);
        setExistencias([]);
      })
      .finally(() => {
        if (vigente) {
          setCargando(false);
        }
      });

    return () => {
      vigente = false;
    };
  }, [sedeActiva]);

  // El filtrado es en memoria a proposito: el catalogo de esta red son tres
  // productos por tres sedes. El dia que crezca, esto pasa a ser parametros de
  // la consulta; hacerlo ahora seria complicar el servidor sin motivo.
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
                // El subrayado activo es terracota, igual que el borde izquierdo
                // del menu lateral: el acento marca donde estas, en todas las
                // pantallas y con el mismo color.
                activa
                  ? 'border-terracota-500 font-bold text-slate-900'
                  : 'border-transparent font-medium text-slate-500 hover:text-slate-800'
              }`}
            >
              {etiqueta}
              <span
                className={`inline-flex min-w-[22px] items-center justify-center rounded-full px-1.5 py-0.5 text-[11px] font-bold tabular-nums ${
                  activa ? 'bg-colorsin-700 text-white' : 'bg-slate-200 text-slate-600'
                }`}
              >
                {conteos[id]}
              </span>
            </button>
          );
        })}
      </div>

      <div className="flex flex-wrap items-center gap-3">
        {/* Solo el alcance: el conteo lo lleva el pie de la tabla, y repetirlo
            aquí obliga a comprobar si las dos cifras coinciden. */}
        <p className="min-w-0 flex-1 text-sm text-slate-500">{nombreSedeActiva}</p>

        <div className="flex h-11 w-full items-center gap-2 rounded-lg border border-slate-300 bg-white px-3 focus-within:border-colorsin-500 focus-within:ring-2 focus-within:ring-colorsin-200 sm:w-80">
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
      </div>

      {/* La carga la pinta la propia tabla, para que los encabezados no
          desaparezcan y la pantalla no salte de altura al llegar los datos. */}
      {error ? (
        <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta>
      ) : (
        <TablaExistencias existencias={filtradas} cargando={cargando} />
      )}
    </div>
  );
}
