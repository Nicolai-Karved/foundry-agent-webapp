FROM node:22-alpine

WORKDIR /app/frontend

# Install dependencies into image layer for better first-run performance
COPY frontend/package*.json ./
RUN npm install --legacy-peer-deps

EXPOSE 5173

ENV CHOKIDAR_USEPOLLING=true
ENV WATCHPACK_POLLING=true

# Keep dependency updates easy: always sync deps on start, then run Vite dev server
CMD ["sh", "-c", "npm install --legacy-peer-deps && npm run dev -- --host 0.0.0.0 --port 5173"]
