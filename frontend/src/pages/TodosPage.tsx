import { useTodos } from "../hooks/useTodos";
import { KanbanBoard } from "../components/KanbanBoard";
import type { AuthResponse } from "../types/AuthResponse";

interface TodosPageProps {
  auth: AuthResponse;
  onLogout: () => Promise<void>;
}

export function TodosPage({ auth, onLogout }: TodosPageProps) {
  const { todos, loading, error, clearError, moveCard, editCard, deleteCard, addTodo } = useTodos(auth.token);

  return (
    <div className="todos-page">
      <header className="todos-header">
        <h1>TaskMeister</h1>
        <div className="header-right">
          <span className="username">{auth.name}</span>
          <button className="btn-link" onClick={onLogout}>Log out</button>
        </div>
      </header>

      <main className="todos-main">
        {error && (
          <p className="form-error" role="alert">
            {error}{" "}
            <button className="btn-link" onClick={clearError}>✕</button>
          </p>
        )}

        {loading ? (
          <p className="status-message">Loading…</p>
        ) : (
          <KanbanBoard
            todos={todos}
            onMove={moveCard}
            onEdit={editCard}
            onDelete={deleteCard}
            onAdd={addTodo}
          />
        )}
      </main>
    </div>
  );
}
