import { useId } from 'react';
import type { ReactNode } from 'react';

const BASE_CONTROL =
  'h-11 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 outline-none transition focus:border-petroleo-500 focus:ring-2 focus:ring-petroleo-100 disabled:bg-slate-100 disabled:text-slate-500';

interface EnvoltorioProps {
  etiqueta: string;
  /** Texto de apoyo bajo el control. */
  ayuda?: string;
  requerido?: boolean;
  children: (id: string) => ReactNode;
}

/**
 * Etiqueta, control y ayuda.
 *
 * El `id` se genera aquí y se le pasa al control por función, en vez de dejarlo
 * a cargo de quien lo usa: es lo que garantiza que el `<label>` y el campo estén
 * atados siempre. Un `htmlFor` que no apunta a nada rompe el clic en la etiqueta
 * y deja el campo sin nombre para un lector de pantalla, y nada lo delata.
 */
function Envoltorio({ etiqueta, ayuda, requerido, children }: EnvoltorioProps) {
  const id = useId();

  return (
    <div className="flex min-w-0 flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-semibold text-slate-700">
        {etiqueta}
        {requerido ? <span className="ml-1 text-terracota-600">*</span> : null}
      </label>
      {children(id)}
      {ayuda ? <span className="text-xs text-slate-500">{ayuda}</span> : null}
    </div>
  );
}

interface CampoTextoProps {
  etiqueta: string;
  valor: string;
  onCambio: (valor: string) => void;
  ayuda?: string;
  requerido?: boolean;
  placeholder?: string;
  tipo?: 'text' | 'date' | 'password';
  disabled?: boolean;
  /** `new-password` evita que el navegador ofrezca las credenciales de quien está conectado. */
  autoComplete?: string;
}

export function CampoTexto({
  etiqueta,
  valor,
  onCambio,
  ayuda,
  requerido,
  placeholder,
  tipo = 'text',
  disabled,
  autoComplete,
}: CampoTextoProps) {
  return (
    <Envoltorio etiqueta={etiqueta} ayuda={ayuda} requerido={requerido}>
      {(id) => (
        <input
          id={id}
          type={tipo}
          value={valor}
          disabled={disabled}
          placeholder={placeholder}
          autoComplete={autoComplete}
          onChange={(evento) => onCambio(evento.target.value)}
          className={BASE_CONTROL}
        />
      )}
    </Envoltorio>
  );
}

/**
 * Qué se admite al teclear en un campo numérico.
 *
 * Devuelve el texto YA NORMALIZADO, o `null` si lo escrito no vale y hay que
 * ignorar la tecla.
 *
 * EL PUNTO SE ESCRIBE COMO COMA. El punto del teclado numérico es la tecla que
 * busca quien digita rápido, y negársela sin más obligaría a bajar a la coma en
 * mitad de una cifra. Como es sustitución de un carácter por otro, el cursor se
 * queda donde estaba.
 */
function filtrarNumero(escrito: string, entero: boolean): string | null {
  // Vacío se permite: hay que poder borrar para corregir.
  if (escrito === '') {
    return '';
  }

  if (entero) {
    // Ni coma ni punto: días y unidades enteras no tienen parte decimal, y
    // admitir el separador solo serviría para escribir algo que se redondearía
    // después sin avisar.
    return /^\d+$/.test(escrito) ? escrito : null;
  }

  // `replace` sin la bandera global cambia SOLO el primer punto: «1.2.3» queda
  // «1,2.3», que no encaja con el patrón y por tanto se rechaza entero. Es lo
  // que se quiere -son dos separadores- y sale gratis.
  const normalizado = escrito.replace('.', ',');

  // Dígitos, opcionalmente una coma, opcionalmente más dígitos. Una coma suelta
  // («,») vale mientras se escribe «,5»; lo que no vale es una segunda coma, un
  // signo, un espacio o una letra.
  return /^\d*,?\d*$/.test(normalizado) ? normalizado : null;
}

interface CampoNumeroProps {
  etiqueta: string;
  /**
   * El valor TAL COMO SE VE, con COMA decimal: «12,5», no «12.5».
   *
   * Es una CADENA y no un número a propósito: con un `number` en el estado no se
   * podría escribir «1,» ni dejar el campo vacío mientras se corrige, porque
   * React lo normalizaría a 1 o a 0 en cada tecla y el cursor saltaría.
   *
   * Para sembrarlo desde un número está {@link aTexto}, y para leerlo antes de
   * enviar, {@link aNumero}. `Number('12,5')` es `NaN`, no 12,5.
   */
  valor: string;
  onCambio: (valor: string) => void;
  ayuda?: string;
  requerido?: boolean;
  disabled?: boolean;
  placeholder?: string;
  /** Sin parte decimal: días, unidades contadas. La coma deja de admitirse. */
  entero?: boolean;
}

