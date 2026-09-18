/// <reference types="vite/client" />

// Tipa las variables propias de este proyecto. Sin esto,
// `import.meta.env.VITE_API_URL` seria `any` y un nombre mal escrito no daria
// ningun error hasta que la aplicacion intentara llamar a `undefined/auth/login`.
interface ImportMetaEnv {
  readonly VITE_API_URL: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
