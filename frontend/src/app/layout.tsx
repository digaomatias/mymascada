import type { Metadata, Viewport } from 'next';
import { Manrope, DM_Mono } from 'next/font/google';
import './globals.css';
import { AuthProvider } from '@/contexts/auth-context';
import { AiSuggestionsProvider } from '@/contexts/ai-suggestions-context';
import { FeaturesProvider } from '@/contexts/features-context';
import { LocaleWrapper } from '@/components/locale-wrapper';
import { Toaster } from 'sonner';
import { NextIntlClientProvider } from 'next-intl';
import { getLocale, getMessages } from 'next-intl/server';

const manrope = Manrope({ subsets: ['latin'], variable: '--font-dash-sans' });
const dmMono = DM_Mono({ subsets: ['latin'], weight: ['400', '500'], variable: '--font-dash-mono' });

export const metadata: Metadata = {
  title: 'MyMascada - Personal Finance Manager',
  description: 'AI-powered personal finance management application',
  manifest: '/manifest.json',
  icons: {
    icon: '/favicon.png',
    apple: '/apple-touch-icon.png',
  },
};

export const viewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
  maximumScale: 1,
  userScalable: false,
  themeColor: '#6b46c1',
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
      <body className={`${manrope.className} ${manrope.variable} ${dmMono.variable}`}>
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
