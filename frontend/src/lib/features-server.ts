import { defaultFeatures, type FeatureFlags } from '@/lib/api-client';

// Server-only module: imported solely from the root layout (a server component).
// Uses INTERNAL_API_URL, so it must never be pulled into a client bundle.

// Server-side address (Docker-internal) preferred; falls back to the public URL.
// Strip any trailing slash so the URL join can't produce a double slash.
const SERVER_API_URL = (
  process.env.INTERNAL_API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5126'
).replace(/\/+$/, '');

// Short timeout: this runs in the (force-dynamic) root layout, so it gates the
// initial render. Keep it tight so a cold/slow backend can't stall the page.
const FETCH_TIMEOUT_MS = 1000;

/**
 * Fetch feature flags server-side so they can seed FeaturesProvider's initial
 * state. This removes the post-hydration client round-trip that otherwise keeps
 * flag-gated UI (e.g. the Google sign-in button) hidden for a few seconds.
 *
 * Never throws: on timeout, error, or an unexpected payload it returns
 * all-disabled defaults, and the client-side provider revalidates to self-heal
 * once the backend is warm.
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

    const data = await res.json();
    // Guard against non-object payloads (string/array/null) before spreading.
    if (data && typeof data === 'object' && !Array.isArray(data)) {
      return { ...defaultFeatures, ...(data as Partial<FeatureFlags>) };
    }
    return defaultFeatures;
  } catch (error) {
    // Cold backend, network error, or timeout — fall back to defaults.
    console.warn('[getFeaturesServerSide] Failed to fetch feature flags:', error);
    return defaultFeatures;
  } finally {
    clearTimeout(timeout);
  }
}
