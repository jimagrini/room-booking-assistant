# Project Overview and Development Journey

## 1. Project overview

The challenge was approached as a small production system rather than as a prompt-only prototype. The result is a conversational room-booking application in which an authenticated user can ask naturally for availability, inspect a room schedule, create a reservation, list owned reservations, and cancel one of them.

The language model is responsible for interpreting intent and selecting a typed tool. It is deliberately not responsible for authorization, validation, or persistence. Those concerns remain in deterministic .NET application and domain services, with PostgreSQL enforcing the final no-overlap invariant. This separation makes assistant behavior convenient without making business correctness depend on model output.

The final solution combines a React web client, an ASP.NET Core API, Groq tool calling, Entity Framework Core, PostgreSQL, JWT authentication, automated tests, a multi-stage Docker image, and a public Railway deployment.

## 2. Implementation approach

The implementation followed four guiding principles:

1. **Model intent, not business truth.** The LLM may request an action, but only typed application services may authorize and execute it.
2. **Protect invariants at more than one boundary.** Requests are validated in the application and domain layers, while overlapping room slots are also prevented by a database uniqueness constraint.
3. **Derive identity from authentication.** The current user comes from the validated JWT and is never accepted from model-generated tool arguments.
4. **Keep evaluation reproducible.** Setup, tests, containerization, deployment, health checks, smoke-test steps, and an executable notebook are committed with the source.

## 3. Step-by-step development process

### Step 1 - Interpret the specification and record assumptions

The functional rules were translated into explicit acceptance scenarios: rooms A-E, room capacity, half-hour alignment, contiguous slots, a maximum three-hour duration, non-overlap, required titles, ownership, and user-specific cancellation. Values omitted from the brief were made explicit: capacities are configured as A=4, B=6, C=8, D=10, and E=12; user-facing time uses `America/Montevideo`; bookings in the past are rejected; and no additional office-hours restriction is imposed.

### Step 2 - Build the domain and application core

The solution was separated into Domain, Application, Infrastructure, API, and Web projects. The domain represents rooms, bookings, slot duration, status, capacity, ownership, and cancellation. Application services validate time ranges and orchestrate the required use cases independently of HTTP and the LLM.

### Step 3 - Add PostgreSQL persistence and concurrency protection

Entity Framework Core migrations create rooms, bookings, and one `BookingSlot` row for every occupied 30-minute interval. A unique database index on `(RoomId, StartTimeUtc)` prevents two concurrent requests from reserving the same room slot. All slots for one booking are written in a single transaction.

### Step 4 - Add authentication and ownership isolation

`User1` and `User2` are created as runtime challenge identities. The shared challenge password is read from configuration, hashed in memory, and never committed. Login returns a short-lived JWT. Booking ownership is obtained from the authenticated request context, so tool calls cannot impersonate another user.

### Step 5 - Expose use cases through REST and typed assistant tools

The API exposes login, room, booking, and assistant endpoints. Five strict tools map the conversational interface to deterministic use cases: `list_available_rooms`, `get_room_schedule`, `list_my_bookings`, `create_booking`, and `cancel_booking`. Schemas reject missing or unexpected arguments before execution.

### Step 6 - Implement the LLM tool loop and conversation context

The assistant sends the system instructions, isolated conversation history, and tool definitions to Groq's OpenAI-compatible Responses API. When the model returns a function call, the server validates and executes it, appends the structured result, and asks the model for a grounded final response. Conversation state is keyed by authenticated user and `conversationId`, retained in server memory for two hours, and cannot be reused by another user.

### Step 7 - Build the responsive web interface

The React and TypeScript client provides login, conversation context, loading and error states, responsive desktop/mobile layouts, formatted assistant content, and sign-out/new-conversation actions. Vite proxies API calls during local development.

### Step 8 - Test, containerize, deploy, and smoke-test

Unit and integration tests cover domain rules, booking use cases, authentication, assistant orchestration, user isolation, persistence, and concurrent conflicts. A multi-stage Dockerfile builds the React client and .NET API into one runtime image. Railway runs that image with managed PostgreSQL, runtime secrets, one application replica, and `/health` monitoring. The production smoke test covered login, availability, conversational creation, active-booking listing, cancellation, and released availability.

## 4. Key decisions

| Decision | Reason | Consequence |
| --- | --- | --- |
| Deterministic services own all rules | LLM output is probabilistic | Model mistakes cannot bypass capacity, time, ownership, or overlap rules |
| One database row per occupied slot | Overlap must also be safe under concurrent requests | PostgreSQL can enforce uniqueness atomically |
| JWT identity is injected server-side | Tool arguments are model-generated and untrusted | Users cannot select another owner |
| UTC storage and Montevideo presentation | Stored instants must remain unambiguous | Natural responses still use the office's local time |
| In-memory conversation history | Keeps the challenge implementation focused | Deployment uses one replica; distributed storage is the scaling path |
| Single web/API image | Simplifies public deployment | Browser and API share one HTTPS origin and avoid production CORS complexity |
| Runtime-only secrets | Credentials must not enter source control | Local and Railway setup require environment variables |

## 5. Main challenges encountered and how they were overcome

| Challenge | Resolution |
| --- | --- |
| The brief required room-specific capacities but omitted their numeric values | Capacities were made configurable and the selected values A=4, B=6, C=8, D=10, E=12 were documented |
| A check-then-insert overlap test alone would be vulnerable to concurrent requests | Bookings are expanded into protected 30-minute slots with a unique `(RoomId, StartTimeUtc)` index and transactional persistence |
| Natural-language tool arguments cannot be trusted for authorization | Strict schemas validate arguments, and the authenticated user is injected by the backend rather than supplied by the LLM |
| Follow-up requests such as "use the same time" require context without leaking conversations between users | The API returns a `conversationId`; the server stores the history with its owner and rejects missing, expired, or cross-user conversations |
| Local time phrases and UTC persistence can produce incorrect booking times | API/tool boundaries use ISO 8601 offsets, storage is UTC, and assistant responses are rendered in `America/Montevideo` |
| The first Railway start failed because the `RoomBooking` connection string was not present | A managed PostgreSQL service and Railway reference variables were configured, while the application continues to fail fast when required configuration is missing |
| Serving React and the API separately would add deployment and CORS configuration | The production API serves the compiled SPA from the same container and listens on Railway's injected port |

## 6. Final result and known trade-offs

The delivered application satisfies the requested booking and assistant actions, is publicly accessible, and keeps business rules testable outside the LLM. It adds `list_my_bookings` to make secure cancellation practical without exposing another user's data.

The main intentional limitation is in-memory conversation history. It is appropriate for this challenge and expires after two hours, but it requires a single application replica. A production scale-out version would move conversation state to Redis or another distributed store. User registration, recurring reservations, rescheduling, calendar integration, and notifications remain outside the requested scope.

## 7. Evaluation references

- [Component architecture and complete question-to-response flow](architecture.md)
- [Technical definition and acceptance scenarios](technical-definition.md)
- [Final assumptions and resolved decisions](assumptions-and-open-questions.md)
- [Railway deployment and production smoke test](deployment.md)
- [Executable technology and evaluation notebook](RoomBookingAssistant.ipynb)
