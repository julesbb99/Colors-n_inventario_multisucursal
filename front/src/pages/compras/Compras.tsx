import { useMemo, useState } from 'react';
import { Building2, PackageCheck, Pencil, Plus, XCircle } from 'lucide-react';
import { useAuth } from '../../hooks/useAuth';
import { useSede } from '../../hooks/useSede';
import { useConsulta } from '../../hooks/useConsulta';
import { useEnvio } from '../../hooks/useEnvio';
import { cancelarOrdenCompra, obtenerOrdenesCompra } from '../../services/compras';
import { ETIQUETA_ESTADO_ORDEN, esBorrador } from '../../models/compras';
import type { EstadoOrdenCompra, OrdenCompraDto } from '../../models/compras';
import { GestionProveedores } from '../../components/compras/GestionProveedores';
import { Modal } from '../../components/ui/Modal';
import { Spinner } from '../../components/ui/Spinner';
import { DataTable } from '../../components/ui/DataTable';
import type { ColumnaTabla } from '../../components/ui/DataTable';
import { StatusBadge } from '../../components/ui/StatusBadge';
import type { EstadoBadge } from '../../components/ui/StatusBadge';
import { Alerta } from '../../components/ui/Alerta';
import { Boton } from '../../components/ui/Boton';
import { FormularioOrdenCompra } from '../../components/compras/FormularioOrdenCompra';
import { FormularioRecepcion } from '../../components/compras/FormularioRecepcion';
import { formatearCOP, formatearEntero, formatearFechaSolo } from '../../utils/formato';

const ESTADO_BADGE: Record<EstadoOrdenCompra, EstadoBadge> = {
  Pendiente: 'neutral',
  Confirmada: 'activo',
  ParcialmenteRecibida: 'advertencia',
  Recibida: 'exitoso',
  Cancelada: 'inactivo',
};

type Pestana = 'todas' | 'pendientes' | 'parciales' | 'recibidas' | 'canceladas';

const PESTANAS: { id: Pestana; etiqueta: string }[] = [
  { id: 'todas', etiqueta: 'Todas' },
  { id: 'pendientes', etiqueta: 'Por recibir' },
  { id: 'parciales', etiqueta: 'Parcialmente recibidas' },
  { id: 'recibidas', etiqueta: 'Recibidas' },
  { id: 'canceladas', etiqueta: 'Canceladas' },
];

const SIN_DATOS: OrdenCompraDto[] = [];

interface AccionesFila {
  onRecibir: ((orden: OrdenCompraDto) => void) | null;
  onEditar: (orden: OrdenCompraDto) => void;
  onRetirar: (orden: OrdenCompraDto) => void;
  /** Si quien mira puede modificar ESA orden. Se decide por fila, por la sede. */
  puedeModificar: (orden: OrdenCompraDto) => boolean;
}

/**
 * Las columnas dependen del rol: la de acciones solo existe para supervisión.
 * Por eso se construyen en una función y no en una constante de módulo.
 */
