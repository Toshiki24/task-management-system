// コメント用の最小 Markdown レンダラ(M3 §3)。
// 方針: 依存ライブラリを増やさず、XSS を防ぐため「まず全部エスケープ → 自分が作る限られたタグだけを挿入」する。
// 対応: 見出しなし。太字 **x** / 斜体 *x* / インラインコード `x` / コードブロック ```x``` /
//       リンク [text](url)(http/https のみ許可)/ 改行。

const PLACEHOLDER_OPEN = "";
const PLACEHOLDER_CLOSE = "";

function escapeHtml(text: string): string {
  return text
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

/** http/https のみ許可。それ以外(javascript: など)は null を返す。 */
function safeUrl(url: string): string | null {
  const trimmed = url.trim();
  return /^https?:\/\//i.test(trimmed) ? trimmed : null;
}

/** 正規表現で使う特殊文字をエスケープする。 */
function escapeRegExp(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/**
 * コメントを限定 Markdown で HTML 化する(M3 §3)。
 * mentionNames を渡すと、本文中の「@名前」をハイライト表示する(解決済みメンションのみ)。
 */
export function renderMarkdownToHtml(source: string, mentionNames: string[] = []): string {
  const tokens: string[] = [];
  const stash = (html: string): string => {
    tokens.push(html);
    return `${PLACEHOLDER_OPEN}${tokens.length - 1}${PLACEHOLDER_CLOSE}`;
  };

  let text = source ?? "";

  // 1. コードブロック(```...```)を退避(中身はエスケープ済みで <pre><code> 化)
  text = text.replace(/```\n?([\s\S]*?)```/g, (_m, code: string) =>
    stash(`<pre class="overflow-x-auto rounded bg-gray-100 p-2 text-xs"><code>${escapeHtml(code.replace(/\n$/, ""))}</code></pre>`),
  );

  // 2. インラインコード(`...`)を退避
  text = text.replace(/`([^`]+)`/g, (_m, code: string) =>
    stash(`<code class="rounded bg-gray-100 px-1 text-[0.85em]">${escapeHtml(code)}</code>`),
  );

  // 3. 残りのテキストをエスケープ
  text = escapeHtml(text);

  // 4. リンク [text](url)。url は http/https のみ、不正ならプレーン表示
  text = text.replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, (match, label: string, url: string) => {
    const href = safeUrl(url);
    if (!href) return match;
    return `<a href="${href}" target="_blank" rel="noopener noreferrer" class="text-blue-600 underline">${label}</a>`;
  });

  // 5. 太字・斜体(太字を先に)
  text = text.replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>");
  text = text.replace(/\*([^*]+)\*/g, "<em>$1</em>");

  // 6. @メンションのハイライト(解決済みの名前のみ。長い名前を優先して前方一致の誤検出を抑える)
  const names = [...mentionNames].filter((n) => n).sort((a, b) => b.length - a.length);
  for (const name of names) {
    const escapedName = escapeHtml(name);
    const pattern = new RegExp(`@${escapeRegExp(escapedName)}`, "g");
    text = text.replace(
      pattern,
      `<span class="rounded bg-blue-50 px-1 font-medium text-blue-700">@${escapedName}</span>`,
    );
  }

  // 7. 改行 → <br>
  text = text.replace(/\n/g, "<br>");

  // 7. 退避したコード片を戻す
  text = text.replace(
    new RegExp(`${PLACEHOLDER_OPEN}(\\d+)${PLACEHOLDER_CLOSE}`, "g"),
    (_m, index: string) => tokens[Number(index)] ?? "",
  );

  return text;
}
