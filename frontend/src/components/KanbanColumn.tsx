import { useState, useRef } from "react";
import { useDroppable } from "@dnd-kit/core";
import { TodoCard } from "./TodoCard";
import type { Todo, TodoStatus } from "../types/Todo";

// ─── AddCardForm ──────────────────────────────────────────────────────────────
// Isolated sub-component so its state never exists inside columns that don't need it.

interface AddCardFormProps {
  onAdd: (title: string) => Promise<void>;
}

function AddCardForm({ onAdd }: AddCardFormProps) {
  const [adding, setAdding]   = useState(false);
  const [draft, setDraft]     = useState("");
  const [saving, setSaving]   = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  const openForm = () => {
    setAdding(true);
    setTimeout(() => inputRef.current?.focus(), 0);
  };

  const cancelForm = () => {
    setAdding(false);
    setDraft("");
  };

  const submitForm = async () => {
    const title = draft.trim();
    if (!title) return;
    setSaving(true);
    try {
      await onAdd(title);
      setDraft("");
      setAdding(false);
    } finally {
      setSaving(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter")  submitForm();
    if (e.key === "Escape") cancelForm();
  };

  if (!adding) {
    return (
      <button className="kanban-add-trigger" onClick={openForm}>
        + Add todo
      </button>
    );
  }

  return (
    <div className="kanban-add-form">
      <input
        ref={inputRef}
        className="kanban-add-input"
        placeholder="Todo title…"
        value={draft}
        onChange={e => setDraft(e.target.value)}
        onKeyDown={handleKeyDown}
        maxLength={500}
        disabled={saving}
      />
      <div className="kanban-add-actions">
        <button
          className="btn-primary kanban-add-submit"
          onClick={submitForm}
          disabled={!draft.trim() || saving}
        >
          {saving ? "Adding…" : "Add"}
        </button>
        <button className="btn-link" onClick={cancelForm} disabled={saving}>
          Cancel
        </button>
      </div>
    </div>
  );
}

// ─── KanbanColumn ─────────────────────────────────────────────────────────────

interface KanbanColumnProps {
  status:   TodoStatus;
  label:    string;
  todos:    Todo[];
  onEdit:   (id: number, title: string) => Promise<void>;
  onDelete: (id: number)                => Promise<void>;
  onAdd?:   (title: string)             => Promise<void>; // only provided for "Not Started"
}

export function KanbanColumn({ status, label, todos, onEdit, onDelete, onAdd }: KanbanColumnProps) {
  const { setNodeRef, isOver } = useDroppable({ id: status });

  return (
    <div className={`kanban-column${isOver ? " is-over" : ""}`}>
      <div className="kanban-column-header">
        <span className="kanban-column-label">{label}</span>
        <span className="kanban-column-count">{todos.length}</span>
      </div>

      <div ref={setNodeRef} className="kanban-column-body">
        {todos.length === 0 && !onAdd && (
          <p className="kanban-column-empty">Nothing here yet.</p>
        )}
        {todos.map(todo => (
          <TodoCard key={todo.id} todo={todo} onEdit={onEdit} onDelete={onDelete} />
        ))}

        {onAdd && (
          <div className="kanban-add-area">
            <AddCardForm onAdd={onAdd} />
          </div>
        )}
      </div>
    </div>
  );
}
