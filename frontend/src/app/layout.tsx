import type { Metadata, Viewport } from 'next';
import { Plus_Jakarta_Sans, DM_Mono, Instrument_Serif } from 'next/font/google';
import './globals.css';
import { AuthProvider } from '@/contexts/auth-context';
import { AiSuggestionsProvider } from '@/contexts/ai-suggestions-context';
import { FeaturesProvider } from '@/contexts/features-context';
import { LocaleWrapper } from '@/components/locale-wrapper';
import { CookieConsent } from '@/components/cookie-consent';
import { Toaster } from 'sonner';
import { NextIntlClientProvider } from 'next-intl';
import { getLocale, getMessages } from 'next-intl/server';

const plusJakarta = Plus_Jakarta_Sans({
  subsets: ['latin'],
  variable: '--font-dash-sans',
  weight: ['400', '500', '600', '700', '800'],
});
const dmMono = DM_Mono({ subsets: ['latin'], weight: ['400', '500'], variable: '--font-dash-mono' });
const instrumentSerif = Instrument_Serif({ subsets: ['latin'], weight: '400', variable: '--font-dash-display' });

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
  themeColor: '#2f8170',
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
      <body className={`${plusJakarta.className} ${plusJakarta.variable} ${dmMono.variable} ${instrumentSerif.variable}`}>
        <NextIntlClientProvider messages={messages}>
          <FeaturesProvider>
          <AuthProvider>
            <LocaleWrapper>
              <AiSuggestionsProvider>
                <div id="root">
                  {children}
                </div>
                <CookieConsent />
                <Toaster
                  position="bottom-right"
                  toastOptions={{
                    duration: 4000,
                    style: {
                      background: 'oklch(99.5% 0.004 65)',
                      color: 'oklch(18% 0.015 65)',
                      border: '1px solid oklch(92% 0.02 65)',
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
