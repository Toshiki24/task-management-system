import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // E2Eテストでは開発サーバー(.next)と衝突しないよう、別の出力先でビルド・起動する
  distDir: process.env.NEXT_DIST_DIR || ".next",
};

export default nextConfig;
