# Cikada.MES remote MCP server. Build context: the REPOSITORY ROOT, because the image also carries the user manuals index built from
# docs/manuals/docs (the filter is Dockerfile.dockerignore next to this file).
#   docker build -f src/Mcp/Cikada.Mcp/Dockerfile -t lkvitai-mes-mcp .
FROM node:22-alpine AS build
WORKDIR /repo/src/Mcp/Cikada.Mcp
COPY src/Mcp/Cikada.Mcp/package.json src/Mcp/Cikada.Mcp/package-lock.json src/Mcp/Cikada.Mcp/tsconfig.json ./
RUN npm ci --ignore-scripts --no-audit --no-fund
COPY src/Mcp/Cikada.Mcp/src ./src
COPY src/Mcp/Cikada.Mcp/test ./test
COPY docs/manuals/docs /repo/docs/manuals/docs
ARG GIT_SHA=
# data/docs-index.json (+ docs-manifest.json) is generated here from the manuals, never committed
RUN npm run build && GIT_SHA="$GIT_SHA" npm run docs:index && npm prune --omit=dev

FROM node:22-alpine
ENV NODE_ENV=production \
    MCP_PORT=8080
WORKDIR /app
COPY --from=build --chown=node:node /repo/src/Mcp/Cikada.Mcp/package.json ./package.json
COPY --from=build --chown=node:node /repo/src/Mcp/Cikada.Mcp/node_modules ./node_modules
COPY --from=build --chown=node:node /repo/src/Mcp/Cikada.Mcp/dist/src ./dist/src
COPY --from=build --chown=node:node /repo/src/Mcp/Cikada.Mcp/data ./data
# the manual screenshots behind docs_image
COPY --from=build --chown=node:node /repo/docs/manuals/docs/img ./data/docs-img
USER node
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD node -e "fetch('http://127.0.0.1:8080/healthz').then(r=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))"
CMD ["node", "dist/src/main.js"]
