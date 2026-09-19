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
