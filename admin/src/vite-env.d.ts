/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Base URL of the Klowee Cafe API, e.g. http://localhost:5181. */
  readonly VITE_API_URL: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
