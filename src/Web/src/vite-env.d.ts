/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** 'true' aktiviert die Demo mit erfundenen Testdaten. */
  readonly VITE_USE_MOCKS?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
