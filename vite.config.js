import { defineConfig } from 'vite';
import { fileURLToPath } from 'node:url';

export default defineConfig({
  build: {
    rollupOptions: {
      input: {
        main: fileURLToPath(new URL('./index.html', import.meta.url)),
        rescue: fileURLToPath(new URL('./rescue.html', import.meta.url)),
        playlab: fileURLToPath(new URL('./playlab.html', import.meta.url)),
        outing: fileURLToPath(new URL('./outing.html', import.meta.url)),
        layoutB: fileURLToPath(new URL('./layout-b.html', import.meta.url)),
      },
    },
  },
});
