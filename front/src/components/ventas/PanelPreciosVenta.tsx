import { useEffect, useMemo, useState } from 'react';
import { AlertTriangle, TrendingDown, TrendingUp } from 'lucide-react';
import { useCatalogos } from '../../hooks/useCatalogos';
import { obtenerPrecioVenta } from '../../services/ventas';
import type { PrecioVentaDto } from '../../models/ventas';
import { convertirCosto, factorPorSimbolo } from '../../utils/unidades';
import { formatearCOP, formatearFechaSolo } from '../../utils/formato';

/** Lo mínimo que el panel necesita saber de cada línea de la venta. */
export interface LineaVentaConsultable {
  clave: number;
  productoId: string;
  unidadId: string;
  /** Lo que hay escrito hoy en el campo de precio, en la unidad de la línea. */
  precioUnitario: string;
}

interface PanelPreciosVentaProps {
  lineas: LineaVentaConsultable[];
  /** Sede desde la que sale la mercancía; acota la «última venta». */
  sucursalId: string;
  /**
   * Rellena el precio de esa línea.
   *
   * `precio` ya viene en la unidad de la línea, que es lo que se guarda en la
   * venta. `precioBase` es la misma cifra por unidad base, y va aparte para que
   * el formulario pueda recalcularla si después se cambia de unidad sin
   * arrastrar el redondeo de esta conversión.
   */
  onUsarPrecio: (clave: number, precio: number, precioBase: number) => void;
  disabled?: boolean;
}

/**
 * A cuánto se está vendiendo, y si eso deja margen.
 *
 * POR QUÉ ESTE PANEL EXISTE. Antes el campo de precio no decía en qué unidad
 * iba, y el mismo esmalte salió a $264.172 por litro en una venta y a $13.209 en
 * otra —esta última por debajo del costo, con unos $24.000 de pérdida por
 * litro—. Nadie lo notó: 50.000 parece razonable si no se cae en que era por
 * galón. Por eso aquí todo se normaliza a una sola unidad y el margen se calcula
 * sobre lo que de verdad hay escrito en la línea, no sobre la lista.
 *
 * TRES CIFRAS QUE NO SE FUNDEN EN UNA:
 *
 *   Lista    lo que fijó la administración, por unidad base.
 *   Última   lo que de verdad se cobró la última vez, en la unidad en que se
 *            cotizó, sin normalizar, para que coincida con la factura.
 *   Costo    lo que ha costado en bodega. NO es un precio de venta: venderlo al
 *            costo es venderlo sin margen.
 */
