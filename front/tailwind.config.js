/**
 * Paleta de Colorsin.
 *
 * VERDE PETROLEO para las superficies de chrome -barra superior y lateral- y
 * TERRACOTA como unico acento. Son los dos colores de los mockups aprobados, y
 * la razon de que funcionen juntos es que no compiten: el petroleo no aparece
 * nunca como acento y el terracota no aparece nunca como fondo grande.
 *
 * Los tres tonos que fijan la identidad son 700, 800 y 900 del petroleo
 * (#1e5f57, #133d38, #0e2d2a); el resto de la escala se derivo de ellos para que
 * existan estados de hover y textos atenuados con el mismo matiz.
 *
 * SOBRE EL CONTRASTE, que es lo que decide cual tono se usa donde:
 *
 *   texto blanco sobre terracota-600   5.5:1   sirve para botones
 *   texto blanco sobre terracota-500   4.2:1   NO llega al minimo de 4.5:1
 *
 * Por eso el 500 se reserva para bordes e indicadores -que no llevan texto
 * encima- y el 600 para cualquier superficie con letra blanca.
 */

const petroleo = {
  50: '#eef5f3',
  100: '#d4e6e2',
  200: '#a9cdc7',
  300: '#74aca3',
  400: '#448a80',
  500: '#2a6f66',
  600: '#25675e',
  700: '#1e5f57',
  800: '#133d38',
  900: '#0e2d2a',
};

const terracota = {
  50: '#fdf3ef',
  100: '#fae2d8',
  200: '#f4c3b0',
  300: '#eb9c7e',
  400: '#dd7550',
  500: '#c85a32',
  600: '#b04924',
  700: '#8f3a1c',
  800: '#742f18',
  900: '#5f2917',
};

/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        petroleo,
        terracota,
        // `colorsin` es un ALIAS de la escala de petroleo, no una escala aparte.
        //
        // Antes apuntaba al azul de Tailwind, y lo usan pantallas que no son
        // parte del chrome: el formulario de acceso, los badges, las pestanas de
        // existencias. Dejarlo en azul habria producido un acceso azul colgando
        // de una aplicacion verde. Apuntandolo aqui, todo lo que ya escribia
        // `colorsin-700` sigue compilando y ademas queda del color correcto.
        colorsin: petroleo,
      },
      fontFamily: {
        sans: [
          'Public Sans',
          'system-ui',
          '-apple-system',
          'Segoe UI',
          'sans-serif',
        ],
      },
    },
  },
  plugins: [],
};
