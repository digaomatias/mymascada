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

/** @type {import('next').NextConfig} */
const nextConfig = {
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
  async rewrites() {
    return [
      {
        source: '/api/:path*',
        destination: process.env.NEXT_PUBLIC_API_URL ? `${process.env.NEXT_PUBLIC_API_URL}/api/:path*` : 'http://localhost:5126/api/:path*',
      },
    ];
  },
  // External packages for server components
  serverExternalPackages: [],
};

module.exports = withPWA(withNextIntl(nextConfig));
