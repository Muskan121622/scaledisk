// Minimal SignalR wiring. The API's hub is at /hubs/notifications and accepts the JWT via
// query string (Program.cs#L104-106) — SignalR's accessTokenFactory handles that automatically.
// We expose a singleton connection so every screen subscribes to the same stream, plus a
// small helper to register/unregister handlers without prop drilling.

import * as signalR from '@microsoft/signalr';
import { getTokens } from './api';

let connection: signalR.HubConnection | null = null;
let started: Promise<void> | null = null;

export function getHub(): signalR.HubConnection {
  if (connection) return connection;
  connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/notifications', {
      accessTokenFactory: () => getTokens()?.accessToken ?? '',
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Warning)
    .build();
  return connection;
}

export async function ensureConnected(): Promise<void> {
  const hub = getHub();
  if (hub.state === signalR.HubConnectionState.Connected) return;
  if (started) return started;
  started = hub.start().catch(() => { started = null; });
  return started;
}

export function stopHub() {
  connection?.stop();
  connection = null;
  started = null;
}

/** Subscribe to a hub event; returns an unsubscribe fn. Silently waits for the connection. */
export function on(event: string, handler: (...args: unknown[]) => void): () => void {
  const hub = getHub();
  hub.on(event, handler);
  ensureConnected().catch(() => { /* surfaced via hub.onreconnecting/onclose */ });
  return () => hub.off(event, handler);
}
