/// <reference types="vite/client" />

/** Typed access to the environment variables this app reads. */
interface ImportMetaEnv {
  /** Base URL of the backend API. Defaults to the backend's development profile when unset. */
  readonly VITE_API_BASE_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
