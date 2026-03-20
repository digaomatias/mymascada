/**
 * Shared configuration constants for E2E tests
 */

/** Base host URL for the API (no path). Respects NEXT_PUBLIC_API_URL env var. */
export const API_HOST = process.env.NEXT_PUBLIC_API_URL || 'https://localhost:5126';

/** Full API v1 base URL (includes /api/v1 path). */
export const API_BASE_URL = `${API_HOST}/api/v1`;

// Other shared test configuration can go here
export const DEFAULT_TIMEOUT = 30000;
export const TEST_USER_EMAIL = 'test@example.com';