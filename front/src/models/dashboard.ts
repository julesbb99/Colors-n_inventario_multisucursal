/**
 * Totales de la pantalla de inicio. Espeja ResumenGeneralDto.
 *
 * Es una FOTO, no un documento: cada lectura se recalcula contra las tablas
 * operativas, asi que dos llamadas seguidas pueden dar cifras distintas. Por eso
 * viaja `generadoEn`.
 */
export interface ResumenGeneralDto {
  /** Sede EFECTIVA de las cifras, no la pedida. `null` = toda la red. */
  sucursalId: number | null;
  sucursalNombre: string | null;
  /** Dia contra el que se calcularon las ventas, en formato `AAAA-MM-DD`. */
  fechaCorte: string;
  ventasDelDia: number;
  cantidadVentasDelDia: number;
  ventasDelMes: number;
  cantidadVentasDelMes: number;
  /** Todo el stock llevado a litros con el factor de cada unidad base. */
  saldoInventarioLitros: number;
  /** Saldos excluidos del total por no tener factor a litros. */
  productosSinConversionALitros: number;
  transferenciasEnTransito: number;
  /** Criterio: `cantidadBase <= stockMinimo`. */
  alertasStockBajo: number;
  /** Lotes con saldo que caducan dentro del umbral, INCLUIDOS los ya vencidos. */
  alertasVencimiento: number;
  /** Ventana usada para `alertasVencimiento`. Sin ella el numero no se puede interpretar. */
  diasUmbralVencimiento: number;
  /** Órdenes de compra que llegaron cortas y siguen esperando el resto. */
  ordenesParcialmenteRecibidas: number;
  /**
   * Lo que vale lo que se pidió y todavía NO ha llegado de esas órdenes.
   *
   * NO ES UNA MERMA. La merma saca mercancía que estaba en la bodega y baja el
   * saldo; esto es lo contrario, mercancía que nunca entró, así que no hay nada
   * que descontar. Y tampoco es una pérdida todavía: la orden sigue abierta y
   * el proveedor puede completar la entrega.
   */
  faltanteRecepcionValor: number;
  generadoEn: string;
}

/** Lo vendido en un mes calendario. Espeja VentasPorMesDto. */
export interface VentasPorMesDto {
  anio: number;
  /** De 1 a 12. */
  mes: number;
  cantidadVentas: number;
  total: number;
}

/**
 * Las ventas mes a mes y el acumulado de TODA la historia.
 *
 * EL ACUMULADO NO SE SUMA DE `meses`. Esa lista viene recortada a la ventana
 * pedida, así que sumarla daría el total de la ventana y no el de la historia.
 * Son dos consultas distintas a propósito.
 */
export interface HistoricoVentasDto {
  sucursalId: number | null;
  sucursalNombre: string | null;
  /** Cuántos meses se pidieron. Sin él, un hueco no se distingue de un mes fuera de ventana. */
  mesesSolicitados: number;
  /** Un elemento por mes CON ventas, del más antiguo al más reciente. */
  meses: VentasPorMesDto[];
  cantidadHistorica: number;
  /** La suma de todas las ventas realizadas, sin acotar por fecha. */
  totalHistorico: number;
  ticketPromedioHistorico: number;
  primeraVenta: string | null;
  ultimaVenta: string | null;
  /** Media sobre los meses QUE TUVIERON VENTAS, no sobre los solicitados. */
  promedioMensual: number;
}

/** Las cuatro clases de rotación, tal como las serializa la API. */
export type ClaseRotacion = 'Alta' | 'Media' | 'Baja' | 'SinMovimiento';

export const ETIQUETA_ROTACION: Record<ClaseRotacion, string> = {
  Alta: 'Alta demanda',
  Media: 'Demanda media',
  Baja: 'Baja demanda',
  SinMovimiento: 'Sin movimiento',
};

export interface RotacionProductoDto {
  productoId: number;
  productoNombre: string;
  categoria: string | null;
  /** Unidad de las dos cantidades. Nula si el producto no tiene unidad base. */
  unidadBaseSimbolo: string | null;
  cantidadVendidaBase: number;
  saldoActualBase: number;
  totalFacturado: number;
  /** Vendido entre saldo. Nulo sin saldo: dividir entre cero no se puede medir. */
  vecesQueRoto: number | null;
  /** Cuántos días aguanta el stock al ritmo del periodo. Nulo si no hubo ventas. */
  diasCobertura: number | null;
  clase: ClaseRotacion;
}

/**
 * La rotación del catálogo en un periodo, ya clasificada.
 *
 * SE CLASIFICA POR DÍAS DE COBERTURA y no por cantidad vendida: «400 litros» no
 * dice si es mucho sin saber cuánto hay en bodega, y los días sí son
 * comparables entre productos de unidades distintas.
 */
export interface RotacionProductosDto {
  desde: string;
  hasta: string;
  dias: number;
  sucursalId: number | null;
  sucursalNombre: string | null;
  umbralDiasAlta: number;
  umbralDiasBaja: number;
  /** Todos, de más a menos demanda. Los sin movimiento al final. */
  productos: RotacionProductoDto[];
  conAlta: number;
  conMedia: number;
  conBaja: number;
  sinMovimiento: number;
}

export interface RendimientoSucursalDto {
  sucursalId: number;
  sucursalNombre: string;
  ciudad: string | null;
  cantidadVentas: number;
  totalVendido: number;
  ticketPromedio: number;
  /** Tajada de las ventas de la red, en %. Nula si la red no vendió nada. */
  participacionPorcentaje: number | null;
  saldoLitros: number;
  productosEnAlerta: number;
  trasladosDespachados: number;
  trasladosRecibidos: number;
  /** Pesos vendidos por litro almacenado: productividad del inventario. */
  ventaPorLitroEnBodega: number | null;
}

/**
 * Comparativa de rendimiento entre sedes.
 *
 * NO APLICA EL AISLAMIENTO POR SEDE, y es la única consulta del panel de la que
 * hay que decirlo: una comparativa en la que cada gerente solo ve su fila no es
 * una comparativa. El operario recibe 403.
 */
export interface ComparativaSucursalesDto {
  desde: string;
  hasta: string;
  /** Todas las sedes, de mayor a menor venta. También las que se quedaron en cero. */
  sucursales: RendimientoSucursalDto[];
  totalRedVendido: number;
  totalRedVentas: number;
}
