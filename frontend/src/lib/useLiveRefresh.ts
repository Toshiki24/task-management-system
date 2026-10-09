"use client";

import { useEffect, useRef } from "react";

interface LiveRefreshOptions {
  /** 定期再取得の間隔(ミリ秒)。既定 15 秒。 */
  intervalMs?: number;
  /** false の間は再取得を止める(モーダル表示中など)。既定 true。 */
  enabled?: boolean;
}

/**
 * 他者の変更を自分の画面へ反映するための軽量リアルタイム更新(M3 §7)。
 *
 * 本番は API Gateway(HTTP)+Lambda のため、WebSocket ではなく
 * 「ウィンドウフォーカス時＋一定間隔での再取得」で最新化する。
 * タブが非表示の間は無駄な通信をしないよう再取得しない。
 *
 * refresh は最新のクロージャを ref で保持するため、呼び出し側で
 * useCallback により安定化させる必要はない。
 */
export function useLiveRefresh(refresh: () => void | Promise<void>, options: LiveRefreshOptions = {}) {
  const { intervalMs = 15000, enabled = true } = options;
  const saved = useRef(refresh);
  saved.current = refresh;

  useEffect(() => {
    if (!enabled) {
      return;
    }

    // タブが表示されているときだけ再取得する
    const run = () => {
      if (typeof document !== "undefined" && document.visibilityState === "visible") {
        void saved.current();
      }
    };

    const handle = window.setInterval(run, intervalMs);
    window.addEventListener("focus", run);
    document.addEventListener("visibilitychange", run);

    return () => {
      window.clearInterval(handle);
      window.removeEventListener("focus", run);
      document.removeEventListener("visibilitychange", run);
    };
  }, [intervalMs, enabled]);
}