export function PanelPreciosVenta({
  lineas,
  sucursalId,
  onUsarPrecio,
  disabled,
}: PanelPreciosVentaProps) {
  const { unidades } = useCatalogos();

  const [precios, setPrecios] = useState<Record<number, PrecioVentaDto>>({});
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

  // Solo las líneas que ya tienen producto: las vacías no tienen nada que
  // consultar y pedirlas daría un 404 por cada tecla.
  const productosPedidos = useMemo(
    () =>
      Array.from(
        new Set(
          lineas.map((l) => Number(l.productoId)).filter((id) => Number.isFinite(id) && id > 0),
        ),
      ).sort((a, b) => a - b),
    [lineas],
  );

  // La clave de dependencia es el TEXTO de la lista, no el arreglo: un arreglo
  // nuevo con los mismos ids en cada render dispararía la consulta sin parar.
  const clave = `${sucursalId}|${productosPedidos.join(',')}`;

  useEffect(() => {
    if (productosPedidos.length === 0) {
      setPrecios({});
      return;
    }

    let vigente = true;
    setConsultando(true);

    const sede = Number(sucursalId);

    Promise.all(
      productosPedidos.map((productoId) =>
        obtenerPrecioVenta(productoId, Number.isFinite(sede) && sede > 0 ? sede : null)
          .then((dato) => [productoId, dato] as const)
          // Un producto sin respuesta no debe tumbar el panel entero: se omite y
          // los demás siguen mostrándose.
          .catch(() => null),
      ),
    )
      .then((resultados) => {
        if (!vigente) {
          return;
        }
        const mapa: Record<number, PrecioVentaDto> = {};
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

  if (productosPedidos.length === 0) {
    return (
      <p className="rounded-xl border border-dashed border-slate-300 px-4 py-3 text-sm text-slate-500">
        Elige el producto de cada línea y aquí aparecerá su precio de lista, lo que se cobró la
        última vez y cuánto margen deja lo que estás cobrando.
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
        <h3 className="text-sm font-bold text-slate-800">Precios y margen</h3>
        <div className="h-px flex-1 bg-petroleo-100" />

        {unidadesVolumen.length > 1 ? (
          <div className="flex items-center gap-2">
            <span id="rotulo-unidad-precio-venta" className="text-xs font-medium text-slate-500">
              Ver en
            </span>
            <div
              role="group"
              aria-labelledby="rotulo-unidad-precio-venta"
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
              dato.precioVenta === null
                ? null
                : aUnidadElegida(dato.precioVenta, dato.unidadBaseSimbolo);

            const ultima =
              dato.ultimaVenta?.precioUnitario == null
                ? null
                : aUnidadElegida(dato.ultimaVenta.precioUnitario, dato.ultimaVenta.unidadSimbolo);

            const costo =
              dato.costoPromedio === null
                ? null
                : aUnidadElegida(dato.costoPromedio, dato.unidadBaseSimbolo);

            // Lo que hay escrito HOY en la línea, normalizado a la unidad de
            // arriba. Es la cifra sobre la que se juzga el margen: el precio de
            // lista puede estar bien y la línea seguir llevando otro número.
            const unidadLinea = unidades.find((u) => u.id === Number(linea.unidadId));
            const escrito = Number(linea.precioUnitario);
            const cobrado =
              Number.isFinite(escrito) && escrito > 0 && unidadLinea
                ? aUnidadElegida(escrito, unidadLinea.simbolo)
                : null;

            // Margen sobre el COSTO: «deja un 30% sobre lo que nos cuesta».
            // Negativo = se está vendiendo con pérdida.
            const margen =
              cobrado !== null && costo !== null && costo > 0
                ? ((cobrado - costo) / costo) * 100
                : null;

            const bajoCosto = margen !== null && margen < 0;

            // El botón solo aparece si hay lista Y difiere de lo escrito: con el
            // autorrelleno, lo normal es que ya coincidan y un botón que no
            // cambia nada es ruido.
            const factorBase = factorPorSimbolo(unidades, dato.unidadBaseSimbolo);
            const precioParaLinea =
              dato.precioVenta !== null &&
              unidadLinea?.factorConversionLitros != null &&
              factorBase !== null
                ? Math.round(
                    convertirCosto(
                      dato.precioVenta,
                      factorBase,
                      unidadLinea.factorConversionLitros,
                    ) * 100,
                  ) / 100
                : null;
            const difiereDeLista =
              precioParaLinea !== null && Math.abs(precioParaLinea - escrito) >= 0.01;

            return (
              <li
                key={linea.clave}
                className={`flex flex-col gap-1 rounded-lg px-3 py-2 text-sm ${
                  bajoCosto ? 'bg-terracota-50 ring-1 ring-terracota-300' : 'bg-white'
                }`}
              >
                <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
                  <span className="min-w-0 flex-1 font-medium text-slate-900">
                    {dato.productoNombre}
                  </span>

                  <span className="text-slate-500">
                    Lista{' '}
                    {lista === null ? (
                      <span className="text-slate-400">sin fijar</span>
                    ) : (
                      <span className="font-semibold tabular-nums text-slate-800">
                        {formatearCOP(lista)} / {destino?.simbolo}
                      </span>
                    )}
                  </span>

                  {/*
                    El costo va SIEMPRE que exista, no solo cuando hay problema:
                    es la cifra contra la que se juzga cualquier precio, y verla
                    al lado es lo que evita aceptar una rebaja que no deja nada.
                  */}
                  {costo !== null ? (
                    <span className="text-slate-500">
                      Costo{' '}
                      <span className="font-semibold tabular-nums text-slate-800">
                        {formatearCOP(costo)} / {destino?.simbolo}
                      </span>
                    </span>
                  ) : null}

                  <span className="text-slate-500">
                    Última{' '}
                    {ultima === null ? (
                      <span className="text-slate-400">nunca se ha vendido</span>
                    ) : (
                      <>
                        <span className="font-semibold tabular-nums text-slate-800">
                          {formatearCOP(ultima)} / {destino?.simbolo}
                        </span>
                        <span className="text-xs text-slate-400">
                          {' '}
                          ({formatearFechaSolo(dato.ultimaVenta!.fecha)}, cobrada a{' '}
                          {formatearCOP(dato.ultimaVenta!.precioUnitario ?? 0)} /{' '}
                          {dato.ultimaVenta!.unidadSimbolo})
                        </span>
                      </>
                    )}
                  </span>

                  {margen !== null ? (
                    <span
                      className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-semibold ${
                        bajoCosto
                          ? 'bg-terracota-100 text-terracota-800'
                          : 'bg-emerald-50 text-emerald-700'
                      }`}
                      title="Margen de lo que estás cobrando, sobre el costo en bodega"
                    >
                      {bajoCosto ? (
                        <TrendingDown size={13} aria-hidden="true" />
                      ) : (
                        <TrendingUp size={13} aria-hidden="true" />
                      )}
                      margen {margen > 0 ? '+' : ''}
                      {margen.toFixed(0)}%
                    </span>
                  ) : null}

                  {difiereDeLista && precioParaLinea !== null ? (
                    <button
                      type="button"
                      onClick={() => onUsarPrecio(linea.clave, precioParaLinea, dato.precioVenta!)}
                      disabled={disabled}
                      // El `title` se convierte en el nombre accesible y taparía
                      // el texto visible: quien navega por voz diría «Usar
                      // lista» y no encontraría nada. Se antepone el rótulo.
                      aria-label={
                        `Usar lista: poner ${formatearCOP(precioParaLinea)} ` +
                        `por ${unidadLinea?.simbolo} en esta línea`
                      }
                      title={`Poner ${formatearCOP(precioParaLinea)} por ${unidadLinea?.simbolo}`}
                      className="rounded-md border border-petroleo-200 px-2 py-1 text-xs font-semibold text-petroleo-700 transition hover:bg-petroleo-100 disabled:cursor-not-allowed disabled:opacity-40"
                    >
                      Usar lista
                    </button>
                  ) : null}
                </div>

                {bajoCosto ? (
                  <p className="flex items-center gap-1.5 text-xs font-semibold text-terracota-800">
                    <AlertTriangle size={14} aria-hidden="true" />
                    Estás vendiendo por debajo del costo: {formatearCOP(cobrado!)} contra{' '}
                    {formatearCOP(costo!)} por {destino?.simbolo}. Revisa la unidad de la línea.
                  </p>
                ) : null}
              </li>
            );
          })}
      </ul>

      <p className="text-xs text-slate-500">
        La lista y el costo van por unidad base del producto y se convierten a la unidad elegida
        arriba; lo que se guarda en la venta es el precio por la unidad de cada línea. «Lista» se
        edita en <strong className="font-semibold">Ventas → Lista de precios</strong>, solo el
        administrador.
      </p>
    </div>
  );
}
