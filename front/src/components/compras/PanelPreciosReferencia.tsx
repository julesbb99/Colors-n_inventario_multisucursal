import { useEffect, useMemo, useState } from 'react';
import { TrendingDown, TrendingUp } from 'lucide-react';
import { useCatalogos } from '../../hooks/useCatalogos';
import { obtenerPrecioReferencia } from '../../services/compras';
import type { PrecioReferenciaDto } from '../../models/compras';
import { convertirCosto, factorPorSimbolo } from '../../utils/unidades';
import { formatearCOP, formatearFechaSolo } from '../../utils/formato';

/** Lo mínimo que el panel necesita saber de cada línea de la orden. */
export interface LineaConsultable {
  clave: number;
  productoId: string;
  unidadId: string;
}

interface PanelPreciosReferenciaProps {
  proveedorId: string;
  lineas: LineaConsultable[];
  /** Rellena el precio de esa línea. El valor ya viene en la unidad de la línea. */
  onUsarPrecio: (clave: number, precio: number) => void;
  disabled?: boolean;
}

/**
 * Qué se ha cobrado por lo que se está pidiendo.
 *
 * MUESTRA DOS CIFRAS Y NO LAS FUNDE EN UNA:
 *
 *   Lista    lo pactado con el proveedor, que mantiene la administración.
 *   Última   lo que de verdad se cobró la última vez.
 *
 * Fundirlas obligaría a elegir cuál gana, y quien digita perdería la única
 * señal de que el proveedor se salió de lo pactado. Por eso, cuando difieren,
 * se marca la diferencia en porcentaje: es lo que hace falta ver antes de
 * aceptar un precio.
 *
 * LA CONVERSIÓN ES SOLO VISUAL. Los factores salen del catálogo
 * `unidades_medida`, igual que en Existencias; lo que se guarda en la orden es
 * el precio en la unidad de CADA LÍNEA, que es lo que va en el papel.
 */
