"use client";

import { useState } from "react";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Modal } from "@/components/common/Modal";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import type { ImportResult } from "@/types/metrics";

interface TaskImportModalProps {
  projectId: string;
  isOpen: boolean;
  onClose: () => void;
  /** 取り込み成功後にタスク一覧を再取得する。 */
  onImported: () => Promise<void> | void;
}

/** CSV からタスクを一括取り込みする(M5 §4)。列順はエクスポートに合わせる。 */
export function TaskImportModal({ projectId, isOpen, onClose, onImported }: TaskImportModalProps) {
  const [file, setFile] = useState<File | null>(null);
  const [result, setResult] = useState<ImportResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  function reset() {
    setFile(null);
    setResult(null);
    setError(null);
  }

  async function handleImport() {
    if (!file) {
      setError("CSV ファイルを選択してください。");
      return;
    }
    setError(null);
    setResult(null);
    setIsSubmitting(true);
    try {
      const csv = await file.text();
      const res = await apiFetch<ImportResult>(`/projects/${projectId}/tasks/import`, {
        method: "POST",
        headers: { "Content-Type": "text/csv" },
        body: csv,
      });
      setResult(res);
      await onImported();
    } catch (err) {
      setError(formatApiErrorMessage(err, "CSV の取り込みに失敗しました。"));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <Modal
      isOpen={isOpen}
      title="CSV インポート"
      onClose={() => {
        reset();
        onClose();
      }}
    >
      <p className="mb-3 text-sm text-gray-600">
        エクスポートと同じ列（タイトル・状態・担当者・優先度・期限・見積・ラベル）の CSV を取り込みます。担当者は取り込み対象外です。
      </p>

      {error && <ErrorMessage message={error} />}

      <input
        type="file"
        accept=".csv,text/csv"
        aria-label="CSV ファイル"
        onChange={(e) => {
          setResult(null);
          setFile(e.target.files?.[0] ?? null);
        }}
        className="mb-4 block w-full text-sm text-gray-700"
      />

      {result && (
        <div className="mb-4 rounded-md border border-gray-200 p-3 text-sm">
          <p className="font-medium text-gray-900">{result.imported} 件を取り込みました。</p>
          {result.failed.length > 0 && (
            <>
              <p className="mt-2 text-red-600">{result.failed.length} 件は取り込めませんでした：</p>
              <ul className="mt-1 max-h-40 list-inside list-disc overflow-y-auto text-gray-700">
                {result.failed.map((f) => (
                  <li key={f.row}>
                    {f.row} 行目：{f.message}
                  </li>
                ))}
              </ul>
            </>
          )}
        </div>
      )}

      <div className="mt-2 flex justify-end gap-3">
        <Button
          type="button"
          variant="secondary"
          onClick={() => {
            reset();
            onClose();
          }}
          disabled={isSubmitting}
        >
          閉じる
        </Button>
        <Button type="button" variant="primary" onClick={handleImport} disabled={isSubmitting || !file}>
          {isSubmitting ? "取り込み中..." : "取り込む"}
        </Button>
      </div>
    </Modal>
  );
}