function construirColumnas(acciones: AccionesFila): ColumnaTabla<OrdenCompraDto>[] {
  const { onRecibir, onEditar, onRetirar, puedeModificar } = acciones;
  const columnas: ColumnaTabla<OrdenCompraDto>[] = [
  {
    id: 'numero',
    header: 'Orden',
    render: (o) => <span className="font-semibold tabular-nums text-slate-900">{o.id}</span>,
  },
  { id: 'proveedor', header: 'Proveedor', accessor: 'proveedorNombre' },
  { id: 'sede', header: 'Sede', accessor: 'sucursalNombre' },
  {
    id: 'fecha',
    header: 'Fecha',
    align: 'derecha',
    render: (o) => (
      <span className="tabular-nums text-slate-600">{formatearFechaSolo(o.fecha)}</span>
    ),
  },
  {
    id: 'lineas',
    header: 'Líneas',
    align: 'derecha',
    render: (o) => (
      // Lo que importa de una orden abierta no es cuántas líneas tiene sino
      // cuántas siguen esperando mercancía.
      // `lineasTotales` y NO `detalles.length`: el listado no trae las líneas,
      // así que contarlas ahí daba siempre cero y la celda decía «1 de 0».
      <span className="tabular-nums text-slate-600">
        {o.lineasPendientes > 0 ? (
          <span className="font-semibold text-amber-700">
            {formatearEntero(o.lineasPendientes)} de {formatearEntero(o.lineasTotales)}
          </span>
        ) : (
          formatearEntero(o.lineasTotales)
        )}
      </span>
    ),
  },
  {
    id: 'total',
    header: 'Total',
    align: 'derecha',
    render: (o) => (
      <span className="font-semibold tabular-nums text-slate-900">{formatearCOP(o.total)}</span>
    ),
  },
  { id: 'usuario', header: 'Registró', accessor: 'usuarioNombre' },
  {
    id: 'estado',
    header: 'Estado',
    align: 'centro',
    render: (o) =>
      o.estado === null ? (
        <StatusBadge estado="neutral" />
      ) : (
        <StatusBadge estado={ESTADO_BADGE[o.estado]}>
          {ETIQUETA_ESTADO_ORDEN[o.estado]}
        </StatusBadge>
      ),
  },
  ];

  columnas.push({
    id: 'acciones',
    header: '',
    align: 'derecha',
    ancho: 'w-64',
    render: (orden) => {
      // Editar y retirar solo mientras sea un BORRADOR. Desde 'Confirmada' hay
      // un compromiso con el proveedor y, si entró mercancía, movimientos en el
      // libro mayor que citan esta orden. La API responde 409; esto solo evita
      // ofrecer el botón.
      const borrador = esBorrador(orden.estado) && puedeModificar(orden);

      // Recibir solo tiene sentido con líneas pendientes: una ya recibida no
      // admite otra recepción y el botón solo llevaría a un 409.
      const puedeRecibir = onRecibir !== null && orden.lineasPendientes > 0;

      if (!borrador && !puedeRecibir) {
        return null;
      }

      return (
        <span className="flex items-center justify-end gap-1">
          {borrador ? (
            <>
              <button
                type="button"
                onClick={() => onEditar(orden)}
                title="Editar la orden"
                aria-label={`Editar la orden ${orden.id}`}
                className="rounded-md p-1.5 text-slate-500 transition hover:bg-slate-100 hover:text-slate-800"
              >
                <Pencil size={16} aria-hidden="true" />
              </button>
              {/*
                Botón con la palabra "Cancelar", no un bote de basura. El icono
                de basura promete un borrado que no ocurre: la orden pasa a
                'Cancelada' y sigue ahí, en su pestaña. El rótulo dice lo mismo
                que esa pestaña, que es lo que hace entendible el resultado.
              */}
              <button
                type="button"
                onClick={() => onRetirar(orden)}
                title="Cancelar la orden. Queda como Cancelada, no se borra."
                aria-label={`Cancelar la orden ${orden.id}`}
                className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3 text-xs font-semibold text-slate-700 transition hover:border-terracota-400 hover:bg-terracota-50 hover:text-terracota-700"
              >
                <XCircle size={15} aria-hidden="true" />
                Cancelar
              </button>
            </>
          ) : null}

          {puedeRecibir ? (
            <button
              type="button"
              onClick={() => onRecibir!(orden)}
              title="Registrar recepción"
              className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3 text-xs font-semibold text-slate-700 transition hover:border-terracota-400 hover:text-terracota-700"
            >
              <PackageCheck size={15} aria-hidden="true" />
              Recibir
            </button>
          ) : null}
        </span>
      );
    },
  });

  return columnas;
}

