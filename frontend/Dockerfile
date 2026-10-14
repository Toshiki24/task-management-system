# 自己ホスト(Docker Compose など、AWS 非依存。M5 §8)向けのフロント/BFF イメージ。
# Next.js の standalone 出力を使い、Node ランタイムで `server.js` を起動する。
# ビルド: frontend ディレクトリで `docker build -t task-management-web:selfhost .`

FROM node:22-bookworm-slim AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci

FROM node:22-bookworm-slim AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
# standalone 出力を有効化してビルドする
ENV NEXT_OUTPUT=standalone
ENV NODE_ENV=production
RUN npm run build

FROM node:22-bookworm-slim AS runtime
WORKDIR /app
ENV NODE_ENV=production
# Next.js の standalone 出力(server.js + 最小限の node_modules)と静的資産をコピーする
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
COPY --from=build /app/public ./public

# 非 root ユーザー(node イメージ同梱)で実行する
USER node

ENV PORT=3000
EXPOSE 3000

CMD ["node", "server.js"]
