# TaskMeister

A full-stack task management app with a Kanban board UI. Users sign up, log in, and manage their todos across three columns — **Not Started**, **In Progress**, and **Done** — with drag-and-drop reordering.

**Stack:** .NET 10 Web API · Entity Framework Core · SQLite · React 19 · Vite · TypeScript · dnd-kit

---

- [Setup](docs/setup.md) — running with Docker or locally, environment variables, running tests
- [Design](docs/design.md) — project structure, API reference

## Thought process

My approach was simple -- scaffold out the desired project structure and fill in over time.

I determined early on that separating out `backend/` and `frontend/` would make the most sense for a project
of this limited scope and scale. 

For the backend, I saw conflicting opinions on how to layout a .NET REST API -- some were more like what I implemented, some followed a stricter "Clean Architecture" pattern. I went with simplicity -- combine models, services, validators, etc all in one backend project.

A Service layer to sit below the Controller layer felt like the right approach for this -- we don't do a whole lot of complicated stuff in the DB, so handing things off to the Service layer served as a way to separate concerns.

Program.cs handles all the configuration for the actual web service. That might not be the best choice in production, where you might have a LOT of setup to do, but for this I determined it was fine.

I used SQLite because it seemed easier to manage, and also allowed me to retain a .db file that would persist between restarts.

I used React because, despite not being as frontend-savvy as most, it's the frontend framework I'm most used to.

I chose a Kanban board approach with draggable/droppable todo cards for the frontend. I both thought it was a cute style, and with existing React libraries it proved easy enough to cobble together. 

PBKDF2 password hashing with SHA256 and 600,000 iterations was chosen because that's a fairly standard password hashing scheme.

We're doing fairly light logging -- we use an ILogger via Dependency Injection but it's not used very heavily.

There's little in the way of caching right now because there's not much to cache, but as the app grows we'd need to seriously think about what we do and don't want to cache.

## Trade-Offs

- The backend uses JWTs with versioning for auth, incrementing token version on the User object on logout. This wouldn't hold up in production, but it's quick enough to implement the minimum behaviour of auth invalidation. A better solution would be to use refresh tokens, but I wanted to keep things simple initially.
- Additionally, the frontend stores the JWT directly in local storage, which leaves the app vulnerable to malicious scripts grabbing the JWT. That also wouldn't hold up in production. The alternative could be a HttpOnly cookie set by the API, or short-lived memory token + refresh token in a cookie.
- A lot of config variables (such as iterations for password hashing) are stored directly in the class that uses them -- that's brittle. We don't have a very robust way of providing each component with the config data it needs via Dependency Injection, but I felt it was fine for MVP.
- Our SQLite usage doesn't use migrations, and changes to DB entity models requires a delete of the todos.db. I felt it was fine to get things moving quickly, but a production app would need a real, durable DB such as Postgres.
- For TodosController, we're returning TodoItem, which is also the object we use for DB access to the TodoItem SQLite table. A more robust app would have separation between DAOs and DTOs -- for an MVP, I felt it was fine.
- As mentioned above, Program.cs does a lot of lifting for configuration. There's relatively little in the way of config files, .yaml, the ability to inject different config variables, etc. Especially with a framework I'm less familiar with, I chose what seemed easiest to get the app running, hence letting it do all that work.
- The `RequireUserFilter` and `[FromUser]` attributes might have been overkill, but to me they seemed a clean way to inject auth and guarantee user presence on routes that needed it. I didn't want to pull user ID from claims and load User in every Controller route / service method, so I used the annotation style. It's pretty close to what I'm familiar with in web frameworks I've worked on -- injecting some kind of middleware on certain routes/groups of routes/controllers. I saw a lot of different recommendations on how to do it, but I went with what I could get working.
- `backend/tests/TaskMeisterAPI.Tests/Fixtures/TestWebApplicationFactory.cs` does some wonky things and interacts with the .NET ecosystem in weird ways. It's hacky and I admit it. I didn't have a good sense of how everything gets wired together in .NET apps, so I went with what would give me clean, simple integration tests.
- Speaking of integration tests -- in addition to Service-level tests, I'm generally a proponent of also having integration tests that spin up a mock application and hit it with HTTP calls. You want to make sure everything composes together nicely.
- No input sanitization beyond length validation. Request fields are validated for length and format via data annotations, but there's no HTML-escaping or XSS sanitization at the API layer. Since the frontend renders todo titles via React (which escapes by default), stored XSS isn't a risk in the current UI — but any future consumer of the API (mobile app, third-party client) would need to handle this itself.

## Scalability Concerns

A SQLite DB with one backend server doesn't allow us to scale beyond maybe a few thousand workers. To really scale this for production, we'd need at minimum:
- A load balancer
- Multiple instances of the service
- Durable SQL database (Postgres)
- Autoscaling

Token versioning doesn't scale horizontally. Right now, every authenticated request hits the database to check TokenVersion. With multiple backend instances this is fine, but under high load it becomes a hot read path. The production alternative might be a short-lived token blocklist in, say, Redis — only revoked tokens need a cache entry, so the set stays small and most requests skip the lookup entirely.

SQLite is single-writer by design. SQLite serializes all writes, which means concurrent requests queue behind each other at the DB layer. For a multi-user app with any write volume, this becomes the bottleneck before CPU or network ever does. Migrating to Postgres removes this constraint and opens the door to read replicas for query scaling.

## Not Implemented

* Rate Limiting on the Backend
    * For this MVP, rate limiting felt like overkill. I didn't want to overcomplicate the already-tenuous Program.cs file, given that so much of it was hacked and cobbled together with my limited awareness of the .NET ecosystem. For a V2 of this, I'd implement (or use existing .NET functionality) for a token bucket-style rate limiting paradigm.
* Pagination
    * There's no pagination on Todo item fetch. I could have implemented a simple limit + offset pattern, or a more sophisticated cursor-based pagination strategy, but I felt that the MVP didn't need it.
* More robust DB transactions and error handling
    * We're being fairly naive with the way we do DB writes, and we don't have anything to handle more serious DB issues such as timeouts, disconnects, etc.
* SSL
    * I felt SSL would have been overkill for the MVP. However, it would be a non-negotiable in production for security.
* User Settings / Preferences
    * We'd need this to be truly production-ready, but for an MVP, there's not much in the way of real user prefs we could set.

## What I'd implement next

- Rate Limiting
- TLS
- Postgres DB
- Deploy to a cloud service like GCP
- Kubernetes setup
- Connection to a logging backend (Stackdriver, etc)

