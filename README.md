# Room Booking Assistant

A conversational meeting-room booking system built for the Promtior technical challenge.

The application allows authenticated users to discover available rooms, create and cancel reservations, and inspect room schedules through an AI assistant that uses explicit tools. All booking and authorization rules are enforced by deterministic application services; the language model only interprets user intent and presents results.

## Live application

- Web application: [room-booking-assistant-production.up.railway.app](https://room-booking-assistant-production.up.railway.app/)
- Health check: [`/health`](https://room-booking-assistant-production.up.railway.app/health)
- Evaluation notebook: [`doc/RoomBookingAssistant.ipynb`](doc/RoomBookingAssistant.ipynb)

### Challenge users

| Username | Password |
| --- | --- |
| `User1` | Provided in the private challenge brief |
| `User2` | Provided in the private challenge brief |

## Project status

The complete booking workflow, PostgreSQL persistence, JWT authentication, REST API, conversational assistant, React web client, Railway deployment, and evaluation notebook are implemented and validated. The repository is ready for final delivery.

## Features and business rules

- Rooms A through E have configurable capacities.
- Reservations use contiguous 30-minute slots and last at most three hours.
- A room cannot be double-booked.
- Authenticated users can see and cancel only their own bookings.
- The assistant can create bookings, list available rooms, retrieve room schedules, list the current user's bookings, and cancel bookings owned by that user.
- Dates and times shown to users use the `America/Montevideo` time zone.

## Architecture

- **Web:** React 19, TypeScript, and Vite.
- **API:** ASP.NET Core on .NET 10 with JWT authentication.
- **Application and domain:** deterministic use cases and booking invariants.
- **Persistence:** Entity Framework Core with PostgreSQL and database-enforced conflict protection.
- **AI orchestration:** Groq's OpenAI-compatible Responses API with explicit tool calling.
- **Deployment:** a multi-stage Docker image on Railway, with managed PostgreSQL.

The production API serves the compiled React application, so browser and API requests share one HTTPS origin. Assistant conversation history is isolated by authenticated user and retained in memory for two hours; production therefore uses one application replica.

## AI provider

The assistant uses Groq with `openai/gpt-oss-20b` as the default model. Groq's Responses API is stateless, so the application supplies the isolated conversation history on each request.

Provide the Groq secret at runtime through the `AI__ApiKey` environment variable. Never commit API keys or put them in `appsettings.json`.

## Local development

### Prerequisites

- .NET SDK 10
- Node.js 24
- Docker Desktop

### 1. Start PostgreSQL

Copy `.env.example` to `.env`, replace its local database password, and run:

```powershell
Copy-Item .env.example .env
docker compose up -d
```

### 2. Start the API

In PowerShell, configure the runtime secrets for the current terminal:

```powershell
$env:ConnectionStrings__RoomBooking = "Host=localhost;Port=5432;Database=room_booking;Username=room_booking;Password=<local-database-password>"
$env:Jwt__SigningKey = "<new-random-secret-containing-at-least-32-characters>"
$env:ChallengeUsers__Password = "<password-from-the-challenge-brief>"
$env:AI__ApiKey = "<groq-api-key>"
dotnet run --project src/RoomBooking.Api
```

Entity Framework Core applies the committed migrations and seeds rooms A-E when the API starts. The default local API address is `http://localhost:5139`.

### 3. Start the web client

In a second terminal:

```powershell
Set-Location src/RoomBooking.Web
npm ci
npm run dev
```

Open `http://localhost:5173`. Vite proxies local API requests to the backend.

## Validation

Run from the repository root:

```powershell
dotnet build --no-restore
dotnet test --no-build
Push-Location src/RoomBooking.Web
npm run build
npm run lint
Pop-Location
docker build -t room-booking-assistant .
```

Last validated on 2026-09-03:

- 34 automated tests passed; 0 failed and 0 skipped.
- The React production build and ESLint completed successfully.
- The production Docker image built successfully.
- The Railway smoke test covered health, login, availability, conversational booking, active-booking history, cancellation, and released availability.
- The evaluation notebook was validated as nbformat 4.5; all executable Python cells pass syntax validation and contain no saved credentials or outputs.

## Example prompts

- `¿Qué salas están disponibles el 4 de septiembre de 2026 de 13:00 a 14:00 para 5 personas?`
- `Reservá la sala B para ese mismo horario con el título Reunión de proyecto. Seremos 5 personas.`
- `Mostrame mis reservas activas.`
- `¿Cuál es la agenda de la sala B para mañana?`
- `Cancelá la reserva Reunión de proyecto.`

## Deployment

The root `Dockerfile` builds the React client and ASP.NET Core API into one production container. The API serves the compiled SPA and connects privately to a Railway PostgreSQL service.

See [Railway deployment](doc/deployment.md) for the variables, health check, networking, and smoke-test procedure.

## Documentation

The required project overview, development journey, end-to-end component flow, decisions, deployment steps, and executable examples are available under [`doc/`](doc/):

- [Project overview and development journey](doc/project-overview.md)
- [Architecture and complete conversational flow](doc/architecture.md)
- [Technical definition](doc/technical-definition.md)
- [Assumptions and open questions](doc/assumptions-and-open-questions.md)
- [Railway deployment](doc/deployment.md)
- [Technology and evaluation notebook](doc/RoomBookingAssistant.ipynb)

## Design principle

> The LLM interprets intent; the application owns business rules, authorization, and data integrity.
