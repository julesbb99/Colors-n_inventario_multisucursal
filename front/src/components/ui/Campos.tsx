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

interface CampoNumeroProps {
  etiqueta: string;
  /**
   * El valor se maneja como CADENA, no como número.
   *
   * Con un `number` en el estado no se puede escribir "1." ni dejar el campo
   * vacío mientras se corrige: React lo normalizaría a 1 o a 0 en cada tecla y
   * el cursor saltaría. Se convierte al enviar, que es cuando importa.
   */
  valor: string;
  onCambio: (valor: string) => void;
  ayuda?: string;
  requerido?: boolean;
  min?: number;
  step?: string;
  disabled?: boolean;
}

export function CampoNumero({
  etiqueta,
  valor,
  onCambio,
  ayuda,
  requerido,
  min = 0,
  step = 'any',
  disabled,
}: CampoNumeroProps) {
  return (
    <Envoltorio etiqueta={etiqueta} ayuda={ayuda} requerido={requerido}>
      {(id) => (
        <input
          id={id}
          type="number"
          inputMode="decimal"
          value={valor}
          min={min}
          step={step}
          disabled={disabled}
          onChange={(evento) => onCambio(evento.target.value)}
          className={`${BASE_CONTROL} tabular-nums`}
        />
      )}
    </Envoltorio>
  );
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
