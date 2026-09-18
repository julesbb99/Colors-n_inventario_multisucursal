/**
 * Sede de la red. Espeja SucursalDto de la API.
 *
 * OJO: no hay campo `codigo` ni marca de activa/inactiva. El esquema no tiene
 * esas columnas, asi que GET /api/comun/sucursales devuelve TODAS las sedes.
 */
export interface SucursalDto {
  id: number;
  nombre: string;
  ciudad: string;
  direccion: string | null;
  /** 'Matriz' o 'Sucursal'. */
  rolRed: string;
}

/** Unidad de medida del catalogo. */
export interface UnidadMedidaDto {
  id: number;
  nombre: string;
  simbolo: string;
  /** Nulo en unidades que no son de volumen: sin el no hay conversion a litros. */
  factorConversionLitros: number | null;
}
