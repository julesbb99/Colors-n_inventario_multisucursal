import { useEffect, useState } from 'react';
import { AlertTriangle } from 'lucide-react';
import { Modal } from '../ui/Modal';
import { Boton } from '../ui/Boton';
import { Alerta } from '../ui/Alerta';
import { CampoNumero } from '../ui/Campos';
import { useCatalogos } from '../../hooks/useCatalogos';
import { useEnvio } from '../../hooks/useEnvio';
import { fijarPrecioVenta, obtenerPrecioVenta } from '../../services/ventas';
import type { PrecioVentaDto } from '../../models/ventas';
import { convertirCosto, factorPorSimbolo } from '../../utils/unidades';
import { formatearCOP } from '../../utils/formato';

interface ListaPreciosVentaProps {
  onCerrar: () => void;
  /** Se llama tras guardar, para que el catálogo recoja el precio nuevo. */
  onGuardado: () => void;
}

/**
 * A cuánto se vende cada producto. SOLO ADMINISTRADOR GENERAL.
 *
 * EL PRECIO VA POR UNIDAD BASE, que es como lo guarda `productos.precio_venta`.
 * Por eso cada fila dice el símbolo y muestra al lado la equivalencia en galón y
 * en caneca: sin eso, «48.880» no dice si es por litro o por caneca, y de ahí
 * salieron ventas del mismo producto a precios veinte veces distintos.
 *
 * AL LADO VA EL COSTO, siempre. Fijar un precio sin ver contra qué se compara es
 * cómo se termina vendiendo por debajo de lo que costó; cuando el precio no
 * cubre el costo, la fila lo dice.
 *
 * Dejar el campo vacío QUITA el precio. No toca ninguna venta histórica: las
 * líneas guardan su propio precio, no una referencia a esto.
 */
