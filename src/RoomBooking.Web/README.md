# Room Booking Web

React and TypeScript client for the Room Booking Assistant.

## Local development

Start the API on `http://localhost:5139`, then run:

```bash
npm install
npm run dev
```

Vite serves the application at `http://localhost:5173` and proxies `/api` and `/health` requests to the local API.

The client supports JWT login, assistant conversation continuity, a responsive chat interface, loading and error states, and basic rendering for the Markdown tables and lists returned by the assistant.

## Configuration

For a deployment where the API is hosted on a different origin, set:

```text
VITE_API_BASE_URL=https://your-api.example.com
```

Do not place API-provider secrets in frontend environment variables. The Groq key is configured only in the backend through `AI__ApiKey`.
