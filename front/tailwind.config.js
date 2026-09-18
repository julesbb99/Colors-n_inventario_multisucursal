/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        // Escala corporativa. Son los valores de `sky` de Tailwind, renombrados
        // para que el codigo diga `colorsin-700` y no `sky-700`: el dia que la
        // marca cambie de tono se toca este bloque y nada mas.
        //
        // OJO: los mockups del lienzo usan verde petroleo con acento terracota,
        // no azul. Se dejo el azul porque es lo que pide la configuracion; para
        // volver al look del mockup basta reemplazar los valores de abajo.
        colorsin: {
          50: '#f0f9ff',
          100: '#e0f2fe',
          200: '#bae6fd',
          300: '#7dd3fc',
          400: '#38bdf8',
          500: '#0ea5e9',
          600: '#0284c7',
          700: '#0369a1',
          800: '#075985',
          900: '#0c4a6e',
        },
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