/**
 * Cantidad, precio, porcentaje o conteo. POSITIVO y con COMA.
 *
 * ES EL ÚNICO CAMPO NUMÉRICO DE LA APLICACIÓN. Tener dos -uno filtrado y otro
 * no- termina siempre igual: el formulario nuevo agarra el que no filtra.
 *
 * POR QUE NO ES UN `<input type="number">`. Ese acepta cosas que aquí no tienen
 * sentido y que además no se ven hasta que fallan: el signo menos, el más, y la
 * notación científica -«1e5» es un número válido para el navegador-. Encima su
 * separador decimal depende de la configuración del equipo, así que el mismo
 * formulario admite «12.5» en una máquina y lo rechaza en otra.
 *
 * Aquí se filtra al teclear, con {@link filtrarNumero}: lo que no cumple no
 * llega a escribirse, en vez de escribirse y rechazarse al enviar. Vale también
 * para lo que se pega, porque pegar dispara el mismo `onChange` con el texto ya
 * puesto.
 *
 * COMO SE DESCARTA UNA TECLA. No se llama a `onCambio`, así que el estado no
 * cambia; React, para un campo controlado, devuelve el DOM al valor de la
 * propiedad aunque no haya vuelto a renderizar. El carácter no llega a verse.
 *
 * `inputMode` hace que en un móvil salga el teclado numérico aunque el campo sea
 * de texto.
 */
export function CampoNumero({
  etiqueta,
  valor,
  onCambio,
  ayuda,
  requerido,
  disabled,
  placeholder,
  entero = false,
}: CampoNumeroProps) {
  return (
    <Envoltorio etiqueta={etiqueta} ayuda={ayuda} requerido={requerido}>
      {(id) => (
        <input
          id={id}
          type="text"
          inputMode={entero ? 'numeric' : 'decimal'}
          autoComplete="off"
          value={valor}
          disabled={disabled}
          placeholder={placeholder}
          onChange={(evento) => {
            const filtrado = filtrarNumero(evento.target.value, entero);
            if (filtrado !== null) {
              onCambio(filtrado);
            }
          }}
          className={`${BASE_CONTROL} tabular-nums`}
        />
      )}
    </Envoltorio>
  );
}

/**
 * Lo escrito en un {@link CampoNumero}, como número.
 *
 * Devuelve `null` si está vacío o no es un número utilizable, que es lo que hay
 * que comprobar antes de enviar. La coma se cambia por punto porque `Number()`
 * no entiende la coma: `Number('12,5')` es `NaN`, no 12,5.
 */
export function aNumero(valor: string): number | null {
  const limpio = valor.trim().replace(',', '.');
  if (limpio === '' || limpio === '.') {
    return null;
  }

  const numero = Number(limpio);
  return Number.isFinite(numero) ? numero : null;
}

/**
 * Un número, listo para sembrar un {@link CampoNumero}.
 *
 * `String(12.5)` daría «12.5», CON PUNTO, y ese texto ya no se podría corregir:
 * al teclear encima, el filtro vería dos separadores y rechazaría la tecla. Lo
 * que el campo muestra y lo que el campo admite tienen que ser lo mismo.
 */
export function aTexto(valor: number | null | undefined): string {
  return valor === null || valor === undefined ? '' : String(valor).replace('.', ',');
}

export interface OpcionSelect {
  valor: string;
  texto: string;
}

interface CampoSelectProps {
  etiqueta: string;
  valor: string;
  opciones: OpcionSelect[];
  onCambio: (valor: string) => void;
  ayuda?: string;
  requerido?: boolean;
  /** Opción inicial vacía, para obligar a elegir de forma consciente. */
  placeholder?: string;
  disabled?: boolean;
}

export function CampoSelect({
  etiqueta,
  valor,
  opciones,
  onCambio,
  ayuda,
  requerido,
  placeholder,
  disabled,
}: CampoSelectProps) {
  return (
    <Envoltorio etiqueta={etiqueta} ayuda={ayuda} requerido={requerido}>
      {(id) => (
        <select
          id={id}
          value={valor}
          disabled={disabled}
          onChange={(evento) => onCambio(evento.target.value)}
          className={`${BASE_CONTROL} cursor-pointer disabled:cursor-not-allowed`}
        >
          {placeholder ? <option value="">{placeholder}</option> : null}
          {opciones.map((opcion) => (
            <option key={opcion.valor} value={opcion.valor}>
              {opcion.texto}
            </option>
          ))}
        </select>
      )}
    </Envoltorio>
  );
}

interface CampoAreaProps {
  etiqueta: string;
  valor: string;
  onCambio: (valor: string) => void;
  ayuda?: string;
  placeholder?: string;
  disabled?: boolean;
}

export function CampoArea({
  etiqueta,
  valor,
  onCambio,
  ayuda,
  placeholder,
  disabled,
}: CampoAreaProps) {
  return (
    <Envoltorio etiqueta={etiqueta} ayuda={ayuda}>
      {(id) => (
        <textarea
          id={id}
          rows={2}
          value={valor}
          disabled={disabled}
          placeholder={placeholder}
          onChange={(evento) => onCambio(evento.target.value)}
          className="w-full resize-y rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 outline-none transition focus:border-petroleo-500 focus:ring-2 focus:ring-petroleo-100 disabled:bg-slate-100"
        />
      )}
    </Envoltorio>
  );
}
