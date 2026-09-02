# Room Booking Assistant

A conversational meeting-room booking system built for the Promtior technical challenge.

The application allows authenticated users to create and cancel reservations, discover available rooms, and inspect room schedules through an AI assistant that uses explicit tools. All booking and authorization rules are enforced by deterministic application services; the language model only interprets user intent and presents results.

## Project status

The project is currently in the architecture and domain-design phase.

## Core requirements

- Rooms A through E, each with a configurable maximum capacity.
- Reservations use contiguous 30-minute slots and last at most three hours.
- A room cannot be double-booked.
- Users `User1` and `User2` can authenticate with the challenge credentials.
- The assistant can create bookings, list available rooms, retrieve room schedules, list the current user's bookings, and cancel bookings owned by that user.
- The solution must be deployed and documented, including a Jupyter notebook.

## Design principle

> The LLM interprets intent; the application owns business rules, authorization, and data integrity.

Detailed scope and decisions are available under [`doc/`](doc/).
