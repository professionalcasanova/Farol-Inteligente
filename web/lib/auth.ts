export type StoredSession = {
  accessToken: string;
  userId: string;
  name: string;
  email: string;
};

export type AuthNotice = "session-expired";

const AUTH_NOTICE_STORAGE_KEY = "farol.auth.notice";
const SESSION_CHANGED_EVENT = "farol.session.changed";

let currentSession: StoredSession | null = null;

function isStoredSession(value: unknown): value is StoredSession {
  if (!value || typeof value !== "object") {
    return false;
  }

  const candidate = value as Record<string, unknown>;

  return (
    typeof candidate.accessToken === "string" &&
    typeof candidate.userId === "string" &&
    typeof candidate.name === "string" &&
    typeof candidate.email === "string"
  );
}

function notifySessionChanged() {
  if (typeof window === "undefined") {
    return;
  }

  window.dispatchEvent(new Event(SESSION_CHANGED_EVENT));
}

export function addSessionChangeListener(listener: () => void) {
  if (typeof window === "undefined") {
    return () => {};
  }

  window.addEventListener(SESSION_CHANGED_EVENT, listener);

  return () => {
    window.removeEventListener(SESSION_CHANGED_EVENT, listener);
  };
}

export function readStoredSession(): StoredSession | null {
  return currentSession;
}

export function writeStoredSession(session: StoredSession) {
  if (!isStoredSession(session)) {
    currentSession = null;
    notifySessionChanged();
    return;
  }

  currentSession = {
    accessToken: session.accessToken,
    userId: session.userId,
    name: session.name,
    email: session.email,
  };
  notifySessionChanged();
}

export function clearStoredSession() {
  currentSession = null;
  notifySessionChanged();
}

export function writeAuthNotice(notice: AuthNotice) {
  if (typeof window === "undefined") {
    return;
  }

  window.sessionStorage.setItem(AUTH_NOTICE_STORAGE_KEY, notice);
}

export function consumeAuthNotice(): AuthNotice | null {
  if (typeof window === "undefined") {
    return null;
  }

  const notice = window.sessionStorage.getItem(AUTH_NOTICE_STORAGE_KEY);

  if (notice !== "session-expired") {
    window.sessionStorage.removeItem(AUTH_NOTICE_STORAGE_KEY);
    return null;
  }

  window.sessionStorage.removeItem(AUTH_NOTICE_STORAGE_KEY);
  return notice;
}
