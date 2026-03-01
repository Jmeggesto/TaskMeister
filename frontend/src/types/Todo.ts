/** Mirrors backend Models/Entities/TodoItem.cs + TodoStatus.cs */

export type TodoStatus = "not_started" | "in_progress" | "done";

export interface Todo {
  id: number;
  title: string;
  status: TodoStatus;
  createdAt: string; // ISO 8601 date string
}
