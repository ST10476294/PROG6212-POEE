# Event Management System

## Overview

This project is a relational database design for an event management system.
The system allows organisers to create and manage events, routes and event
categories, while participants can enrol in categories and receive results
after completing an event.

The database is designed for Microsoft SQL Server and follows a relational
structure with primary keys, foreign keys, unique constraints and appropriate
data types.

## System Roles

### 1. Organiser

Organisers are responsible for creating and managing events.

An organiser can:

- Create events
- Update event information
- Manage event routes
- Create event categories
- Define category entry fees
- Set maximum participant limits
- Manage event-related information

The `Users` table stores the user's role, while the `Events` table connects
an event to its organiser through `OrganiserId`.

### 2. Participant

Participants take part in events by enrolling in an available category.

A participant can:

- View available events
- View event routes
- View event categories
- Enrol in an event category
- View their enrolment status
- View their event result

The `Enrolments` table connects participants to event categories. Results are
stored in the `Results` table and are linked to an individual enrolment.

---

## Database Design

The database consists of the following main entities:

### Users

Stores system users.

Important attributes include:

- `UserId` - Primary key
- `FullName`
- `Email` - Unique
- `PasswordHash`
- `Role`
- `CreatedAt`

A user can organise multiple events and can also participate in multiple
event categories depending on their role and application rules.

### Events

Stores information about individual events.

Important attributes include:

- `EventId` - Primary key
- `OrganiserId` - Foreign key to `Users`
- `Name`
- `Discipline`
- `EventDate`
- `Location`
- `Description`

Each organiser can organise multiple events.

### Routes

Stores route information associated with an event.

Important attributes include:

- `RouteId` - Primary key
- `EventId` - Foreign key to `Events`
- `Description`
- `ElevationGainM`
- `MapUrl`

An event can have multiple routes.

### Categories

Stores the categories available within an event.

Important attributes include:

- `CategoryId` - Primary key
- `EventId` - Foreign key to `Events`
- `Name`
- `DistanceKm`
- `MaxParticipants`
- `EntryFee`

An event can contain multiple categories.

### Enrolments

Stores participant entries into event categories.

Important attributes include:

- `EnrolmentId` - Primary key
- `ParticipantId` - Foreign key to `Users`
- `CategoryId` - Foreign key to `Categories`
- `EnrolmentDate`
- `Status`

A participant can have multiple enrolments, while each enrolment belongs to
one category.

### Results

Stores the result associated with an enrolment.

Important attributes include:

- `ResultId` - Primary key
- `EnrolmentId` - Unique foreign key to `Enrolments`
- `FinishTime`
- `OverallPosition`
- `CategoryPosition`
- `Status`

The unique constraint on `EnrolmentId` ensures that an enrolment has at most
one result.

---

## Entity Relationships

The database uses the following main relationships:

```text
Users
  │
  ├── 1:N ── Events
  │
  └── 1:N ── Enrolments
                 │
                 │ N:1
                 ▼
             Categories
                 ▲
                 │ N:1
                 │
               Events
                 │
                 └── 1:N ── Routes

Enrolments
     │
     └── 1:1 ── Results
