// Next.js Instrumentation Hook
// https://nextjs.org/docs/app/building-your-application/optimizing/instrumentation

import { captureRequestError } from '@sentry/nextjs';

export async function register() {
  if (process.env.NEXT_RUNTIME === 'nodejs') {
    await import('./sentry.server.config');
  }

  if (process.env.NEXT_RUNTIME === 'edge') {
    await import('./sentry.edge.config');
  }
}

// Capture unhandled errors in Next.js request handler (App Router, Next.js 15+)
// The hook signature matches what Next.js passes: err, request (with headers), context.
// We delegate directly to Sentry when a DSN is configured.
export const onRequestError: (
  err: unknown,
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  request: any,
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  context: any
) => void | Promise<void> = (err, request, context) => {
  captureRequestError(err, request, context);
};
