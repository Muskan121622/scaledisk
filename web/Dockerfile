# ScaleDesk web — static SPA behind nginx, built for the in-cluster k8s stack.
# Context MUST be the web/ directory:  docker build -f web/Dockerfile web
# nginx proxies /api, /hubs (websocket) and /health to the `api` Service so the SPA
# stays same-origin in the cluster, exactly like the Vite dev proxy does locally.

# ---------- build ----------
FROM node:20-alpine AS build
WORKDIR /app

# Dependency layer caches independently of source changes.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
# `npm run build` = tsc -b && vite build -> static assets in /app/dist
RUN npm run build

# ---------- runtime ----------
FROM nginx:1.27-alpine AS runtime
EXPOSE 80

# Static files + SPA fallback + reverse-proxy rules.
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

HEALTHCHECK --interval=15s --timeout=3s --start-period=5s --retries=3 \
  CMD wget -q --spider http://127.0.0.1/ || exit 1
