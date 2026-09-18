/**
 * Formato de cifras y fechas, con la convencion colombiana.
 *
 * Todo pasa por aqui para que la coma decimal, el punto de miles y el simbolo de
 * peso se decidan una sola vez. Un `toFixed(2)` suelto en un componente da
 * "1590.50" donde el resto de la pantalla dice "1.590,5".
 */

const formatoLitros = new Intl.NumberFormat('es-CO', {
  minimumFractionDigits: 1,
  maximumFractionDigits: 2,
});

const formatoEntero = new Intl.NumberFormat('es-CO', {
  maximumFractionDigits: 0,
});

const formatoMoneda = new Intl.NumberFormat('es-CO', {
  style: 'currency',
  currency: 'COP',
  // Los centavos no significan nada en un precio de pintura por litro y solo
  // alargan la cifra en una tabla.
  maximumFractionDigits: 0,
});

/** Un volumen con su unidad: `1.590,5 L`. */
export function formatearVolumen(valor: number | null, simbolo: string | null): string {
  if (valor === null) {
    return '—';
  }
  return `${formatoLitros.format(valor)} ${simbolo ?? ''}`.trim();
}

/** Litros, cuando la unidad ya se sabe: `4.869,2 L`. */
export function formatearLitros(valor: number): string {
  return `${formatoLitros.format(valor)} L`;
}

/** Pesos colombianos sin centavos: `$190.174.496`. */
export function formatearCOP(valor: number): string {
  return formatoMoneda.format(valor);
}

/** Un conteo: `142`. */
export function formatearEntero(valor: number): string {
  return formatoEntero.format(valor);
}

/**
 * Una fecha `AAAA-MM-DD` de la API como `30 nov 2026`.
 *
 * OJO CON LA ZONA HORARIA. `new Date('2026-11-30')` interpreta la cadena como
 * medianoche UTC, y en Colombia (UTC-5) eso se muestra como el 29 de noviembre:
 * la fecha se corre un dia entero. Por eso se parte la cadena y se construye la
 * fecha en hora LOCAL, que es lo que significa un `DateOnly` sin huso.
 */
export function formatearFechaSolo(iso: string | null): string {
  if (!iso) {
    return '—';
  }

  const partes = iso.slice(0, 10).split('-');
  if (partes.length !== 3) {
    return iso;
  }

  const anio = Number(partes[0]);
  const mes = Number(partes[1]);
  const dia = Number(partes[2]);

  if (!Number.isFinite(anio) || !Number.isFinite(mes) || !Number.isFinite(dia)) {
    return iso;
  }

  return new Date(anio, mes - 1, dia).toLocaleDateString('es-CO', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  });
}

/** Una marca de tiempo completa como `18 sep 2026, 15:40`. */
export function formatearFechaHora(iso: string | null): string {
  if (!iso) {
    return '—';
  }

  const fecha = new Date(iso);
  if (Number.isNaN(fecha.getTime())) {
    return iso;
  }

  return fecha.toLocaleString('es-CO', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/** Los dias que faltan, con signo: `+73 d`, `-10 d`. */
export function formatearDias(dias: number | null): string {
  if (dias === null) {
    return 'Sin fecha';
  }
  return dias < 0 ? `${dias} d` : `+${dias} d`;
}

/**
 * Un color estable para el chip de un producto.
 *
 * Es una funcion del NOMBRE y no un campo de la base: el catalogo no guarda
 * ningun color, y el chip existe para distinguir de un vistazo las filas de una
 * tabla, no para representar el tono real de la pintura. Al ser determinista, el
 * mismo producto sale siempre del mismo color en todas las pantallas.
 */
const PALETA_CHIPS = [
  '#2F5D8C',
  '#8C5A2F',
  '#2E6B60',
  '#6B5DA8',
  '#9A3F5B',
  '#4A6B2F',
  '#8A6410',
  '#5A6470',
] as const;

export function colorDeProducto(nombre: string): string {
  let suma = 0;
  for (let i = 0; i < nombre.length; i += 1) {
    suma = (suma + nombre.charCodeAt(i) * (i + 1)) % 100000;
  }
  return PALETA_CHIPS[suma % PALETA_CHIPS.length];
}
