export type StoredSession = {
  accessToken: string;
  userId: string;
  name: string;
  email: string;
};

export type AuthNotice = "session-expired";

const SESSION_STORAGE_KEY = "farol.session";
const AUTH_NOTICE_STORAGE_KEY = "farol.auth.notice";

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

export function readStoredSession(): StoredSession | null {
  if (typeof window === "undefined") {
    return null;
  }

  const rawValue = window.localStorage.getItem(SESSION_STORAGE_KEY);

  if (!rawValue) {
    return null;
  }

  try {
    const parsedValue = JSON.parse(rawValue) as unknown;

    if (!isStoredSession(parsedValue)) {
      window.localStorage.removeItem(SESSION_STORAGE_KEY);
      return null;
    }

    return parsedValue;
  } catch {
    window.localStorage.removeItem(SESSION_STORAGE_KEY);
    return null;
  }
}

export function writeStoredSession(session: StoredSession) {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.setItem(
    SESSION_STORAGE_KEY,
    JSON.stringify({
      accessToken: session.accessToken,
      userId: session.userId,
      name: session.name,
      email: session.email,
    } satisfies StoredSession),
  );
}

export function clearStoredSession() {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.removeItem(SESSION_STORAGE_KEY);
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
