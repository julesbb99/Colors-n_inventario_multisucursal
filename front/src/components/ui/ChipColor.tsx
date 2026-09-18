import { colorDeProducto } from '../../utils/formato';

interface ChipColorProps {
  /** Nombre del producto: de el sale el color, de forma estable. */
  nombre: string;
  alto?: number;
}

/**
 * La barra de color que acompana a un producto en las tablas.
 *
 * Reemplaza a la miniatura de producto de los mockups: en una empresa de
 * pinturas el color es lo que distingue una referencia de otra de un vistazo, y
 * ademas el catalogo no guarda imagenes.
 *
 * El color NO representa el tono real del producto -la base no tiene ese dato-
 * sino que sirve para seguir una fila con la vista. Va marcado como decorativo
 * para que un lector de pantalla no lo anuncie.
 */
export function ChipColor({ nombre, alto = 32 }: ChipColorProps) {
  return (
    <span
      aria-hidden="true"
      className="inline-block w-2.5 shrink-0 rounded-sm"
      style={{ height: alto, backgroundColor: colorDeProducto(nombre) }}
    />
  );
}
