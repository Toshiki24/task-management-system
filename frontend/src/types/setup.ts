/** 初回セットアップ(M5 §8)の型。 */

/**
 * セットアップ状態。
 * - required: ユーザー未作成で、初回セットアップが必要
 * - not-required: 既にセットアップ済み、または機能無効(AWS 本番など)
 * - unknown: 状態を確認できない(DB障害など)。安全側に倒して作成フォームは出さない
 */
export type SetupStatus = "required" | "not-required" | "unknown";

export interface CreateInitialAdminInput {
  name: string;
  email: string;
  password: string;
}
