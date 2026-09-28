import { dirname } from "path";
import { fileURLToPath } from "url";
import { FlatCompat } from "@eslint/eslintrc";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

// eslint-config-next 15 は従来形式(.eslintrc)の設定のため、FlatCompat で flat config に変換して読み込む
const compat = new FlatCompat({
  baseDirectory: __dirname,
});

const eslintConfig = [
  ...compat.extends("next/core-web-vitals", "next/typescript"),
  {
    // XSS対策(security-review.md 5.5)
    rules: {
      // HTMLをエスケープせずに埋め込む dangerouslySetInnerHTML を禁止する
      "react/no-danger": "error",
      // href 等に javascript: スキームのURLを書くことを禁止する
      "react/jsx-no-script-url": "error",
    },
  },
  {
    ignores: [
      "node_modules/**",
      ".next/**",
      // E2Eテスト用のビルド出力先(next.config.ts の distDir)
      ".next-e2e/**",
      "out/**",
      "build/**",
      "next-env.d.ts",
    ],
  },
];

export default eslintConfig;
