import { useMemo } from 'react';
import type { ReactNode } from 'react';
import { useConsulta } from '../../hooks/useConsulta';
import { obtenerVenta } from '../../services/ventas';
import type { VentaDto } from '../../models/ventas';
import { Modal } from '../ui/Modal';
import { Boton } from '../ui/Boton';
import { Alerta } from '../ui/Alerta';
import { ChipColor } from '../ui/ChipColor';
import { CargandoPanel } from '../ui/Spinner';
import { formatearCOP, formatearFechaHora, formatearVolumen } from '../../utils/formato';

interface DetalleVentaProps {
  ventaId: number;
  onCerrar: () => void;
}

/**
 * Valor inicial ESTABLE, fuera del componente.
 *
 * `useConsulta` lo usa además para limpiar la pantalla cuando la consulta
 * falla; si se creara en cada render, el efecto no cambiaría de comportamiento
 * pero sí la identidad del valor, y eso es una trampa que ya cuesta cara en las
 * listas (`SIN_DATOS`).
 */
const SIN_VENTA: VentaDto | null = null;

/**
 * Un par etiqueta/valor del encabezado.
 *
 * EL VALOR SE PARTE, NO SE RECORTA. Con `truncate` la caja queda siempre
 * bonita, pero en un panel estrecho la fecha salía como «20 de sept de 2026,
 * 0…»: justo el dato por el que se abre la venta, ilegible. Aquí vale más un
 * renglón de más que un dato a medias.
 */
function Dato({ etiqueta, children }: { etiqueta: string; children: ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">
        {etiqueta}
      </dt>
      <dd className="mt-0.5 break-words text-sm text-slate-900">{children}</dd>
    </div>
  );
}

/**
 * Qué se vendió en una venta: el detalle línea por línea.
 *
 * SE CONSULTA AL ABRIR, no viene en el listado. `GET /api/ventas` devuelve los
 * encabezados sin detalle a propósito -cargar las líneas de cien ventas
 * convertiría una consulta en ciento una-, así que el detalle solo se pide de
 * la venta que de verdad se quiere mirar.
 *
 * ABIERTO A CUALQUIER ROL, igual que el listado: consultar qué se despachó es
 * trabajo de mostrador, no de supervisión. Lo que sigue acotado es la SEDE: la
 * API responde 403 si la venta es de otra, así que un operador no puede llegar
 * a una venta ajena ni escribiendo el id a mano.
 */
