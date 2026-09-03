# Railway deployment

Railway is an explicitly permitted deployment option in the challenge. This setup uses one application service for the React client and ASP.NET Core API, plus one managed PostgreSQL service.

## Runtime architecture

- The root `Dockerfile` builds the React application with Node.js.
- It publishes the ASP.NET Core API with .NET 10.
- The final image contains the API and the compiled frontend under `wwwroot`.
- ASP.NET Core listens on Railway's injected `PORT`.
- Entity Framework Core applies the committed migrations when the service starts.
- Railway terminates public HTTPS at the platform edge.

Keep the application service at one replica. Assistant conversation state is intentionally stored in memory for this challenge and is not shared between replicas.

## 1. Create the Railway project

1. Sign in to Railway and create a new project.
2. Choose **Deploy from GitHub repo** and select `jimagrini/room-booking-assistant`.
3. Use `main` as the production branch after the deployment pull request is merged.
4. Railway detects the root `Dockerfile`; no custom build or start command is required.
5. Add **Database → PostgreSQL** to the same project.

## 2. Configure application variables

Add these variables to the application service, not the PostgreSQL service:

| Variable | Value |
| --- | --- |
| `ConnectionStrings__RoomBooking` | `Host=${{Postgres.PGHOST}};Port=${{Postgres.PGPORT}};Database=${{Postgres.PGDATABASE}};Username=${{Postgres.PGUSER}};Password=${{Postgres.PGPASSWORD}};SSL Mode=Require;Trust Server Certificate=true` |
| `Jwt__SigningKey` | A new random secret of at least 32 characters |
| `ChallengeUsers__Password` | The password evaluators will use for `User1` and `User2` |
| `AI__ApiKey` | A valid Groq API key |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

The application already defaults to:

- `AI__BaseUrl=https://api.groq.com/openai/v1/`
- `AI__Model=openai/gpt-oss-20b`
- `AI__OfficeTimeZoneId=America/Montevideo`

Override them only if the provider or model changes. Do not add secrets to Git, build arguments, logs, screenshots, or the public README.

The Railway reference syntax assumes the PostgreSQL service is named `Postgres`. If it has another name, select each PostgreSQL variable through **Add Reference Variable** so Railway inserts the correct service name.

## 3. Configure health and networking

1. In the application service settings, set the health-check path to `/health`.
2. Set the health-check timeout to 300 seconds so the first migration has time to complete.
3. Leave the replica count at one.
4. Under **Networking**, generate a public domain.
5. Open `https://<generated-domain>/health` and confirm the response is `{"status":"healthy"}`.
6. Open the domain root and confirm the login screen loads.

No `Cors__AllowedOrigin` value is needed in production because the browser and API use the same origin.

## 4. Smoke test the deployed application

Run this flow from the public domain:

1. Sign in as `User1`.
2. Ask for rooms available at a future date and time for five people.
3. Reserve one returned room in the same conversation.
4. List active reservations and confirm the new booking appears.
5. Cancel that booking.
6. Ask for availability again and confirm the room was released.
7. Sign out, sign in as `User2`, and confirm User1's booking history is not exposed.

Also refresh a non-root client route if routes are added later; the SPA fallback should return `index.html`.

## 5. Local container check

With local PostgreSQL running and secrets set in the shell:

```powershell
docker build -t room-booking-assistant .
docker run --rm -p 8080:8080 `
  -e ConnectionStrings__RoomBooking="Host=host.docker.internal;Port=5432;Database=room_booking;Username=room_booking;Password=<local-password>" `
  -e Jwt__SigningKey="<new-random-secret-at-least-32-characters>" `
  -e ChallengeUsers__Password="<challenge-password>" `
  -e AI__ApiKey="<groq-api-key>" `
  room-booking-assistant
```

Then open `http://localhost:8080/health` and `http://localhost:8080`.
