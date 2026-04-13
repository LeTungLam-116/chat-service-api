# Minimal Chat App

A decoupled real-time messaging application demonstrating end-to-end communication via WebSockets. Built with a monolithic .NET 8 Web API backend and a lightweight React SPA.

## Tech Stack

### Backend
- **Framework:** .NET 8 Web API (C#)
- **Real-Time:** ASP.NET Core SignalR
- **Database:** MS SQL Server + Entity Framework Core 8
- **Authentication:** JWT Bearer & Google Authentication

### Frontend
- **Framework:** React.js (Vite)
- **HTTP Client:** Axios
- **State Management:** React Hooks & Local Storage

## Key Features

- **Real-Time Communication:** Persistent WebSocket connections via SignalR for instant message delivery, typing indicators, read receipts, and message revocation.
- **Group Chat Architecture:** Managed group ecosystem allowing users to create persistent rooms, request entry, and approve incoming members securely.
- **Friendship System:** Robust friend request logic utilizing EF Core relational entities.
- **OAuth 2.0 Integration:** Secure login flows utilizing Google OAuth combined with custom JWT token handling.
- **Performance Optimized:** Chat history queries are optimized with composite indices to ensure minimal latency during heavy message retrieval.

## Getting Started

### Prerequisites
- .NET 8 SDK
- SQL Server 
- Node.js

### Backend Setup
1. Clone the repository and navigate to the `ChatService.Api` directory.
2. Update the `DefaultConnection` string in `appsettings.json` to point to your SQL Server instance.
3. Apply Entity Framework migrations to build the database schema:
   ```bash
   dotnet ef database update
   ```
4. Run the API:
   ```bash
   dotnet run
   ```

### Frontend Setup
1. Navigate to the `chat-web-client` directory.
2. Install standard node dependencies:
   ```bash
   npm install
   ```
3. Start the Vite development server:
   ```bash
   npm run dev
   ```
4. Access the application at `http://localhost:5173`.
