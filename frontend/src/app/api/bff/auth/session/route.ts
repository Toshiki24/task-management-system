import { NextResponse } from "next/server";
import { backendUnavailable, unauthorized } from "@/lib/server/responses";
import { getSession } from "@/lib/server/session";
import { BackendUnavailableError, getValidAccessToken } from "@/lib/server/tokens";

/**
 * ログイン状態の確認。画面の表示前に呼び出し、ログイン中のユーザー情報を返す。
 * アクセストークンの期限が切れていればここで再発行し、再発行できなければ401を返す。
 */
export async function GET() {
  const session = await getSession();

  try {
    if (!(await getValidAccessToken(session))) {
      return unauthorized();
    }
  } catch (error) {
    if (error instanceof BackendUnavailableError) {
      return backendUnavailable();
    }
    throw error;
  }

  return NextResponse.json({ user: session.user });
}
