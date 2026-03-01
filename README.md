# Todo App — React + .NET 10 + SQLite

A minimal full-stack todo application. The backend is a .NET 10 Web API using Entity Framework Core with SQLite. The frontend is a Vite + React app.

---

## Project structure

```
TaskMeister/
├── backend/          .NET 10 Web API
│   ├── Controllers/  TodosController
│   ├── Data/         AppDbContext (EF Core)
│   ├── Models/       TodoItem
│   └── Program.cs    App entry point & DI setup
└── frontend/         Vite + React
    └── src/
        ├── api/      todosApi.js  (fetch wrapper)
        └── App.jsx   Main UI
```

---

## Running the backend

> Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10)

```bash
cd backend
dotnet run
```

The API starts on **http://localhost:5000** by default. SQLite creates `todos.db` automatically in the project directory on first run.

### API endpoints

| Method | Path             | Description        |
|--------|------------------|--------------------|
| GET    | /api/todos       | List all todos     |
| GET    | /api/todos/{id}  | Get one todo       |
| POST   | /api/todos       | Create a todo      |
| PUT    | /api/todos/{id}  | Update a todo      |
| DELETE | /api/todos/{id}  | Delete a todo      |

Swagger UI is available at **http://localhost:5000/swagger** in development.

---

## Running the frontend

> Requires [Node.js 18+](https://nodejs.org)

```bash
cd frontend
npm install      # first time only
npm run dev
```

The app opens on **http://localhost:5173** and talks to the API at `http://localhost:5000`.

---

## Adding EF Core migrations (optional)

If you want schema migrations instead of `EnsureCreated`:

```bash
cd backend
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Then remove the `db.Database.EnsureCreated()` call in `Program.cs`.
