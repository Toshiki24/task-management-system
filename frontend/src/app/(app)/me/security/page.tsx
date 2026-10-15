"use client";

import { MfaSettings } from "@/components/settings/MfaSettings";
import { NotificationSettings } from "@/components/settings/NotificationSettings";

export default function SettingsPage() {
  return (
    <div className="mx-auto max-w-2xl space-y-8">
      <h1 className="text-lg font-bold text-gray-900">設定</h1>
      <NotificationSettings />
      <MfaSettings />
    </div>
  );
}