export function DetalleVenta({ ventaId, onCerrar }: DetalleVentaProps) {
  const {
    datos: venta,
    cargando,
    error,
    esPermisos,
  } = useConsulta(() => obtenerVenta(ventaId), [ventaId], SIN_VENTA);

  const lineas = venta?.detalles ?? [];

  /**
   * Los tres importes, sumados de las líneas.
   *
   * La API los deriva en cada lectura en vez de guardarlos, así que sumarlos
   * aquí no puede discrepar de una columna vieja. Lo que sí puede discrepar es
   * `ventas.total`, que SÍ está almacenado: por eso se comparan más abajo.
   */
  const suma = useMemo(
    () =>
      lineas.reduce(
        (acumulado, linea) => ({
          bruto: acumulado.bruto + linea.subtotalBruto,
          descuento: acumulado.descuento + linea.valorDescuento,
          neto: acumulado.neto + linea.subtotalNeto,
        }),
        { bruto: 0, descuento: 0, neto: 0 },
      ),
    [lineas],
  );

  /**
   * El encabezado guardado y la suma de las líneas no cuadran.
   *
   * Un peso de margen porque son dos decimales redondeados por separado. Si se
   * separan más, lo que hay delante es una venta registrada por fuera de este
   * servicio o con el total mal calculado -que es exactamente lo que pasaba
   * cuando una línea sin precio se contaba como cero-, y quien consulta tiene
   * que verlo en vez de creerse cualquiera de los dos números.
   */
  const descuadre =
    venta !== null && venta.total !== null && Math.abs(venta.total - suma.neto) > 1;

  return (
    <Modal
      titulo={`Venta n.º ${ventaId}`}
      descripcion="Qué se despachó, en qué unidad y a qué precio."
      ancho="xl"
      onCerrar={onCerrar}
      pie={
        <Boton variante="secundaria" onClick={onCerrar}>
          Cerrar
        </Boton>
      }
    >
      {cargando ? <CargandoPanel texto="Cargando la venta…" /> : null}

      {error ? <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta> : null}

      {venta !== null && !cargando ? (
        <div className="flex flex-col gap-5">
          <dl className="grid grid-cols-2 gap-x-4 gap-y-3 rounded-xl bg-slate-50 px-4 py-3 sm:grid-cols-4">
            <Dato etiqueta="Cliente">
              {venta.clienteRazonSocial}
              <span className="block text-xs tabular-nums text-slate-500">
                {venta.clienteDocumento}
              </span>
            </Dato>
            <Dato etiqueta="Sede">{venta.sucursalNombre}</Dato>
            <Dato etiqueta="Fecha">
              <span className="tabular-nums">{formatearFechaHora(venta.fecha)}</span>
            </Dato>
            <Dato etiqueta="Registró">{venta.usuarioNombre}</Dato>
          </dl>

          {lineas.length === 0 ? (
            <Alerta tipo="info">
              Esta venta no tiene líneas registradas. El encabezado existe pero no dice qué se
              despachó, así que no salió de este módulo: una venta registrada aquí crea sus
              líneas y descuenta el stock en la misma transacción.
            </Alerta>
          ) : (
            <div className="overflow-hidden rounded-xl border border-slate-200">
              <div className="overflow-x-auto">
                <table className="w-full border-collapse text-sm">
                  <thead>
                    <tr className="border-b border-petroleo-100 bg-petroleo-50 text-[11px] font-semibold uppercase tracking-wider text-petroleo-800">
                      <th scope="col" className="px-4 py-3 text-left">
                        Producto
                      </th>
                      <th scope="col" className="px-4 py-3 text-right">
                        Cantidad
                      </th>
                      <th scope="col" className="px-4 py-3 text-right">
                        Precio unitario
                      </th>
                      <th scope="col" className="px-4 py-3 text-right">
                        Desc.
                      </th>
                      <th scope="col" className="px-4 py-3 text-right">
                        Subtotal
                      </th>
                    </tr>
                  </thead>

                  <tbody>
                    {lineas.map((linea) => (
                      <tr
                        key={linea.id}
                        className="border-b border-slate-100 last:border-0 even:bg-slate-50/70"
                      >
                        <td className="px-4 py-3">
                          <span className="flex items-center gap-2.5">
                            <ChipColor nombre={linea.productoNombre} alto={26} />
                            <span className="text-slate-800">{linea.productoNombre}</span>
                          </span>
                        </td>
                        <td className="px-4 py-3 text-right tabular-nums text-slate-700">
                          {/* En la unidad EN QUE SE COTIZÓ, sin normalizar a la
                              unidad base: es lo que hay que poder contrastar
                              con la factura que se llevó el cliente. */}
                          {formatearVolumen(linea.cantidad, linea.unidadSimbolo)}
                        </td>
                        <td className="px-4 py-3 text-right tabular-nums text-slate-700">
                          {linea.precioUnitario === null
                            ? '—'
                            : formatearCOP(linea.precioUnitario)}
                        </td>
                        <td className="px-4 py-3 text-right tabular-nums text-slate-600">
                          {/* El descuento es un PORCENTAJE, no un importe. Se
                              muestra con el peso que rebaja al lado para que no
                              haya que multiplicar de cabeza. */}
                          {linea.descuento === 0 ? (
                            '—'
                          ) : (
                            <>
                              {linea.descuento}%
                              <span className="block text-xs text-slate-500">
                                −{formatearCOP(linea.valorDescuento)}
                              </span>
                            </>
                          )}
                        </td>
                        <td className="px-4 py-3 text-right font-semibold tabular-nums text-slate-900">
                          {formatearCOP(linea.subtotalNeto)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {/*
                LOS TOTALES VAN FUERA DE LA TABLA, no en un `tfoot`.

                La tabla se desplaza en horizontal cuando la ventana es
                estrecha, y un pie dentro de ella se desplaza con las líneas:
                el importe se va por un lado y la palabra «Total» por el otro,
                justo cuando menos sitio hay para buscarlos. Aquí el resumen
                queda quieto y siempre a la vista.
              */}
              <dl className="flex flex-col gap-1 border-t border-slate-200 bg-slate-50 px-4 py-3 text-sm">
                <div className="flex items-center justify-end gap-4">
                  <dt className="text-slate-600">Bruto</dt>
                  <dd className="w-36 text-right tabular-nums text-slate-700">
                    {formatearCOP(suma.bruto)}
                  </dd>
                </div>

                {suma.descuento > 0 ? (
                  <div className="flex items-center justify-end gap-4">
                    <dt className="text-slate-600">Descuentos</dt>
                    <dd className="w-36 text-right tabular-nums text-terracota-700">
                      −{formatearCOP(suma.descuento)}
                    </dd>
                  </div>
                ) : null}

                <div className="mt-1 flex items-center justify-end gap-4 border-t border-slate-200 pt-2">
                  <dt className="font-semibold text-slate-700">Total de la venta</dt>
                  <dd className="w-36 text-right text-base font-bold tabular-nums text-slate-900">
                    {formatearCOP(venta.total ?? suma.neto)}
                  </dd>
                </div>
              </dl>
            </div>
          )}

          {descuadre ? (
            <Alerta>
              El total guardado en el encabezado ({formatearCOP(venta.total ?? 0)}) no coincide
              con la suma de las líneas ({formatearCOP(suma.neto)}). Lo que se cobró son las
              líneas; el encabezado quedó mal calculado.
            </Alerta>
          ) : null}

          <p className="text-xs text-slate-500">
            Las cantidades van en la unidad en que se cotizó cada línea. El stock se descontó en
            la unidad base del producto, por FEFO: el lote que vence antes sale primero.
          </p>
        </div>
      ) : null}
    </Modal>
  );
}
