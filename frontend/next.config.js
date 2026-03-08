const withPWA = require('next-pwa')({
  dest: 'public',
  register: true,
  skipWaiting: true,
  disable: process.env.NODE_ENV === 'development',
  runtimeCaching: [
    {
      // Never cache API responses — they contain sensitive financial data
      urlPattern: /\/api\//,
      handler: 'NetworkOnly',
    },
    {
      // Cache static assets (images, fonts, CSS, JS)
      urlPattern: /\/_next\/static\/.*/,
      handler: 'CacheFirst',
      options: {
        cacheName: 'static-assets',
        expiration: {
          maxEntries: 200,
          maxAgeSeconds: 30 * 24 * 60 * 60, // 30 days
        },
      },
    },
    {
      // Cache page navigations with network-first strategy
      urlPattern: /^https?:\/\/[^/]+\/?(?!api\/).*/,
      handler: 'NetworkFirst',
      options: {
        cacheName: 'pages',
        expiration: {
          maxEntries: 50,
        },
      },
    },
  ],
});

const createNextIntlPlugin = require('next-intl/plugin');
const withNextIntl = createNextIntlPlugin('./src/i18n/request.ts');

const { withSentryConfig } = require('@sentry/nextjs');

/** @type {import('next').NextConfig} */
const nextConfig = {
  env: {
    NEXT_PUBLIC_APP_VERSION: require('./package.json').version,
  },
  turbopack: {
    rules: {
      '*.svg': {
        loaders: ['@svgr/webpack'],
        as: '*.js',
      },
    },
  },
  // Enable standalone output for Docker
  output: 'standalone',
  // API configuration for backend integration
  // INTERNAL_API_URL is the Docker-internal address used for server-side rewrites.
  // NEXT_PUBLIC_API_URL is the public URL used by the browser (baked at build time).
  async rewrites() {
    const rewriteTarget = process.env.INTERNAL_API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5126';
    return [
      {
        source: '/api/:path*',
        destination: `${rewriteTarget}/api/v1/:path*`,
      },
    ];
  },
  // External packages for server components
  serverExternalPackages: [],
};

const combinedConfig = withPWA(withNextIntl(nextConfig));

// Only apply Sentry config when a DSN is present or SENTRY_AUTH_TOKEN is set.
// This ensures the build works fine in environments without Sentry configured.
const hasSentry = !!(process.env.NEXT_PUBLIC_SENTRY_DSN || process.env.SENTRY_DSN || process.env.SENTRY_AUTH_TOKEN);

module.exports = hasSentry
  ? withSentryConfig(combinedConfig, {
      // Sentry organisation / project slugs (read from env; empty = no upload)
      org: process.env.SENTRY_ORG,
      project: process.env.SENTRY_PROJECT,

      // Source map upload — only when SENTRY_AUTH_TOKEN is set
      authToken: process.env.SENTRY_AUTH_TOKEN,

      // Only upload source maps in CI/production
      silent: true,

      // Automatically tree-shake Sentry logger statements to reduce bundle size
      disableLogger: true,

      // Hides source maps from the browser bundle (they're uploaded to Sentry)
      hideSourceMaps: true,

      // Tunnels Sentry requests through Next.js to avoid ad-blockers
      tunnelRoute: '/monitoring-tunnel',

      // Disable automatic source map upload when no auth token is present
      sourcemaps: {
        disable: !process.env.SENTRY_AUTH_TOKEN,
      },

      // Disable automatic instrumentation injection when no DSN (pure auth-token-only scenario)
      autoInstrumentServerFunctions: true,
      autoInstrumentMiddleware: true,
    })
  : combinedConfig;
