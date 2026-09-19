import { useEffect, useMemo, useState } from 'react';
import { Plus, Search } from 'lucide-react';
import { useAuth } from '../../hooks/useAuth';
import { useSede } from '../../hooks/useSede';
import { useCatalogos } from '../../hooks/useCatalogos';
import { useConsulta } from '../../hooks/useConsulta';
import { useEnvio } from '../../hooks/useEnvio';
import {
  desactivarExistencia,
  obtenerExistencias,
  reactivarExistencia,
} from '../../services/inventario';
import { estadoExistencia } from '../../models/inventario';
import type { ExistenciaDto } from '../../models/inventario';
import { TablaExistencias } from '../../components/inventario/TablaExistencias';
import { FormularioMovimiento } from '../../components/inventario/FormularioMovimiento';
import { FormularioExistencia } from '../../components/inventario/FormularioExistencia';
import { Alerta } from '../../components/ui/Alerta';
import { Boton } from '../../components/ui/Boton';
import { Modal } from '../../components/ui/Modal';
import { Spinner } from '../../components/ui/Spinner';
import { formatearVolumen } from '../../utils/formato';

type Pestana = 'todas' | 'alerta' | 'agotadas' | 'deshabilitadas';

const PESTANAS: { id: Pestana; etiqueta: string }[] = [
  { id: 'todas', etiqueta: 'Todas' },
  { id: 'alerta', etiqueta: 'En alerta' },
  { id: 'agotadas', etiqueta: 'Agotadas' },
  { id: 'deshabilitadas', etiqueta: 'Deshabilitadas' },
];

const SIN_DATOS: ExistenciaDto[] = [];

/** Una unidad del catálogo que sí sirve para convertir, con el factor ya resuelto. */
interface UnidadVolumen {
  id: number;
  nombre: string;
  simbolo: string;
  factor: number;
}

