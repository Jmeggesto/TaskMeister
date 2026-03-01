import { request } from "./apiClient";
import type { Todo } from "../types/Todo";

export const fetchTodos = (token: string) =>
  request<Todo[]>("/todos", { token });

export const createTodo = (title: string, token: string) =>
  request<Todo>("/todos", { method: "POST", body: { title }, token });

export const updateTodo = (id: number, updates: Pick<Todo, "title" | "status">, token: string) =>
  request<Todo>(`/todos/${id}`, { method: "PUT", body: updates, token });

export const deleteTodo = (id: number, token: string) =>
  request<void>(`/todos/${id}`, { method: "DELETE", token });