export function PanelPreciosReferencia({
  proveedorId,
  lineas,
  onUsarPrecio,
  disabled,
}: PanelPreciosReferenciaProps) {
  const { unidades } = useCatalogos();

  const [precios, setPrecios] = useState<Record<number, PrecioReferenciaDto>>({});
  const [consultando, setConsultando] = useState(false);
  const [unidadElegida, setUnidadElegida] = useState<number | null>(null);

  const unidadesVolumen = useMemo(
    () =>
      unidades
        .flatMap((u) =>
          u.factorConversionLitros === null
            ? []
            : [{ id: u.id, nombre: u.nombre, simbolo: u.simbolo, factor: u.factorConversionLitros }],
        )
        .sort((a, b) => a.factor - b.factor),
    [unidades],
  );

  const unidadPorDefecto =
    unidadesVolumen.find((u) => u.factor === 1)?.id ?? unidadesVolumen[0]?.id ?? null;
  const unidadActiva = unidadElegida ?? unidadPorDefecto;
  const destino = unidadesVolumen.find((u) => u.id === unidadActiva) ?? null;

  // Solo las líneas que ya tienen producto. Las vacías no tienen nada que
  // consultar y pedirlas daría un 404 por cada tecla.
  const productosPedidos = useMemo(
    () =>
      Array.from(
        new Set(
          lineas
            .map((l) => Number(l.productoId))
            .filter((id) => Number.isFinite(id) && id > 0),
        ),
      ).sort((a, b) => a - b),
    [lineas],
  );

  // La clave de dependencia es el TEXTO de la lista, no el arreglo: un arreglo
  // nuevo con los mismos ids en cada render dispararía la consulta sin parar.
  const clave = `${proveedorId}|${productosPedidos.join(',')}`;

  useEffect(() => {
    const idProveedor = Number(proveedorId);
    if (!Number.isFinite(idProveedor) || idProveedor <= 0 || productosPedidos.length === 0) {
      setPrecios({});
      return;
    }

    let vigente = true;
    setConsultando(true);

    Promise.all(
      productosPedidos.map((productoId) =>
        obtenerPrecioReferencia(productoId, idProveedor)
          .then((dato) => [productoId, dato] as const)
          // Un producto sin respuesta no debe tumbar el panel entero: se omite
          // y los demás siguen mostrándose.
          .catch(() => null),
      ),
    )
      .then((resultados) => {
        if (!vigente) {
          return;
        }
        const mapa: Record<number, PrecioReferenciaDto> = {};
        for (const fila of resultados) {
          if (fila) {
            mapa[fila[0]] = fila[1];
          }
        }
        setPrecios(mapa);
      })
      .finally(() => {
        if (vigente) {
          setConsultando(false);
        }
      });

    return () => {
      vigente = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clave]);

  if (!proveedorId || productosPedidos.length === 0) {
    return (
      <p className="rounded-xl border border-dashed border-slate-300 px-4 py-3 text-sm text-slate-500">
        Elige el proveedor y el producto de cada línea y aquí aparecerá lo que se cobró la última
        vez, junto al precio de lista.
      </p>
    );
  }

  /** Pasa un precio de la unidad `simboloOrigen` a la unidad elegida arriba. */
  function aUnidadElegida(precio: number, simboloOrigen: string | null): number | null {
    const factorOrigen = factorPorSimbolo(unidades, simboloOrigen);
    if (destino === null || factorOrigen === null) {
      return null;
    }
    return convertirCosto(precio, factorOrigen, destino.factor);
  }

  return (
    <div className="flex flex-col gap-3 rounded-xl border border-petroleo-100 bg-petroleo-50/50 p-4">
      <div className="flex flex-wrap items-center gap-3">
        <h3 className="text-sm font-bold text-slate-800">Referencia de precios</h3>
        <div className="h-px flex-1 bg-petroleo-100" />

        {unidadesVolumen.length > 1 ? (
          <div className="flex items-center gap-2">
            <span id="rotulo-unidad-precio" className="text-xs font-medium text-slate-500">
              Ver en
            </span>
            <div
              role="group"
              aria-labelledby="rotulo-unidad-precio"
              className="flex items-center gap-1 rounded-lg border border-slate-300 bg-white p-0.5"
            >
              {unidadesVolumen.map((unidad) => {
                const activa = unidad.id === unidadActiva;
                return (
                  <button
                    key={unidad.id}
                    type="button"
                    onClick={() => setUnidadElegida(unidad.id)}
                    aria-pressed={activa}
                    title={unidad.nombre}
                    aria-label={unidad.nombre}
                    className={`h-7 rounded-md px-2.5 text-xs font-semibold transition ${
                      activa ? 'bg-petroleo-800 text-white' : 'text-slate-600 hover:bg-slate-100'
                    }`}
                  >
                    {unidad.simbolo}
                  </button>
                );
              })}
            </div>
          </div>
        ) : null}
      </div>

      {consultando ? <p className="text-sm text-slate-500">Consultando precios…</p> : null}

      <ul className="flex flex-col gap-2">
        {lineas
          .filter((linea) => Number(linea.productoId) > 0)
          .map((linea) => {
            const dato = precios[Number(linea.productoId)];
            if (!dato) {
              return null;
            }

            const lista =
              dato.precioReferencia === null
                ? null
                : aUnidadElegida(dato.precioReferencia, dato.unidadBaseSimbolo);

            const ultima =
              dato.ultimaCompra?.precioUnitario == null
                ? null
                : aUnidadElegida(
                    dato.ultimaCompra.precioUnitario,
                    dato.ultimaCompra.unidadSimbolo,
                  );

            // La diferencia se calcula sobre la LISTA, que es la referencia
            // pactada: "el proveedor cobró un 39% menos de lo acordado".
            const diferencia =
              lista !== null && ultima !== null && lista !== 0
                ? ((ultima - lista) / lista) * 100
                : null;

            // El precio que se rellena va en la unidad de ESTA LÍNEA, no en la
            // que se está mirando arriba: lo que se guarda en la orden es el
            // precio por la unidad en que se pide.
            const unidadLinea = unidades.find((u) => u.id === Number(linea.unidadId));
            const precioParaLinea =
              dato.precioReferencia !== null &&
              unidadLinea?.factorConversionLitros != null &&
              factorPorSimbolo(unidades, dato.unidadBaseSimbolo) !== null
                ? convertirCosto(
                    dato.precioReferencia,
                    factorPorSimbolo(unidades, dato.unidadBaseSimbolo) ?? 1,
                    unidadLinea.factorConversionLitros,
                  )
                : null;

            return (
              <li
                key={linea.clave}
                className="flex flex-wrap items-center gap-x-4 gap-y-1 rounded-lg bg-white px-3 py-2 text-sm"
              >
                <span className="min-w-0 flex-1 font-medium text-slate-900">
                  {dato.productoNombre}
                </span>

                <span className="text-slate-500">
                  Lista{' '}
                  {lista === null ? (
                    <span className="text-slate-400">sin precio pactado</span>
                  ) : (
                    <span className="font-semibold tabular-nums text-slate-800">
                      {formatearCOP(lista)} / {destino?.simbolo}
                    </span>
                  )}
                </span>

                <span className="text-slate-500">
                  Última{' '}
                  {ultima === null ? (
                    <span className="text-slate-400">nunca se le ha comprado</span>
                  ) : (
                    <>
                      <span className="font-semibold tabular-nums text-slate-800">
                        {formatearCOP(ultima)} / {destino?.simbolo}
                      </span>
                      <span className="text-xs text-slate-400">
                        {' '}
                        ({formatearFechaSolo(dato.ultimaCompra!.fecha)}, cotizada a{' '}
                        {formatearCOP(dato.ultimaCompra!.precioUnitario ?? 0)} /{' '}
                        {dato.ultimaCompra!.unidadSimbolo})
                      </span>
                    </>
                  )}
                </span>

                {diferencia !== null && Math.abs(diferencia) >= 0.5 ? (
                  <span
                    className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-semibold ${
                      diferencia > 0
                        ? 'bg-terracota-50 text-terracota-700'
                        : 'bg-emerald-50 text-emerald-700'
                    }`}
                    title="Diferencia de la última compra frente al precio de lista"
                  >
                    {diferencia > 0 ? (
                      <TrendingUp size={13} aria-hidden="true" />
                    ) : (
                      <TrendingDown size={13} aria-hidden="true" />
                    )}
                    {diferencia > 0 ? '+' : ''}
                    {diferencia.toFixed(0)}%
                  </span>
                ) : null}

                {precioParaLinea !== null ? (
                  <button
                    type="button"
                    onClick={() => onUsarPrecio(linea.clave, precioParaLinea)}
                    disabled={disabled}
                    title={`Poner ${formatearCOP(precioParaLinea)} por ${unidadLinea?.simbolo} en esta línea`}
                    className="rounded-md border border-petroleo-200 px-2 py-1 text-xs font-semibold text-petroleo-700 transition hover:bg-petroleo-100 disabled:cursor-not-allowed disabled:opacity-40"
                  >
                    Usar lista
                  </button>
                ) : null}
              </li>
            );
          })}
      </ul>

      <p className="text-xs text-slate-500">
        El precio de lista está pactado por unidad base del producto; lo de arriba es esa misma
        cifra convertida. Lo que se guarda en la orden es el precio por la unidad de cada línea.
      </p>
    </div>
  );
}