export function Existencias() {
  const { sedeActiva, sucursales } = useSede();
  const { esSupervision, esAdminGeneral, sucursalId: sedePropia } = useAuth();
  const { unidades } = useCatalogos();

  const [pestana, setPestana] = useState<Pestana>('todas');
  const [busqueda, setBusqueda] = useState('');
  const [abierto, setAbierto] = useState(false);
  const [unidadElegida, setUnidadElegida] = useState<number | null>(null);

  // ESTA PANTALLA TIENE SU PROPIO FILTRO DE SEDE, aparte del selector de la
  // barra superior. El de arriba sigue mandando en el resto de la aplicación,
  // donde la API SÍ aísla por sede y pedir una ajena da 403; aquí, en cambio,
  // cualquier rol puede mirar cualquier sede, y hacía falta un control que lo
  // permitiera sin desbloquear los demás módulos.
  //
  // Arranca en la sede de la barra y se vuelve a sincronizar si esa cambia, para
  // que al entrar desde otra pantalla se vea lo que se venía mirando.
  const [sedeFiltro, setSedeFiltro] = useState<number | null>(sedeActiva);

  useEffect(() => {
    setSedeFiltro(sedeActiva);
  }, [sedeActiva]);

  // Se piden SIEMPRE con las inactivas y se reparten en memoria. El catálogo de
  // esta red son tres productos por tres sedes, así que cambiar de pestaña no
  // tiene por qué costar un viaje al servidor.
  const {
    datos: existencias,
    cargando,
    error,
    esPermisos,
    recargar,
  } = useConsulta(() => obtenerExistencias(sedeFiltro, true), [sedeFiltro], SIN_DATOS);

  const { enviando, error: errorAccion, esPermisos: accionEsPermisos, enviar } = useEnvio();

  const [formulario, setFormulario] = useState<{ fila: ExistenciaDto | null } | null>(null);
  const [aDeshabilitar, setADeshabilitar] = useState<ExistenciaDto | null>(null);
  const [filaOcupada, setFilaOcupada] = useState<number | null>(null);

  /**
   * Si quien mira puede TOCAR esa fila.
   *
   * Ver es de todos; escribir sigue acotado a la sede propia. No es el control
   * -la API comprueba lo mismo- pero evita ofrecer un botón que daría 403.
   */
  function puedeEditarFila(fila: ExistenciaDto): boolean {
    return esSupervision && (esAdminGeneral || fila.sucursalId === sedePropia);
  }

  /** La sede que se está mirando, ¿es una en la que se puede dar de alta? */
  const puedeCrearAqui =
    esSupervision &&
    (esAdminGeneral || (sedeFiltro !== null && sedeFiltro === sedePropia) || sedeFiltro === null);

  const unidadesVolumen = useMemo<UnidadVolumen[]>(
    () =>
      unidades
        .flatMap((unidad) =>
          unidad.factorConversionLitros === null
            ? []
            : [
                {
                  id: unidad.id,
                  nombre: unidad.nombre,
                  simbolo: unidad.simbolo,
                  factor: unidad.factorConversionLitros,
                },
              ],
        )
        .sort((a, b) => a.factor - b.factor),
    [unidades],
  );

  const unidadPorDefecto =
    unidadesVolumen.find((unidad) => unidad.factor === 1)?.id ?? unidadesVolumen[0]?.id ?? null;
  const unidadActiva = unidadElegida ?? unidadPorDefecto;

  // Contadores y pestañas usan `estadoExistencia`, la MISMA función que decide la
  // insignia de la tabla. Antes cada uno tenía su criterio y un producto agotado
  // salía también en "En alerta", contado dos veces.
  const conteos = useMemo(() => {
    let alerta = 0;
    let agotadas = 0;
    let deshabilitadas = 0;
    let activas = 0;

    for (const fila of existencias) {
      switch (estadoExistencia(fila)) {
        case 'deshabilitado':
          deshabilitadas += 1;
          break;
        case 'agotado':
          agotadas += 1;
          activas += 1;
          break;
        case 'alerta':
          alerta += 1;
          activas += 1;
          break;
        default:
          activas += 1;
      }
    }

    return { todas: activas, alerta, agotadas, deshabilitadas };
  }, [existencias]);

  const filtradas = useMemo(() => {
    const porPestana = existencias.filter((fila) => {
      const estado = estadoExistencia(fila);

      if (pestana === 'deshabilitadas') {
        return estado === 'deshabilitado';
      }
      // Las dadas de baja NO se mezclan con el listado del día: tienen su
      // pestaña. Sin esto reaparecerían en "Todas" como una fila más.
      if (estado === 'deshabilitado') {
        return false;
      }
      if (pestana === 'alerta') {
        return estado === 'alerta';
      }
      if (pestana === 'agotadas') {
        return estado === 'agotado';
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

  // El mensaje de la tabla vacía cambia con la pestaña. Uno genérico -"no hay
  // existencias para esta sede"- es engañoso en "Deshabilitadas", donde lo
  // normal es no tener ninguna y eso es una buena noticia, no un vacío de datos.
  const VACIOS: Record<Pestana, string> = {
    todas: 'No hay existencias registradas para esta sede.',
    alerta: 'Ningún producto está por debajo de su mínimo de reposición.',
    agotadas: 'Ningún producto está agotado.',
    deshabilitadas: 'No hay existencias deshabilitadas.',
  };

  function ejecutar(fila: ExistenciaDto, accion: (id: number) => Promise<unknown>) {
    setFilaOcupada(fila.id);
    void enviar(async () => {
      await accion(fila.id);
      recargar();
    }).finally(() => setFilaOcupada(null));
  }

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
        <div className="flex items-center gap-2">
          <label htmlFor="sede-existencias" className="text-sm font-medium text-slate-500">
            Sede
          </label>
          <select
            id="sede-existencias"
            value={sedeFiltro === null ? '' : String(sedeFiltro)}
            onChange={(evento) =>
              setSedeFiltro(evento.target.value === '' ? null : Number(evento.target.value))
            }
            className="h-11 cursor-pointer rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 outline-none transition focus:border-petroleo-500"
          >
            <option value="">Todas las sedes</option>
            {sucursales.map((sede) => (
              <option key={sede.id} value={sede.id}>
                {sede.nombre}
              </option>
            ))}
          </select>
        </div>

        {/* Cambiar de unidad NO cambia ningún dato: el saldo se guarda y se
            compara con el mínimo en la unidad base. Es una lente sobre la misma
            cifra, y por eso el control vive en la pantalla y no en el filtro que
            viaja a la API. */}
        {unidadesVolumen.length > 1 ? (
          <div className="flex items-center gap-2">
            <span id="rotulo-unidad" className="text-sm font-medium text-slate-500">
              Ver en
            </span>
            <div
              role="group"
              aria-labelledby="rotulo-unidad"
              className="flex h-11 items-center gap-1 rounded-lg border border-slate-300 bg-white p-1"
            >
              {unidadesVolumen.map((unidad) => {
                const activa = unidad.id === unidadActiva;

                return (
                  <button
                    key={unidad.id}
                    type="button"
                    onClick={() => setUnidadElegida(unidad.id)}
                    aria-pressed={activa}
                    // El botón muestra el símbolo, que es corto; el nombre
                    // completo va en el título y en la etiqueta accesible,
                    // porque "cn5" no se entiende solo.
                    title={unidad.nombre}
                    aria-label={unidad.nombre}
                    className={`h-9 rounded-md px-3 text-sm font-semibold transition ${
                      activa
                        ? 'bg-petroleo-800 text-white'
                        : 'text-slate-600 hover:bg-slate-100'
                    }`}
                  >
                    {unidad.simbolo}
                  </button>
                );
              })}
            </div>
          </div>
        ) : null}

        <div className="flex h-11 min-w-0 flex-1 items-center gap-2 rounded-lg border border-slate-300 bg-white px-3 focus-within:border-petroleo-500 sm:min-w-[16rem]">
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

        {puedeCrearAqui ? (
          <Boton variante="secundaria" onClick={() => setFormulario({ fila: null })}>
            <Plus size={17} aria-hidden="true" />
            Nueva existencia
          </Boton>
        ) : null}

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

      {formulario ? (
        <FormularioExistencia
          existencia={formulario.fila}
          sedeSugerida={sedeFiltro}
          onCerrar={() => setFormulario(null)}
          onGuardado={recargar}
        />
      ) : null}

      {aDeshabilitar ? (
        <Modal
          titulo="Deshabilitar existencia"
          descripcion={`${aDeshabilitar.productoNombre} — ${aDeshabilitar.sucursalNombre}`}
          ocupado={enviando}
          onCerrar={() => setADeshabilitar(null)}
          pie={
            <>
              <Boton
                variante="secundaria"
                onClick={() => setADeshabilitar(null)}
                disabled={enviando}
              >
                Cancelar
              </Boton>
              <Boton
                onClick={() => {
                  const fila = aDeshabilitar;
                  setADeshabilitar(null);
                  ejecutar(fila, desactivarExistencia);
                }}
                disabled={enviando}
              >
                {enviando ? (
                  <>
                    <Spinner etiqueta="Deshabilitando" />
                    Deshabilitando…
                  </>
                ) : (
                  'Deshabilitar'
                )}
              </Boton>
            </>
          }
        >
          <div className="flex flex-col gap-3 text-sm text-slate-600">
            <p>
              El producto deja de listarse y de alertar en esa sede.{' '}
              <strong className="font-semibold text-slate-900">No se borra nada</strong>: la fila
              conserva su saldo, sus lotes y todo el libro mayor, y se puede volver a habilitar
              desde la pestaña «Deshabilitadas».
            </p>
            <p className="rounded-lg bg-slate-50 px-3 py-2">
              Saldo actual:{' '}
              <span className="font-semibold tabular-nums text-slate-900">
                {formatearVolumen(aDeshabilitar.cantidadBase, aDeshabilitar.unidadBaseSimbolo)}
              </span>
              {aDeshabilitar.cantidadBase !== 0 ? (
                <>
                  {' '}
                  — el servidor va a rechazarlo: primero hay que sacar esa mercancía con un
                  traslado, una venta o un ajuste, para que el movimiento quede explicado.
                </>
              ) : null}
            </p>
          </div>
        </Modal>
      ) : null}

      {errorAccion ? (
        <Alerta tipo={accionEsPermisos ? 'permisos' : 'error'}>{errorAccion}</Alerta>
      ) : null}

      {error ? (
        <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta>
      ) : (
        <TablaExistencias
          existencias={filtradas}
          cargando={cargando}
          unidadDestinoId={unidadActiva}
          puedeEditar={puedeEditarFila}
          onEditar={(fila) => setFormulario({ fila })}
          onDesactivar={setADeshabilitar}
          onReactivar={(fila) => ejecutar(fila, reactivarExistencia)}
          filaOcupada={filaOcupada}
          estadoVacio={VACIOS[pestana]}
        />
      )}
    </div>
  );
}
