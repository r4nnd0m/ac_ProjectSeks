# Baut das Frontend ohne Demo-Daten und liefert es mit nginx (unprivilegiert, Port 8080) aus.
FROM node:lts-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
ENV VITE_USE_MOCKS=false
RUN npm run build

FROM nginxinc/nginx-unprivileged:stable-alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 8080
CMD ["nginx", "-g", "daemon off;"]
