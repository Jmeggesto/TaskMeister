import { useState, useRef } from "react";
import { useDraggable } from "@dnd-kit/core";
import { CSS } from "@dnd-kit/utilities";
import type { Todo } from "../types/Todo";

interface TodoCardProps {
  todo: Todo;
  isDragOverlay?: boolean;
  onEdit?:   (id: number, title: string) => Promise<void>;
  onDelete?: (id: number)                => Promise<void>;
}

const PencilIcon = () => (
  <svg width="13" height="13" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <path d="M11.5 2.5l2 2L5 13H3v-2L11.5 2.5z" />
  </svg>
);

const XIcon = () => (
  <svg width="13" height="13" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" aria-hidden="true">
    <path d="M3 3l10 10M13 3L3 13" />
  </svg>
);

export function TodoCard({ todo, isDragOverlay = false, onEdit, onDelete }: TodoCardProps) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(todo.title);
  const [saving, setSaving] = useState(false);
  const inputRef = useRef<HTMLTextAreaElement>(null);
  // Ref guard prevents onBlur from double-firing a save that Enter already started
  const savingRef = useRef(false);

  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: todo.id,
    disabled: editing,
  });

  const classes = [
    "todo-card",
    isDragging    && "is-dragging",
    isDragOverlay && "is-overlay",
    editing       && "is-editing",
  ].filter(Boolean).join(" ");

  // Prevent the action buttons' pointer events from initiating a drag
  const blockDrag = (e: React.PointerEvent) => e.stopPropagation();

  const startEdit = (e: React.PointerEvent) => {
    e.stopPropagation();
    setDraft(todo.title);
    setEditing(true);
    setTimeout(() => {
      inputRef.current?.focus();
      inputRef.current?.select();
    }, 0);
  };

  const cancelEdit = () => {
    setEditing(false);
    setDraft(todo.title);
  };

  const saveEdit = async () => {
    // Guard against double-fire: Enter triggers saveEdit, then blur fires on the
    // same event loop tick before React has re-rendered with editing=false.
    if (savingRef.current) return;
    const title = draft.trim();
    if (!title || title === todo.title) { cancelEdit(); return; }
    savingRef.current = true;
    setSaving(true);
    try {
      await onEdit?.(todo.id, title);
      setEditing(false);
    } finally {
      savingRef.current = false;
      setSaving(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); saveEdit(); }
    if (e.key === "Escape") cancelEdit();
  };

  return (
    <div
      ref={isDragOverlay ? undefined : setNodeRef}
      className={classes}
      style={{ transform: CSS.Translate.toString(transform) }}
      {...(isDragOverlay || editing ? {} : { ...listeners, ...attributes })}
    >
      {/* Action buttons — hidden until hover via CSS, not rendered in the DragOverlay clone */}
      {!isDragOverlay && !editing && (
        <div className="todo-card-actions">
          <button
            className="todo-card-btn todo-card-btn--edit"
            onPointerDown={startEdit}
            aria-label="Edit todo"
          >
            <PencilIcon />
          </button>
          <button
            className="todo-card-btn todo-card-btn--delete"
            onPointerDown={blockDrag}
            onClick={() => onDelete?.(todo.id)}
            aria-label="Delete todo"
          >
            <XIcon />
          </button>
        </div>
      )}

      {editing ? (
        <textarea
          ref={inputRef}
          className="todo-card-input"
          value={draft}
          onChange={e => setDraft(e.target.value)}
          onKeyDown={handleKeyDown}
          onBlur={saveEdit}
          maxLength={500}
          disabled={saving}
          rows={2}
        />
      ) : (
        <span className="todo-card-title">{todo.title}</span>
      )}
    </div>
  );
}
