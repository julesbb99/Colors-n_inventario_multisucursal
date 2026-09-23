import { useMemo, useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { Modal } from '../ui/Modal';
import { Boton } from '../ui/Boton';
import { Alerta } from '../ui/Alerta';
import { CargandoPanel, Spinner } from '../ui/Spinner';
import { aNumero, aTexto, CampoNumero, CampoSelect } from '../ui/Campos';
import { CampoSede } from '../ui/CampoSede';
import { useSede } from '../../hooks/useSede';
import { useCatalogos } from '../../hooks/useCatalogos';
import { useConsulta } from '../../hooks/useConsulta';
import { useEnvio } from '../../hooks/useEnvio';
import {
  actualizarOrdenCompra,
  crearOrdenCompra,
  obtenerOrdenCompra,
  obtenerProveedores,
} from '../../services/compras';
import type {
  CrearLineaOrdenCompraDto,
  OrdenCompraDto,
  ProveedorDto,
} from '../../models/compras';
import { formatearCOP } from '../../utils/formato';
import { PanelPreciosReferencia } from './PanelPreciosReferencia';

interface LineaBorrador {
  clave: number;
  productoId: string;
  cantidad: string;
  unidadId: string;
  precioUnitario: string;
  descuento: string;
}

function lineaVacia(clave: number): LineaBorrador {
  return { clave, productoId: '', cantidad: '', unidadId: '', precioUnitario: '', descuento: '' };
}

const SIN_PROVEEDORES: ProveedorDto[] = [];

interface FormularioOrdenCompraProps {
  /**
   * La orden que se edita, o `null` para una nueva.
   *
   * Solo se puede pasar una orden en 'Pendiente': la API rechaza con 409
   * cualquier otro estado, y la pantalla ya no ofrece el botón.
   */
  orden?: OrdenCompraDto | null;
  onCerrar: () => void;
  onCreada: () => void;
}

/**
 * Al EDITAR hay que volver a pedir la orden por id.
 *
 * La que llega por parámetro viene del LISTADO, y el listado no trae las líneas
 * -serían cientos de filas que nadie mira-, así que su `detalles` está vacío. Si
 * el formulario se armara con eso, abriría con una línea en blanco y guardar
 * REEMPLAZARÍA las líneas reales por esa: el PUT sustituye, no parchea. Sería
 * pérdida de datos silenciosa.
 *
 * Por eso el formulario de verdad no se monta hasta tener la orden completa: sus
 * campos se inicializan una sola vez, al montarse.
 */
export function FormularioOrdenCompra({
  orden = null,
  onCerrar,
  onCreada,
}: FormularioOrdenCompraProps) {
  const { datos, cargando, error } = useConsulta(
    () => (orden === null ? Promise.resolve(null) : obtenerOrdenCompra(orden.id)),
    [orden?.id ?? 0],
    null as OrdenCompraDto | null,
  );

  if (orden === null) {
    return <FormularioOrdenInterno orden={null} onCerrar={onCerrar} onCreada={onCreada} />;
  }

  if (error !== null) {
    return (
      <Modal titulo={`Editar orden #${orden.id}`} onCerrar={onCerrar}>
        <Alerta tipo="error">{error}</Alerta>
      </Modal>
    );
  }

  if (cargando || datos === null) {
    return (
      <Modal titulo={`Editar orden #${orden.id}`} onCerrar={onCerrar}>
        <CargandoPanel texto="Cargando las líneas de la orden…" />
      </Modal>
    );
  }

  return <FormularioOrdenInterno orden={datos} onCerrar={onCerrar} onCreada={onCreada} />;
}

function FormularioOrdenInterno({
  orden = null,
  onCerrar,
  onCreada,
}: FormularioOrdenCompraProps) {
  const { sedeActiva } = useSede();
  const { opcionesProducto, opcionesUnidad, unidadBaseDe } = useCatalogos();
  const { enviando, error, esPermisos, enviar } = useEnvio();

  const esEdicion = orden !== null;

  // Con las inactivas incluidas SOLO al editar: si una orden vieja apunta a un
  // proveedor ya retirado, sin esto el selector saldría vacío y guardar la
  // orden la reasignaría en silencio a otro proveedor.
  const { datos: proveedores } = useConsulta(
    () => obtenerProveedores(esEdicion),
    [esEdicion],
    SIN_PROVEEDORES,
  );

  const [proveedorId, setProveedorId] = useState(orden ? String(orden.proveedorId) : '');
  const [sucursalId, setSucursalId] = useState(
    orden ? String(orden.sucursalId) : sedeActiva === null ? '' : String(sedeActiva),
  );
  const [plazoPagoDias, setPlazoPagoDias] = useState(aTexto(orden?.plazoPagoDias));
  const [lineas, setLineas] = useState<LineaBorrador[]>(
    orden && orden.detalles.length > 0
      ? orden.detalles.map((detalle, indice) => ({
          clave: indice,
          productoId: String(detalle.productoId),
          cantidad: aTexto(detalle.cantidad),
          unidadId: String(detalle.unidadId),
          precioUnitario: aTexto(detalle.precioUnitario),
          // El cero se muestra vacío: «sin descuento» se lee mejor en blanco que
          // como un 0 que parece escrito a mano.
          descuento: detalle.descuento === 0 ? '' : aTexto(detalle.descuento),
        }))
      : [lineaVacia(0)],
  );
  const [siguienteClave, setSiguienteClave] = useState(
    orden && orden.detalles.length > 0 ? orden.detalles.length : 1,
  );
  const [validacion, setValidacion] = useState<string | null>(null);

  function actualizarLinea(clave: number, cambios: Partial<LineaBorrador>) {
    setLineas((previas) =>
      previas.map((linea) => (linea.clave === clave ? { ...linea, ...cambios } : linea)),
    );
  }

  function elegirProducto(clave: number, productoId: string) {
    const unidad = unidadBaseDe(Number(productoId));
    actualizarLinea(clave, { productoId, unidadId: unidad === null ? '' : String(unidad) });
  }

  const totalEstimado = useMemo(
    () =>
      lineas.reduce((suma, linea) => {
        const cantidad = aNumero(linea.cantidad);
        const precio = aNumero(linea.precioUnitario);
        const descuento = aNumero(linea.descuento) ?? 0;
        if (cantidad === null || precio === null) {
          return suma;
        }
        return suma + cantidad * precio * (1 - descuento / 100);
      }, 0),
    [lineas],
  );

  function alGuardar() {
    setValidacion(null);

    if (!proveedorId) {
      setValidacion('Elige el proveedor.');
      return;
    }
    if (!sucursalId) {
      setValidacion('Elige la sede que recibirá la mercancía.');
      return;
    }

    const lineasValidas: CrearLineaOrdenCompraDto[] = [];
    for (const linea of lineas) {
      if (!linea.productoId || !linea.unidadId || !linea.cantidad) {
        setValidacion('Cada línea necesita producto, cantidad y unidad.');
        return;
      }
      const cantidad = aNumero(linea.cantidad);
      if (cantidad === null || cantidad <= 0) {
        setValidacion('La cantidad de cada línea debe ser mayor que cero.');
        return;
      }
      lineasValidas.push({
        productoId: Number(linea.productoId),
        cantidad,
        unidadId: Number(linea.unidadId),
        // `aNumero('')` ya devuelve null, que es lo que espera la API para
        // «usa el precio de lista del proveedor».
        precioUnitario: aNumero(linea.precioUnitario),
        descuento: aNumero(linea.descuento) ?? 0,
      });
    }

    if (lineasValidas.length === 0) {
      setValidacion('Agrega al menos una línea.');
      return;
    }

    const cuerpo = {
      proveedorId: Number(proveedorId),
      sucursalId: Number(sucursalId),
      plazoPagoDias: aNumero(plazoPagoDias),
      lineas: lineasValidas,
    };

    void enviar(async () => {
      if (orden) {
        await actualizarOrdenCompra(orden.id, cuerpo);
      } else {
        await crearOrdenCompra(cuerpo);
      }
      onCreada();
      onCerrar();
    });
  }

  return (
    <Modal
      titulo={esEdicion ? `Editar orden #${orden.id}` : 'Nueva orden de compra'}
      descripcion={
        esEdicion
          ? 'Solo se edita mientras la orden siga Pendiente. Las líneas que dejes aquí reemplazan a las que tenía.'
          : 'La orden compromete dinero con el proveedor pero NO mueve stock: eso ocurre cuando se registra su recepción.'
      }
      ancho="xl"
      ocupado={enviando}
      onCerrar={onCerrar}
      pie={
        <>
          <span className="mr-auto text-sm text-slate-500">
            Total estimado{' '}
            <span className="font-semibold text-slate-800">{formatearCOP(totalEstimado)}</span>
          </span>
          <Boton variante="secundaria" onClick={onCerrar} disabled={enviando}>
            Cancelar
          </Boton>
          <Boton onClick={alGuardar} disabled={enviando}>
            {enviando ? (
              <>
                <Spinner etiqueta="Guardando" />
                Guardando…
              </>
            ) : esEdicion ? (
              'Guardar cambios'
            ) : (
              'Crear orden'
            )}
          </Boton>
        </>
      }
    >
      <div className="flex flex-col gap-5">
        {error ? <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta> : null}
        {validacion ? <Alerta tipo="info">{validacion}</Alerta> : null}

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
          <CampoSelect
            etiqueta="Proveedor"
            valor={proveedorId}
            onCambio={setProveedorId}
            requerido
            placeholder="Selecciona un proveedor"
            disabled={enviando}
            opciones={proveedores.map((p) => ({ valor: String(p.id), texto: p.nombre }))}
          />
          <CampoSede valor={sucursalId} onCambio={setSucursalId} disabled={enviando} />
          <CampoNumero
            etiqueta="Plazo de pago"
            valor={plazoPagoDias}
            onCambio={setPlazoPagoDias}
            entero
            ayuda="Días. Opcional."
            disabled={enviando}
          />
        </div>

        <div className="flex flex-col gap-3">
          <div className="flex items-center gap-3">
            <h3 className="text-sm font-bold text-slate-800">Líneas</h3>
            <div className="h-px flex-1 bg-slate-200" />
            <Boton
              variante="secundaria"
              onClick={() => {
                setLineas((previas) => [...previas, lineaVacia(siguienteClave)]);
                setSiguienteClave((n) => n + 1);
              }}
              disabled={enviando}
              className="!h-9 !px-3 text-xs"
            >
              <Plus size={15} aria-hidden="true" />
              Agregar línea
            </Boton>
          </div>

          {lineas.map((linea) => (
            <div
              key={linea.clave}
              className="grid grid-cols-1 items-end gap-3 rounded-xl border border-slate-200 bg-slate-50 p-3 sm:grid-cols-12"
            >
              <div className="sm:col-span-4">
                <CampoSelect
                  etiqueta="Producto"
                  valor={linea.productoId}
                  onCambio={(v) => elegirProducto(linea.clave, v)}
                  opciones={opcionesProducto}
                  placeholder="Elegir"
                  disabled={enviando}
                />
              </div>
              <div className="sm:col-span-2">
                <CampoNumero
                  etiqueta="Cantidad"
                  valor={linea.cantidad}
                  onCambio={(v) => actualizarLinea(linea.clave, { cantidad: v })}
                  disabled={enviando}
                />
              </div>
              <div className="sm:col-span-2">
                <CampoSelect
                  etiqueta="Unidad"
                  valor={linea.unidadId}
                  onCambio={(v) => actualizarLinea(linea.clave, { unidadId: v })}
                  opciones={opcionesUnidad}
                  placeholder="Elegir"
                  disabled={enviando}
                />
              </div>
              <div className="sm:col-span-2">
                <CampoNumero
                  etiqueta="Precio"
                  valor={linea.precioUnitario}
                  onCambio={(v) => actualizarLinea(linea.clave, { precioUnitario: v })}
                  disabled={enviando}
                />
              </div>
              <div className="sm:col-span-1">
                <CampoNumero
                  etiqueta="Dcto %"
                  valor={linea.descuento}
                  onCambio={(v) => actualizarLinea(linea.clave, { descuento: v })}
                  disabled={enviando}
                />
              </div>
              <div className="flex justify-end sm:col-span-1">
                <button
                  type="button"
                  onClick={() =>
                    setLineas((previas) => previas.filter((l) => l.clave !== linea.clave))
                  }
                  disabled={enviando || lineas.length === 1}
                  title="Quitar línea"
                  className="flex h-11 w-11 items-center justify-center rounded-lg text-slate-500 transition hover:bg-red-50 hover:text-red-700 disabled:cursor-not-allowed disabled:text-slate-300 disabled:hover:bg-transparent"
                >
                  <Trash2 size={17} aria-hidden="true" />
                  <span className="sr-only">Quitar línea</span>
                </button>
              </div>
            </div>
          ))}
        </div>

        {/* Debajo de las líneas, que es donde se está mirando al digitar el
            precio. Consulta la lista pactada y lo que de verdad se cobró la
            última vez, y convierte las dos cifras a la unidad que se elija. */}
        <PanelPreciosReferencia
          proveedorId={proveedorId}
          lineas={lineas}
          disabled={enviando}
          onUsarPrecio={(clave, precio) =>
            // Se redondea a peso: la orden se cotiza en pesos y un precio con
            // decimales arrastrados de la conversión no se puede escribir en
            // una factura.
            actualizarLinea(clave, { precioUnitario: aTexto(Math.round(precio)) })
          }
        />
      </div>
    </Modal>
  );
}
