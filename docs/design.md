# Design

## Project structure

```
TaskMeister/
├── backend/
│   ├── src/TaskMeisterAPI/
│   │   ├── Configuration/        Strongly-typed options (JwtOptions, DatabaseOptions, …)
│   │   ├── Controllers/          HTTP Controllers to handle request/response layer
│   │   ├── Data/                 AppDbContext (EF Core + SQLite)
│   │   ├── Infrastructure/
│   │   │   ├── Auth/             JWT current-user resolution, token validation
│   │   │   └── ModelBinding/     [FromUser] model binder
│   │   ├── Models/               DB entities, request objects, response classes
│   │   ├── Services/             Service layer to perform business logic
│   │   └── Program.cs            App entry point & DI setup
│   └── tests/TaskMeisterAPI.Tests/
│       ├── Integration/          WebApplicationFactory-based API tests
│       └── Unit/                 Service unit tests
└── frontend/
    └── src/
        ├── api/                  Fetch wrappers (todosApi, usersApi)
        ├── components/           KanbanBoard, KanbanColumn, TodoCard
        ├── hooks/                useTodos (optimistic updates), useAuth
        ├── pages/                TodosPage, LandingPage
        └── types/                Todo, AuthResponse
```

---

## API reference

All `/api/todos` endpoints require a `Bearer` token (obtained from login).

### Users

| Method | Path                 | Auth | Description       |
|--------|----------------------|------|-------------------|
| POST   | `/api/users/signup`  | —    | Create an account |
| POST   | `/api/users/login`   | —    | Get a JWT token   |
| POST   | `/api/users/logout`  | ✓    | Invalidate token  |

### Todos

| Method | Path               | Auth | Description            |
|--------|--------------------|------|------------------------|
| GET    | `/api/todos`       | ✓    | List all todos         |
| GET    | `/api/todos/{id}`  | ✓    | Get a single todo      |
| POST   | `/api/todos`       | ✓    | Create a todo          |
| PUT    | `/api/todos/{id}`  | ✓    | Update title or status |
| DELETE | `/api/todos/{id}`  | ✓    | Delete a todo          |

Todo status values: `not_started` · `in_progress` · `done`

---

## Thought process, assumptions, and tradeoffs

<!--
    Fill this section in before submitting. Suggested topics:

    - Why SQLite over EF Core in-memory
    - Why JWT for auth, and the PBKDF2 password hashing choice
    - Token storage (localStorage) — known tradeoff and what the production alternative would be
    - What "production-ready MVP" meant for this project specifically
    - Tradeoffs made (no pagination, no refresh tokens, no soft deletes, etc.)
    - Scalability considerations
    - What you would implement next given more time
-->
