import type { Metadata, Viewport } from 'next';
import { Inter } from 'next/font/google';
import './globals.css';
import { AuthProvider } from '@/contexts/auth-context';
import { AiSuggestionsProvider } from '@/contexts/ai-suggestions-context';
import { FeaturesProvider } from '@/contexts/features-context';
import { LocaleWrapper } from '@/components/locale-wrapper';
import { Toaster } from 'sonner';
import { NextIntlClientProvider } from 'next-intl';
import { getLocale, getMessages } from 'next-intl/server';

const inter = Inter({ subsets: ['latin'] });

export const metadata: Metadata = {
  title: 'MyMascada - Personal Finance Manager',
  description: 'AI-powered personal finance management application',
  manifest: '/manifest.json',
  icons: {
    icon: '/icon.svg',
    apple: '/icon-192x192.png',
  },
};

export const viewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
  maximumScale: 1,
  userScalable: false,
  themeColor: '#3b82f6',
};

// Force dynamic rendering since locale detection requires cookies at request time
export const dynamic = 'force-dynamic';

export default async function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const locale = await getLocale();
  const messages = await getMessages();

  return (
    <html lang={locale}>
      <body className={inter.className}>
        <NextIntlClientProvider messages={messages}>
          <FeaturesProvider>
          <AuthProvider>
            <LocaleWrapper>
              <AiSuggestionsProvider>
                <div id="root">
                  {children}
                </div>
                <Toaster
                  position="bottom-right"
                  toastOptions={{
                    duration: 4000,
                    style: {
                      background: '#ffffff',
                      color: '#1f2937',
                      border: '1px solid #e5e7eb',
                    },
                  }}
                />
              </AiSuggestionsProvider>
            </LocaleWrapper>
          </AuthProvider>
          </FeaturesProvider>
        </NextIntlClientProvider>
      </body>
    </html>
  );
}
