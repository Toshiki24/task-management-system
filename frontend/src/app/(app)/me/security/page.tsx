"use client";

import { MfaSettings } from "@/components/settings/MfaSettings";

export default function SecuritySettingsPage() {
  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <h1 className="text-lg font-bold text-gray-900">セキュリティ設定</h1>
      <MfaSettings />
    </div>
  );
}
