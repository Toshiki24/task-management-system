import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import { headers } from "next/headers";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "案件・タスク管理システム",
  description: "案件・タスク管理システム",
};

export default async function RootLayout({ children }: LayoutProps<"/">) {
  // リクエストヘッダーを参照して動的レンダリングにする。これにより middleware が
  // リクエストごとに設定した CSP の nonce が Next.js のスクリプトに付与される(SEC2-10)。
  await headers();

  return (
    <html
      lang="ja"
      className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}
    >
      <body className="min-h-full flex flex-col">{children}</body>
    </html>
  );
}
