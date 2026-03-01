import { useState } from "react";
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  useSensor,
  useSensors,
  type DragStartEvent,
  type DragEndEvent,
} from "@dnd-kit/core";
import { KanbanColumn } from "./KanbanColumn";
import { TodoCard } from "./TodoCard";
import type { Todo, TodoStatus } from "../types/Todo";

const COLUMNS: { status: TodoStatus; label: string }[] = [
  { status: "not_started", label: "Not Started" },
  { status: "in_progress", label: "In Progress" },
  { status: "done",        label: "Done" },
];

// Module-level stub — the DragOverlay clone is purely visual and never fires callbacks
const NOOP = async () => {};

interface KanbanBoardProps {
  todos: Todo[];
  onMove:   (id: number, status: TodoStatus) => Promise<void>;
  onEdit:   (id: number, title: string)      => Promise<void>;
  onDelete: (id: number)                     => Promise<void>;
  onAdd:    (title: string)                  => Promise<void>;
}

export function KanbanBoard({ todos, onMove, onEdit, onDelete, onAdd }: KanbanBoardProps) {
  const [activeId, setActiveId] = useState<number | null>(null);

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } })
  );

  // Resolves to the dragged todo, or null when nothing is being dragged
  const activeTodo = todos.find(t => t.id === activeId) ?? null;

  const handleDragStart = ({ active }: DragStartEvent) => {
    setActiveId(active.id as number);
  };

  const handleDragEnd = ({ active, over }: DragEndEvent) => {
    setActiveId(null);
    if (!over) return;
    const newStatus = over.id as TodoStatus;
    const todo = todos.find(t => t.id === (active.id as number));
    if (todo && todo.status !== newStatus) {
      onMove(todo.id, newStatus);
    }
  };

  return (
    <DndContext sensors={sensors} onDragStart={handleDragStart} onDragEnd={handleDragEnd}>
      <div className="kanban-board">
        {COLUMNS.map(col => (
          <KanbanColumn
            key={col.status}
            status={col.status}
            label={col.label}
            todos={todos
              .filter(t => t.status === col.status)
              .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())}
            onEdit={onEdit}
            onDelete={onDelete}
            onAdd={col.status === "not_started" ? onAdd : undefined}
          />
        ))}
      </div>

      <DragOverlay dropAnimation={{ duration: 200, easing: "ease" }}>
        {activeTodo
          ? <TodoCard todo={activeTodo} isDragOverlay onEdit={NOOP} onDelete={NOOP} />
          : null}
      </DragOverlay>
    </DndContext>
  );
}
