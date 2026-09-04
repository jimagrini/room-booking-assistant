# Architecture

## Component diagram

The diagram shows every runtime component involved from the authenticated user's question to the grounded response.

```mermaid
flowchart TD
    User[Authenticated user]
    Web[React web client]
    Api[ASP.NET Core API]
    Assistant[Assistant orchestrator]
    LLM[Groq LLM provider]
    Tools[Typed tool executor]
    Booking[Booking application service]
    Db[(PostgreSQL)]

    User -->|Question| Web
    Web -->|JWT, message, conversationId| Api
    Api --> Assistant
    Assistant -->|Instructions, history, tool schemas| LLM
    LLM -->|Tool call or final answer| Assistant
    Assistant --> Tools
    Tools --> Booking
    Booking -->|Transactional read or write| Db
    Db --> Booking
    Booking --> Tools
    Tools -->|Structured result| Assistant
    Assistant -->|Grounded response| Api
    Api --> Web
    Web -->|Rendered answer| User
```

## Complete question-to-response interaction

```mermaid
sequenceDiagram
    participant U as Authenticated user
    participant W as React client and API
    participant A as Assistant orchestrator
    participant L as Groq LLM
    participant B as Booking service and PostgreSQL

    U->>W: Send natural-language question
    W->>A: JWT, message, and conversationId
    A->>L: Instructions, isolated history, and strict tools
    alt A booking tool is required
        L-->>A: Typed function call
        A->>B: Validated command with authenticated identity
        B-->>A: Structured success or error result
        A->>L: Tool result
    end
    L-->>A: Grounded user-facing answer
    A-->>W: Message, conversationId, and toolsUsed
    W-->>U: Render final response
```

## Conversational request flow

1. The React client sends the user's message, JWT, and optional `conversationId` to the API.
2. The API derives the user identity from the validated token; the LLM cannot supply or override it.
3. The assistant loads only conversation history owned by that user and sends it with system instructions and strict tool definitions to the LLM provider.
4. If the model requests a tool, the orchestrator validates its arguments and invokes the typed tool executor.
5. The booking application service applies capacity, time, ownership, cancellation, and overlap rules. PostgreSQL protects occupied 30-minute slots atomically.
6. The structured result is returned to the model, which produces a response grounded in actual application data.
7. The API returns the answer, stable `conversationId`, and `toolsUsed` metadata; the web client renders it to the user.

The tool loop may repeat when the provider requires more than one function call. The model has no direct database access and cannot bypass application authorization or domain validation.

## Deployment view

Railway runs one application container and one managed PostgreSQL service. The application container serves both the compiled React SPA and ASP.NET Core API over the same public HTTPS origin. It communicates with PostgreSQL through Railway's private network and with Groq over HTTPS. One application replica is used because conversation state is retained in process memory for two hours.
