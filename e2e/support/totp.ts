import { createHmac } from "node:crypto";

const ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

/** RFC 4648 Base32 デコード(パディング・空白を許容)。 */
function base32Decode(input: string): Buffer {
  const normalized = input.trim().replace(/=+$/, "").replace(/\s/g, "").toUpperCase();
  const bytes: number[] = [];
  let buffer = 0;
  let bitsLeft = 0;
  for (const ch of normalized) {
    const index = ALPHABET.indexOf(ch);
    if (index < 0) {
      throw new Error(`Base32 として不正な文字: ${ch}`);
    }
    buffer = (buffer << 5) | index;
    bitsLeft += 5;
    if (bitsLeft >= 8) {
      bytes.push((buffer >> (bitsLeft - 8)) & 0xff);
      bitsLeft -= 8;
    }
  }
  return Buffer.from(bytes);
}

/**
 * RFC 6238 の TOTP コードを計算する(HMAC-SHA1・30秒ステップ・6桁)。
 * バックエンドの Totp 実装と同じ既定で、E2E からコードを生成するために使う。
 */
export function totp(secretBase32: string, at: Date = new Date()): string {
  const key = base32Decode(secretBase32);
  let counter = Math.floor(at.getTime() / 1000 / 30);

  const counterBytes = Buffer.alloc(8);
  for (let i = 7; i >= 0; i--) {
    counterBytes[i] = counter & 0xff;
    counter = Math.floor(counter / 256);
  }

  const hash = createHmac("sha1", key).update(counterBytes).digest();
  const offset = hash[hash.length - 1] & 0x0f;
  const binary =
    ((hash[offset] & 0x7f) << 24) |
    ((hash[offset + 1] & 0xff) << 16) |
    ((hash[offset + 2] & 0xff) << 8) |
    (hash[offset + 3] & 0xff);

  return (binary % 1_000_000).toString().padStart(6, "0");
}
