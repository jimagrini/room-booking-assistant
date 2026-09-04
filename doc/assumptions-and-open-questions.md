# Assumptions and Resolved Decisions

The brief leaves several operational values unspecified. The following table records the final decisions implemented by the submitted solution; these are no longer pending questions.

| Topic | Specification status | Final implemented decision |
| --- | --- | --- |
| Room capacities | Rooms must have individual limits, but numeric values are not provided | Capacities are configurable and seeded as A=4, B=6, C=8, D=10, E=12 |
| Office hours | Not specified | No additional office-hours restriction is imposed; the required half-hour alignment, future start, and three-hour maximum still apply |
| Office time zone | The office is in Cubo Itau, but the time zone is not explicit | User-facing dates use `America/Montevideo`; persisted instants use UTC |
| Past bookings | Not specified | Creating a booking whose start is in the past is rejected |
| Cancellation persistence | Not specified | Cancellation is soft: status and cancellation timestamp are retained while protected slot rows are released |
| Schedule query size | Not specified | A schedule request is bounded to a maximum range of seven days |
| Conversation persistence | Not specified | Conversation history is stored in application memory for two hours, keyed by `conversationId` and authenticated user |
| Horizontal scaling | Not specified | The submitted deployment uses one application replica because conversation memory is process-local |
| Challenge users | User1 and User2 share the supplied password | The password is read at runtime, hashed in memory, and never committed; identity is returned through a short-lived JWT |
| LLM provider | Stack is open; Groq is suggested for cloud use | Groq's OpenAI-compatible Responses API is used with `openai/gpt-oss-20b`, configurable through environment settings |

## Rationale for the capacity values

The exact capacities are not material to the architecture, but concrete limits are necessary to evaluate availability and validation. The increasing values 4, 6, 8, 10, and 12 make capacity filtering easy to demonstrate while keeping them externalized in `RoomSeeds` configuration.

## Remaining production considerations

No unresolved assumption blocks challenge evaluation. If the system were promoted beyond the challenge scope, the following policies would require product decisions:

- Define official office opening hours and holiday behavior.
- Move conversation history to a distributed store before adding application replicas.
- Define retention and audit requirements for cancelled bookings.
- Decide whether registration, roles, recurring meetings, rescheduling, and calendar notifications are required.

These items are intentionally outside the submitted MVP and do not change the challenge's requested behavior.
