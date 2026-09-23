export function Loading({ label = "データを読み込んでいます..." }: { label?: string }) {
  return <p className="text-sm text-gray-500">{label}</p>;
}
