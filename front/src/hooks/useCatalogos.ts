import { useContext } from 'react';
import { CatalogosContext } from '../context/CatalogosContext';
import type { CatalogosContextValue } from '../context/CatalogosContext';

/** Productos y unidades de medida. Lanza si se usa fuera de `CatalogosProvider`. */
export function useCatalogos(): CatalogosContextValue {
  const contexto = useContext(CatalogosContext);

  if (contexto === undefined) {
    throw new Error('useCatalogos se usó fuera de <CatalogosProvider>.');
  }

  return contexto;
}