export function ListaPreciosVenta({ onCerrar, onGuardado }: ListaPreciosVentaProps) {
  const { productos, unidades } = useCatalogos();
  const { enviando, error, esPermisos, enviar } = useEnvio();

  const [datos, setDatos] = useState<Record<number, PrecioVentaDto>>({});
  const [valores, setValores] = useState<Record<number, string>>({});
  const [cargando, setCargando] = useState(true);
  const [guardado, setGuardado] = useState<number | null>(null);

  // En un efecto y no en el render: pedir datos mientras se pinta es un efecto
  // secundario en mitad del render, y React puede ejecutarlo dos veces o
  // descartarlo. El `vigente` descarta la respuesta si el panel se cierra
  // mientras la petición viaja.
  useEffect(() => {
    let vigente = true;
    setCargando(true);

    void Promise.all(
      productos.map((producto) =>
        obtenerPrecioVenta(producto.id)
          .then((dato) => [producto.id, dato] as const)
          .catch(() => null),
      ),
    )
      .then((filas) => {
        if (!vigente) {
          return;
        }
        const mapaDatos: Record<number, PrecioVentaDto> = {};
        const mapaValores: Record<number, string> = {};
        for (const fila of filas) {
          if (fila) {
            mapaDatos[fila[0]] = fila[1];
            mapaValores[fila[0]] = fila[1].precioVenta === null ? '' : String(fila[1].precioVenta);
          }
        }
        setDatos(mapaDatos);
        setValores(mapaValores);
      })
      .finally(() => {
        if (vigente) {
          setCargando(false);
        }
      });

    return () => {
      vigente = false;
    };
  }, [productos]);

  function guardar(productoId: number) {
    const texto = valores[productoId] ?? '';
    const precio = texto.trim() === '' ? null : Number(texto);

    if (precio !== null && (!Number.isFinite(precio) || precio <= 0)) {
      return;
    }

    void enviar(async () => {
      const actualizado = await fijarPrecioVenta(productoId, { precioVenta: precio });
      setDatos((previos) => ({ ...previos, [productoId]: actualizado }));
      setGuardado(productoId);
      onGuardado();
    });
  }

  /** Lo que valdría ese precio por litro, por galón y por caneca. */
  function equivalencias(precioBase: number, simboloBase: string | null): string | null {
    const factorBase = factorPorSimbolo(unidades, simboloBase);
    if (factorBase === null) {
      return null;
    }

    return unidades
      .filter((u) => u.factorConversionLitros !== null && u.simbolo !== simboloBase)
      // De la unidad más pequeña a la más grande. El catálogo no llega ordenado
      // y sin esto salía «por caneca» antes que «por galón», que se lee al
      // revés de como se piensa.
      .sort((a, b) => a.factorConversionLitros! - b.factorConversionLitros!)
      .map(
        (u) =>
          `${formatearCOP(
            Math.round(convertirCosto(precioBase, factorBase, u.factorConversionLitros!)),
          )} / ${u.simbolo}`,
      )
      .join(' · ');
  }

  return (
    <Modal
      titulo="Lista de precios de venta"
      descripcion="Los precios son de toda la red: las tres sedes heredan la misma lista."
      ancho="xl"
      ocupado={enviando}
      onCerrar={onCerrar}
      pie={
        <Boton variante="secundaria" onClick={onCerrar} disabled={enviando}>
          Cerrar
        </Boton>
      }
    >
      <div className="flex flex-col gap-3">
        {error ? <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta> : null}
        {cargando ? <p className="text-sm text-slate-500">Cargando precios…</p> : null}

        {productos.map((producto) => {
          const dato = datos[producto.id];
          const escrito = Number(valores[producto.id] ?? '');
          const hayPrecio = Number.isFinite(escrito) && escrito > 0;

          const costo = dato?.costoPromedio ?? null;
          const bajoCosto = hayPrecio && costo !== null && escrito < costo;
          const margen =
            hayPrecio && costo !== null && costo > 0 ? ((escrito - costo) / costo) * 100 : null;

          return (
            <div
              key={producto.id}
              className={`flex flex-col gap-2 rounded-lg p-3 ${
                bajoCosto ? 'bg-terracota-50 ring-1 ring-terracota-300' : 'bg-slate-50'
              }`}
            >
              <div className="flex flex-wrap items-end gap-3">
                <span className="min-w-0 flex-1 text-sm text-slate-700">
                  {producto.nombre}
                  <span className="text-xs text-slate-400">
                    {' '}
                    · por {producto.unidadBaseSimbolo}
                  </span>
                  {costo !== null ? (
                    <span className="block text-xs text-slate-500">
                      cuesta{' '}
                      <span className="font-semibold tabular-nums">{formatearCOP(costo)}</span> /{' '}
                      {producto.unidadBaseSimbolo}
                    </span>
                  ) : null}
                </span>

                <div className="w-44">
                  <CampoNumero
                    etiqueta={`Precio / ${producto.unidadBaseSimbolo ?? 'unidad'}`}
                    valor={valores[producto.id] ?? ''}
                    onCambio={(v) => {
                      setValores((previos) => ({ ...previos, [producto.id]: v }));
                      setGuardado(null);
                    }}
                    min={0}
                    disabled={enviando}
                  />
                </div>

                {margen !== null ? (
                  <span
                    className={`inline-flex h-11 items-center rounded-lg px-2.5 text-xs font-semibold tabular-nums ${
                      bajoCosto
                        ? 'bg-terracota-100 text-terracota-800'
                        : 'bg-emerald-50 text-emerald-700'
                    }`}
                    title="Margen sobre el costo en bodega"
                  >
                    {margen > 0 ? '+' : ''}
                    {margen.toFixed(0)}%
                  </span>
                ) : null}

                <Boton
                  variante="secundaria"
                  onClick={() => guardar(producto.id)}
                  disabled={enviando}
                  className="!h-11 !px-3 text-xs"
                >
                  {guardado === producto.id ? 'Guardado' : 'Guardar'}
                </Boton>
              </div>

              {bajoCosto ? (
                <p className="flex items-center gap-1.5 text-xs font-semibold text-terracota-800">
                  <AlertTriangle size={14} aria-hidden="true" />
                  Este precio está por debajo de lo que cuesta el producto.
                </p>
              ) : null}

              {hayPrecio ? (
                <p className="text-xs text-slate-500">
                  equivale a {equivalencias(escrito, producto.unidadBaseSimbolo) ?? '—'}
                </p>
              ) : null}
            </div>
          );
        })}

        <p className="text-xs text-slate-500">
          Vacío quita el precio del producto, que no es lo mismo que ponerlo en cero: sin precio, la
          venta que no traiga uno propio se rechaza; en cero se registraría regalada.
        </p>
      </div>
    </Modal>
  );
}
