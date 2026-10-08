"use client";

import { useEffect, useLayoutEffect, useRef, useState, type KeyboardEvent } from "react";
import type { Member } from "@/types/member";

interface MentionTextareaProps {
  value: string;
  onChange: (value: string) => void;
  members: Member[];
  placeholder?: string;
  rows?: number;
  ariaLabel?: string;
  maxLength?: number;
}

// 直近の「@クエリ」を取り出す(キャレット直前のテキストから)
const MENTION_RE = /@([^\s@]*)$/;

export function MentionTextarea({
  value,
  onChange,
  members,
  placeholder,
  rows = 2,
  ariaLabel,
  maxLength = 1000,
}: MentionTextareaProps) {
  const ref = useRef<HTMLTextAreaElement>(null);
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [activeIndex, setActiveIndex] = useState(0);
  const [caret, setCaret] = useState<number | null>(null);

  const matches = open
    ? members.filter((m) => m.name.toLowerCase().includes(query.toLowerCase())).slice(0, 8)
    : [];

  // 値を書き換えたあとにキャレット位置を復元する
  useLayoutEffect(() => {
    if (caret !== null && ref.current) {
      ref.current.selectionStart = caret;
      ref.current.selectionEnd = caret;
      setCaret(null);
    }
  }, [caret]);

  useEffect(() => {
    setActiveIndex(0);
  }, [query, open]);

  function refreshMentionState(text: string, selectionStart: number) {
    const before = text.slice(0, selectionStart);
    const m = MENTION_RE.exec(before);
    if (m) {
      setQuery(m[1]);
      setOpen(true);
    } else {
      setOpen(false);
    }
  }

  function handleChange(e: React.ChangeEvent<HTMLTextAreaElement>) {
    const text = e.target.value;
    onChange(text);
    refreshMentionState(text, e.target.selectionStart ?? text.length);
  }

  function selectMember(member: Member) {
    const el = ref.current;
    const selectionStart = el?.selectionStart ?? value.length;
    const before = value.slice(0, selectionStart);
    const after = value.slice(selectionStart);
    const m = MENTION_RE.exec(before);
    if (!m) return;
    const start = before.length - m[0].length;
    const inserted = `@${member.name} `;
    const next = before.slice(0, start) + inserted + after;
    onChange(next);
    setOpen(false);
    setCaret(start + inserted.length);
  }

  function handleKeyDown(e: KeyboardEvent<HTMLTextAreaElement>) {
    if (!open || matches.length === 0) return;
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setActiveIndex((i) => Math.min(i + 1, matches.length - 1));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setActiveIndex((i) => Math.max(i - 1, 0));
    } else if (e.key === "Enter" || e.key === "Tab") {
      e.preventDefault();
      selectMember(matches[activeIndex]);
    } else if (e.key === "Escape") {
      setOpen(false);
    }
  }

  return (
    <div className="relative">
      <textarea
        ref={ref}
        rows={rows}
        maxLength={maxLength}
        value={value}
        onChange={handleChange}
        onKeyDown={handleKeyDown}
        onBlur={() => setTimeout(() => setOpen(false), 120)}
        placeholder={placeholder}
        aria-label={ariaLabel}
        className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
      />
      {open && matches.length > 0 && (
        <ul
          role="listbox"
          aria-label="メンション候補"
          className="absolute z-10 mt-1 max-h-48 w-56 overflow-y-auto rounded-md border border-gray-200 bg-white shadow-lg"
        >
          {matches.map((member, index) => (
            <li key={member.userId}>
              <button
                type="button"
                // onMouseDown はテキストエリアの blur より先に発火するので候補が消えない
                onMouseDown={(e) => {
                  e.preventDefault();
                  selectMember(member);
                }}
                onMouseEnter={() => setActiveIndex(index)}
                className={`block w-full px-3 py-1.5 text-left text-sm ${
                  index === activeIndex ? "bg-blue-50" : "hover:bg-gray-50"
                }`}
              >
                <span className="text-gray-900">@{member.name}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
