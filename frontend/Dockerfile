# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM node:22-alpine AS build
WORKDIR /app

# Install dependencies first (dedicated layer — only re-runs when package files change)
COPY frontend/package*.json ./
RUN npm ci

# Copy source and build static assets
COPY frontend/ ./
RUN npm run build

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM nginx:alpine AS runtime

# Serve the built assets
COPY --from=build /app/dist /usr/share/nginx/html

# Replace the default nginx config with ours (SPA fallback + /api proxy)
COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
