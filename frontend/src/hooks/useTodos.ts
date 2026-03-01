import { useState, useEffect, useCallback } from "react";
import { fetchTodos, createTodo, updateTodo, deleteTodo } from "../api/todosApi";
import type { Todo, TodoStatus } from "../types/Todo";

export function useTodos(token: string) {
  const [todos, setTodos] = useState<Todo[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await fetchTodos(token);
      setTodos(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load todos");
    } finally {
      setLoading(false);
    }
  }, [token]);

  useEffect(() => { load(); }, [load]);

  /**
   * Applies an optimistic state update, fires an API call, and reverts on failure.
   * `apiCall` receives the pre-update snapshot so mutations can read the original values
   * (e.g. fetching a todo's title before it was overwritten).
   */
  const withOptimistic = useCallback(async (
    apply:    (snapshot: Todo[]) => Todo[],
    apiCall:  (snapshot: Todo[]) => Promise<unknown>,
    errorMsg: string,
  ) => {
    const snapshot = todos;
    setTodos(apply(snapshot));
    try {
      await apiCall(snapshot);
    } catch (err) {
      setTodos(snapshot);
      setError(err instanceof Error ? err.message : errorMsg);
    }
  }, [todos]);

  const moveCard = useCallback((id: number, status: TodoStatus) =>
    withOptimistic(
      ts => ts.map(t => t.id === id ? { ...t, status } : t),
      snapshot => {
        const todo = snapshot.find(t => t.id === id)!;
        return updateTodo(id, { title: todo.title, status }, token);
      },
      "Failed to move card",
    ), [withOptimistic, token]);

  const editCard = useCallback((id: number, title: string) =>
    withOptimistic(
      ts => ts.map(t => t.id === id ? { ...t, title } : t),
      snapshot => {
        const todo = snapshot.find(t => t.id === id)!;
        return updateTodo(id, { title, status: todo.status }, token);
      },
      "Failed to update card",
    ), [withOptimistic, token]);

  const deleteCard = useCallback((id: number) =>
    withOptimistic(
      ts => ts.filter(t => t.id !== id),
      () => deleteTodo(id, token),
      "Failed to delete card",
    ), [withOptimistic, token]);

  const addTodo = useCallback(async (title: string) => {
    try {
      const created = await createTodo(title, token);
      setTodos(ts => [created, ...ts]);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create todo");
    }
  }, [token]);

  return { todos, loading, error, clearError: () => setError(null), moveCard, editCard, deleteCard, addTodo };
}