export function Compras() {
  const { sedeActiva, nombreSedeActiva } = useSede();
  const { esSupervision, esAdminGeneral, sucursalId: sedePropia } = useAuth();
  const [pestana, setPestana] = useState<Pestana>('todas');
  const [formulario, setFormulario] = useState<{ orden: OrdenCompraDto | null } | null>(null);
  const [recibiendo, setRecibiendo] = useState<OrdenCompraDto | null>(null);
  const [aRetirar, setARetirar] = useState<OrdenCompraDto | null>(null);
  const [proveedoresAbierto, setProveedoresAbierto] = useState(false);

  const {
    datos: ordenes,
    cargando,
    error,
    esPermisos,
    recargar,
  } = useConsulta(() => obtenerOrdenesCompra(sedeActiva), [sedeActiva], SIN_DATOS);

  const { enviando, error: errorAccion, esPermisos: accionEsPermisos, enviar } = useEnvio();

  /**
   * Si quien mira puede modificar ESA orden.
   *
   * Por sede, no por rol: crear y editar una orden Pendiente está abierto a
   * cualquier rol -es un borrador- pero solo en la sede propia. No es el
   * control: la API comprueba lo mismo.
   */
  const puedeModificar = useMemo(
    () => (orden: OrdenCompraDto) => esAdminGeneral || orden.sucursalId === sedePropia,
    [esAdminGeneral, sedePropia],
  );

  const columnas = useMemo(
    () =>
      construirColumnas({
        onRecibir: esSupervision ? setRecibiendo : null,
        onEditar: (orden) => setFormulario({ orden }),
        onRetirar: setARetirar,
        puedeModificar,
      }),
    [esSupervision, puedeModificar],
  );

  /**
   * «Por recibir» y «Parcialmente recibidas» son EXCLUYENTES.
   *
   * Antes «Por recibir» era `lineasPendientes > 0`, y una orden que llegó a
   * medias cumple eso: salía en las dos listas a la vez, contada dos veces.
   * Ahora «Por recibir» son las que no han recibido NADA todavía, y la otra
   * pestaña son las que llegaron cortas y esperan el resto.
   */
  const esParcial = (o: OrdenCompraDto) => o.estado === 'ParcialmenteRecibida';
  const esPorRecibir = (o: OrdenCompraDto) => !esParcial(o) && o.lineasPendientes > 0;

  const conteos = useMemo(
    () => ({
      // "Todas" excluye las canceladas: una orden retirada no forma parte del
      // trabajo del día, tiene su propia pestaña. Mismo criterio que las
      // existencias deshabilitadas.
      todas: ordenes.filter((o) => o.estado !== 'Cancelada').length,
      pendientes: ordenes.filter((o) => o.estado !== 'Cancelada' && esPorRecibir(o)).length,
      parciales: ordenes.filter((o) => esParcial(o)).length,
      recibidas: ordenes.filter((o) => o.estado === 'Recibida').length,
      canceladas: ordenes.filter((o) => o.estado === 'Cancelada').length,
    }),
    [ordenes],
  );

  const filtradas = useMemo(() => {
    if (pestana === 'canceladas') {
      return ordenes.filter((o) => o.estado === 'Cancelada');
    }

    const vigentes = ordenes.filter((o) => o.estado !== 'Cancelada');

    if (pestana === 'pendientes') {
      return vigentes.filter(esPorRecibir);
    }
    if (pestana === 'parciales') {
      return vigentes.filter(esParcial);
    }
    if (pestana === 'recibidas') {
      return vigentes.filter((o) => o.estado === 'Recibida');
    }
    return vigentes;
  }, [ordenes, pestana]);

  const VACIOS: Record<Pestana, string> = {
    todas: 'No hay órdenes de compra registradas para esta sede.',
    pendientes: 'No hay órdenes esperando su primera entrega.',
    parciales: 'Ninguna orden llegó corta.',
    recibidas: 'Ninguna orden se ha recibido todavía.',
    canceladas: 'No hay órdenes retiradas.',
  };

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
        <p className="min-w-0 flex-1 text-sm text-slate-500">
          {nombreSedeActiva} · una orden no mueve stock: eso lo hace su recepción
        </p>

        {/* El catálogo de proveedores y sus precios son de toda la red: solo el
            Administrador General los toca, igual que en la API. */}
        {esAdminGeneral ? (
          <Boton variante="secundaria" onClick={() => setProveedoresAbierto(true)}>
            <Building2 size={17} aria-hidden="true" />
            Proveedores
          </Boton>
        ) : null}

        {/* Abierto a cualquier rol: una orden Pendiente es un borrador. Lo que
            obliga a la empresa es confirmarla o recibirla, y eso sigue siendo
            de supervisión. */}
        <Boton onClick={() => setFormulario({ orden: null })}>
          <Plus size={17} aria-hidden="true" />
          Nueva orden
        </Boton>
      </div>

      {formulario ? (
        <FormularioOrdenCompra
          orden={formulario.orden}
          onCerrar={() => setFormulario(null)}
          onCreada={recargar}
        />
      ) : null}

      {proveedoresAbierto ? (
        <GestionProveedores onCerrar={() => setProveedoresAbierto(false)} />
      ) : null}

      {aRetirar ? (
        <Modal
          titulo={`Cancelar la orden #${aRetirar.id}`}
          descripcion={`${aRetirar.proveedorNombre} — ${aRetirar.sucursalNombre}`}
          ocupado={enviando}
          onCerrar={() => setARetirar(null)}
          pie={
            <>
              {/*
                "Volver" y no "Cancelar": el botón de al lado ya cancela la
                ORDEN, y dos botones que dicen «Cancelar» uno junto al otro, con
                significados opuestos, es la forma más fácil de que alguien
                cancele lo que no quería.
              */}
              <Boton variante="secundaria" onClick={() => setARetirar(null)} disabled={enviando}>
                Volver
              </Boton>
              <Boton
                onClick={() => {
                  const orden = aRetirar;
                  setARetirar(null);
                  void enviar(async () => {
                    await cancelarOrdenCompra(orden.id);
                    recargar();
                  });
                }}
                disabled={enviando}
              >
                {enviando ? (
                  <>
                    <Spinner etiqueta="Cancelando" />
                    Cancelando…
                  </>
                ) : (
                  'Cancelar la orden'
                )}
              </Boton>
            </>
          }
        >
          <div className="flex flex-col gap-3 text-sm text-slate-600">
            <p>
              La orden pasa a <strong className="font-semibold text-slate-900">Cancelada</strong> y
              sale del listado del día. <strong className="font-semibold">No se borra</strong>: se
              conserva con su detalle en la pestaña «Canceladas», que es lo que permite responder
              después a quién pidió esto y por qué no llegó.
            </p>
            <p className="rounded-lg bg-slate-50 px-3 py-2">
              Solo se puede mientras siga Pendiente. Una vez confirmada o recibida, la orden es el
              documento que respalda lo que entró a la bodega.
            </p>
          </div>
        </Modal>
      ) : null}

      {errorAccion ? (
        <Alerta tipo={accionEsPermisos ? 'permisos' : 'error'}>{errorAccion}</Alerta>
      ) : null}

      {recibiendo ? (
        <FormularioRecepcion
          orden={recibiendo}
          onCerrar={() => setRecibiendo(null)}
          onRecibida={recargar}
        />
      ) : null}

      {error ? (
        <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta>
      ) : (
        <DataTable
          columnas={columnas}
          data={filtradas}
          claveFila={(o) => o.id}
          cargando={cargando}
          estadoVacio={VACIOS[pestana]}
        />
      )}
    </div>
  );
}
