import type { FeatureFlags } from '@/lib/api-client';

// Server-only module: imported solely from the root layout (a server component).
// Uses INTERNAL_API_URL, so it must never be pulled into a client bundle.

export const defaultFeatures: FeatureFlags = {
  aiCategorization: false,
  googleOAuth: false,
  bankSync: false,
  emailNotifications: false,
  accountSharing: false,
  stripeBilling: false,
};

// Server-side address (Docker-internal) preferred; falls back to the public URL.
const SERVER_API_URL =
  process.env.INTERNAL_API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5126';

// Short timeout so a cold/slow backend can never block the page render.
const FETCH_TIMEOUT_MS = 2000;

/**
 * Fetch feature flags server-side so they can seed FeaturesProvider's initial
 * state. This removes the post-hydration client round-trip that otherwise keeps
 * flag-gated UI (e.g. the Google sign-in button) hidden for a few seconds.
 *
 * Never throws: on timeout or error it returns all-disabled defaults, and the
 * client-side provider revalidates to self-heal once the backend is warm.
 */
export async function getFeaturesServerSide(): Promise<FeatureFlags> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), FETCH_TIMEOUT_MS);

  try {
    const res = await fetch(`${SERVER_API_URL}/api/latest/Features`, {
      signal: controller.signal,
      cache: 'no-store',
      headers: { Accept: 'application/json' },
    });

    if (!res.ok) {
      return defaultFeatures;
    }

    const data = (await res.json()) as Partial<FeatureFlags>;
    return { ...defaultFeatures, ...data };
  } catch {
    // Cold backend, network error, or timeout — fall back to defaults.
    return defaultFeatures;
  } finally {
    clearTimeout(timeout);
  }
}
