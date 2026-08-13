# syntax=docker/dockerfile:1

# ---- base: shared dependency layer -----------------------------------------
FROM node:20-alpine AS base
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci

# ---- development: Vite dev server, source bind-mounted by docker-compose.dev.yml ----
FROM base AS development
COPY . .
EXPOSE 5173

HEALTHCHECK --interval=10s --timeout=3s --start-period=30s --retries=5 \
    CMD wget -qO- http://127.0.0.1:5173/ || exit 1

CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0"]

# ---- build: production bundle ----------------------------------------------
FROM base AS build
ARG VITE_API_BASE_URL=/api
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
COPY . .
RUN npm run build

# ---- final: static files served by Nginx, non-root -------------------------
FROM nginx:1.27-alpine AS final

# The nginx:1.27-alpine tag is a frozen manifest - it doesn't pick up Alpine security
# patches released after that tag was last built. `apk upgrade` pulls current patched
# packages (e.g. openssl/libcrypto3/libssl3) from Alpine's repos at build time instead.
RUN apk update && apk upgrade --no-cache

RUN addgroup -g 1000 shopkeeper \
    && adduser -D -u 1000 -G shopkeeper shopkeeper \
    && rm -rf /usr/share/nginx/html/* \
    && rm /etc/nginx/conf.d/default.conf

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

RUN touch /var/run/nginx.pid \
    && chown -R shopkeeper:shopkeeper /var/run/nginx.pid /usr/share/nginx/html /var/cache/nginx /etc/nginx

USER shopkeeper
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -qO- http://127.0.0.1:8080/healthz || exit 1

CMD ["nginx", "-g", "daemon off;"]
