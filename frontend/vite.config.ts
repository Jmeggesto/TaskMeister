import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      // Proxy all /api requests to the .NET backend during development.
      // The backend port comes from Properties/launchSettings.json.
      // Change the target here if the backend port changes — nowhere else.
      "/api": {
        target: "http://localhost:5276",
        changeOrigin: true,
      },
    },
  },
});
