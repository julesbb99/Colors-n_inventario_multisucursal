import type { UnidadMedidaDto } from '../models/comun';

/**
 * Conversión entre unidades de volumen, solo para MOSTRAR.
 *
 * LOS FACTORES NO SE ESCRIBEN AQUÍ. Vienen del catálogo `unidades_medida` que
 * sirve GET /api/comun/unidades-medida, que es el mismo número con el que la API
 * convierte al registrar un movimiento. Teclear 3,785 en el frontend crearía una
 * segunda verdad: el día que alguien corrija el factor en la base, la pantalla
 * seguiría enseñando el viejo y nadie lo notaría, porque las dos cifras son
 * plausibles.
 *
 * Nada de esto toca el dato guardado ni el criterio de alerta: el saldo se
 * almacena y se compara con el mínimo en la unidad base del producto. Esto es
 * una lente sobre la misma cifra.
 */

/**
 * El factor a litros de la unidad con ese símbolo.
 *
 * Nulo cuando el símbolo no está en el catálogo o la unidad no es de volumen
 * (por ejemplo, un producto que se lleve por piezas). Quien lo reciba nulo NO
 * debe convertir: debe dejar el valor como viene.
 */
export function factorPorSimbolo(
  unidades: UnidadMedidaDto[],
  simbolo: string | null,
): number | null {
  if (simbolo === null || simbolo === '') {
    return null;
  }
  return unidades.find((unidad) => unidad.simbolo === simbolo)?.factorConversionLitros ?? null;
}

/**
 * Una CANTIDAD que está en la unidad base, expresada en la unidad destino.
 *
 * 1.590,5 L a galones son 420,2: al ser el galón más grande que el litro, el
 * número baja.
 */
export function convertirCantidad(
  valorEnBase: number,
  factorBase: number,
  factorDestino: number,
): number {
  return (valorEnBase * factorBase) / factorDestino;
}

/**
 * Un COSTO POR unidad base, expresado como costo POR unidad destino.
 *
 * Va al revés que la cantidad, y conviene verlo junto: si un litro cuesta
 * $22.500, el galón cuesta $85.171 -más, porque lleva más producto- mientras que
 * la misma cantidad medida en galones da un número más pequeño. Invertir esta
 * división es el error clásico, y el resultado sigue pareciendo razonable.
 */
export function convertirCosto(
  costoPorUnidadBase: number,
  factorBase: number,
  factorDestino: number,
): number {
  return (costoPorUnidadBase / factorBase) * factorDestino;
}
