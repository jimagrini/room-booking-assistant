# Technical Definition

## 1. Objective

Build a production-minded conversational assistant whose sole purpose is managing meeting-room reservations. The solution should be intentionally small, easy to review, and safe under invalid input and concurrent booking attempts.

## 2. In scope

- Authenticate `User1` and `User2`.
- Create a booking for the authenticated user.
- List rooms available for a requested time range and attendee count.
- Retrieve available and occupied 30-minute slots for one room over a requested range.
- List bookings owned by the authenticated user.
- Cancel a booking owned by the authenticated user.
- Enforce all constraints in deterministic application code and at the persistence boundary.
- Expose the use cases to the conversational assistant as typed tools.
- Deploy the application to a public cloud environment.
- Provide source code, automated tests, project documentation, a component diagram, and a Jupyter notebook.

## 3. Out of scope for the MVP

- User registration and password recovery.
- Administrator roles.
- Editing or rescheduling an existing booking.
- Email or calendar notifications.
- Recurring reservations.
- Vector databases or retrieval-augmented generation.
- Multiple cooperating agents.

## 4. Business rules

1. Only rooms A, B, C, D, and E can be booked.
2. Every room has a configured positive capacity.
3. Every booking has a non-empty title and a positive attendee count.
4. The attendee count cannot exceed the selected room's capacity.
5. Start and end times must be aligned to 30-minute boundaries.
6. End time must be later than start time.
7. A booking lasts at least 30 minutes and at most three hours.
8. A booking occupies a sequence of contiguous 30-minute slots.
9. Two active bookings cannot occupy the same room slot.
10. A booking belongs to the authenticated user; the user identity is obtained from the authentication context, never from LLM-generated arguments.
11. A user can cancel only an active booking they own.
12. Adjacent bookings are valid: a booking may begin exactly when another booking ends.

## 5. Proposed assistant tools

| Tool | Purpose | Authenticated context |
| --- | --- | --- |
| `create_booking` | Create a validated reservation | Owner is injected by the server |
| `list_available_rooms` | Find rooms free for a range and suitable for the attendee count | User must be authenticated |
| `get_room_schedule` | Return occupied and available slots for a room and range | User must be authenticated |
| `list_my_bookings` | Let the user identify bookings and cancellation IDs | Filters by authenticated user |
| `cancel_booking` | Cancel one of the current user's bookings | Ownership is enforced by the server |

Tool inputs and outputs must use typed schemas. Tool results should return stable error codes in addition to human-readable messages so the model can explain failures without inventing details.

## 6. Data model

The relational database contains rooms, bookings, and protected booking slots. The two fixed challenge users are runtime authentication identities rather than persisted application accounts.

### ChallengeUser (runtime only)

- `Id`
- `Username`
- `PasswordHash`

The shared password is read from runtime configuration and hashed in memory when the application starts.

### Room

- `Id`
- `Name`
- `Capacity`

### Booking

- `Id`
- `RoomId`
- `UserId`
- `Title`
- `AttendeeCount`
- `StartTimeUtc`
- `EndTimeUtc`
- `Status`
- `CreatedAtUtc`
- `CancelledAtUtc`

### BookingSlot

- `Id`
- `BookingId`
- `RoomId`
- `StartTimeUtc`

`BookingSlot` has a unique constraint on `(RoomId, StartTimeUtc)`. A booking creates between one and six slot rows in the same database transaction. This turns the no-double-booking rule into a database invariant and protects it under concurrent requests.

## 7. Security decisions

- Passwords are stored as hashes even though the supplied challenge password is shared.
- Successful login returns a short-lived JWT.
- JWT signing secrets and LLM credentials are read from environment variables and never committed.
- The backend injects the authenticated user into booking operations.
- Tool arguments cannot select or override a user ID.
- All tool inputs are validated again by the application layer.
- The system prompt limits the assistant to room-booking tasks, but authorization never depends on the prompt.

## 8. Time handling

- Persist instants in UTC.
- Parse and present user-facing dates in the configured office time zone.
- Use explicit ISO 8601 values at API and tool boundaries.
- Require clarification when the user omits information that cannot be inferred safely, such as the date or start time.

## 9. Acceptance scenarios

### Successful booking

Given room B has enough capacity and is free from 10:00 to 11:30, when the authenticated user requests that interval with a title and attendee count, then one booking and three protected slot records are created and the assistant confirms the exact details.

### Overlap rejected

Given room B is occupied from 10:00 to 11:30, attempts to book 09:30-10:30, 10:30-11:00, or 11:00-12:00 are rejected. A booking starting exactly at 11:30 is allowed.

### Unauthorized cancellation rejected

Given User1 owns a booking, when User2 asks to cancel it, the booking remains active and the assistant explains that it is unavailable or not owned by User2 without leaking unnecessary details.

### Capacity rejected

Given a room capacity of four, a request for five attendees is rejected and no booking or slot record is created.

### Concurrent conflict rejected

Given two requests attempt to reserve the same room slot concurrently, exactly one succeeds and the other receives a conflict result.

## 10. Definition of done

- Every required use case is available through real LLM tool calling.
- Core rules have unit tests and persistence-level integration tests.
- Authentication and ownership checks are tested.
- The application can be started from a clean checkout using documented commands.
- A deployed URL and health check are available.
- No secrets are present in Git history.
- The README identifies the challenge users, explains how to provide the private password at runtime, and contains setup, architecture, sample prompts, test instructions, and the deployment URL.
- `/doc` contains the project overview and development journey, component diagram, complete question-to-response flow, resolved decision record, and runnable notebook.

