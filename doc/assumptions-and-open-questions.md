# Assumptions and Open Questions

The challenge specification intentionally or accidentally leaves some domain values undefined. These items will be confirmed with Promtior when possible and otherwise documented as explicit configuration.

| Topic | Specification status | Proposed fallback |
| --- | --- | --- |
| Room capacities | Required but numeric values are missing | Seed configurable values and document them |
| Office hours | Not specified | Use a configurable local opening window |
| Office time zone | Office is in Cubo Itau; time zone is not explicit | `America/Montevideo` |
| Past bookings | Not specified | Reject creating bookings in the past |
| Cancellation persistence | Not specified | Soft cancellation using a status and audit timestamp |
| Schedule query size | Not specified | Apply a bounded maximum range |
| Conversation persistence | Not specified | Keep only the active browser session for the MVP |

## Clarification to request

> The document states that rooms A-E have room-specific capacities, but the numeric capacity for each room is not included. Could you confirm those values? Also, should bookings be restricted to specific office hours?

