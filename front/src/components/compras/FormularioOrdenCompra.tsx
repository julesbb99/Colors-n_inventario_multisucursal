import { useMemo, useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { Modal } from '../ui/Modal';
import { Boton } from '../ui/Boton';
import { Alerta } from '../ui/Alerta';
import { Spinner } from '../ui/Spinner';
import { CampoNumero, CampoSelect } from '../ui/Campos';
import { CampoSede } from '../ui/CampoSede';
import { useSede } from '../../hooks/useSede';
import { useCatalogos } from '../../hooks/useCatalogos';
import { useConsulta } from '../../hooks/useConsulta';
import { useEnvio } from '../../hooks/useEnvio';
import { crearOrdenCompra, obtenerProveedores } from '../../services/compras';
import type { CrearLineaOrdenCompraDto, ProveedorDto } from '../../models/compras';
import { formatearCOP } from '../../utils/formato';

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
  onCerrar: () => void;
  onCreada: () => void;
}

export function FormularioOrdenCompra({ onCerrar, onCreada }: FormularioOrdenCompraProps) {
  const { sedeActiva } = useSede();
  const { opcionesProducto, opcionesUnidad, unidadBaseDe } = useCatalogos();
  const { enviando, error, esPermisos, enviar } = useEnvio();

  const { datos: proveedores } = useConsulta(() => obtenerProveedores(), [], SIN_PROVEEDORES);

  const [proveedorId, setProveedorId] = useState('');
  const [sucursalId, setSucursalId] = useState(sedeActiva === null ? '' : String(sedeActiva));
  const [plazoPagoDias, setPlazoPagoDias] = useState('');
  const [lineas, setLineas] = useState<LineaBorrador[]>([lineaVacia(0)]);
  const [siguienteClave, setSiguienteClave] = useState(1);
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
        const cantidad = Number(linea.cantidad);
        const precio = Number(linea.precioUnitario);
        const descuento = Number(linea.descuento) || 0;
        if (!Number.isFinite(cantidad) || !Number.isFinite(precio)) {
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
      const cantidad = Number(linea.cantidad);
      if (!Number.isFinite(cantidad) || cantidad <= 0) {
        setValidacion('La cantidad de cada línea debe ser mayor que cero.');
        return;
      }
      lineasValidas.push({
        productoId: Number(linea.productoId),
        cantidad,
        unidadId: Number(linea.unidadId),
        precioUnitario: linea.precioUnitario === '' ? null : Number(linea.precioUnitario),
        descuento: linea.descuento === '' ? 0 : Number(linea.descuento),
      });
    }

    if (lineasValidas.length === 0) {
      setValidacion('Agrega al menos una línea.');
      return;
    }

    void enviar(async () => {
      await crearOrdenCompra({
        proveedorId: Number(proveedorId),
        sucursalId: Number(sucursalId),
        plazoPagoDias: plazoPagoDias === '' ? null : Number(plazoPagoDias),
        lineas: lineasValidas,
      });
      onCreada();
      onCerrar();
    });
  }

  return (
    <Modal
      titulo="Nueva orden de compra"
      descripcion="La orden compromete dinero con el proveedor pero NO mueve stock: eso ocurre cuando se registra su recepción."
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
                <Spinner etiqueta="Creando" />
                Creando…
              </>
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
      </div>
    </Modal>
  );
}
