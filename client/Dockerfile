# --- build stage: compile the Vite app to static assets ---
FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
# VITE_API_BASE_URL is left empty on purpose: the app calls same-origin "/api", which nginx (below) reverse-proxies
# to the API. This keeps the browser same-origin (no CORS) on the deployed site. Override at build time only if the
# API lives on a different origin: `--build-arg VITE_API_BASE_URL=https://api.example.com`.
ARG VITE_API_BASE_URL=""
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

# Entra ID (Azure AD) SSO — build args, not runtime env (Vite inlines VITE_* at build time). Leave empty to keep SSO
# disabled (the login screen falls back to a friendly message and the guest path stays fully usable).
ARG VITE_AZURE_AD_TENANT_ID=""
ARG VITE_AZURE_AD_CLIENT_ID=""
ENV VITE_AZURE_AD_TENANT_ID=$VITE_AZURE_AD_TENANT_ID
ENV VITE_AZURE_AD_CLIENT_ID=$VITE_AZURE_AD_CLIENT_ID

RUN npm run build

# --- runtime stage: serve static assets + proxy /api ---
FROM nginx:1.27-alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/templates/default.conf.template

# The API upstream is templated from env so the same image works across hosts (Render/Northflank/local compose).
# Defaults suit docker-compose (service name "api" on 8080). Nginx's entrypoint renders templates on start.
ENV API_UPSTREAM=http://api:8080
ENV PORT=80
EXPOSE 80

# nginx:alpine's docker-entrypoint runs envsubst over /etc/nginx/templates/*.template into /etc/nginx/conf.d/.
