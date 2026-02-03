import { getRequestConfig } from 'next-intl/server';
import { cookies } from 'next/headers';
import { defaultLocale, normalizeLocale, type Locale } from './config';

// Import messages statically to avoid dynamic import issues during static generation
import enMessages from '../../messages/en.json';
import ptBRMessages from '../../messages/pt-BR.json';

const LOCALE_COOKIE_NAME = 'NEXT_LOCALE';

// Static message map
const messages: Record<Locale, typeof enMessages> = {
  'en': enMessages,
  'pt-BR': ptBRMessages,
};

// Get the user's locale preference from cookie
// This is called for each request to determine the locale
async function getUserLocale(): Promise<Locale> {
  try {
    const cookieStore = await cookies();
    const localeCookie = cookieStore.get(LOCALE_COOKIE_NAME);

    if (localeCookie?.value) {
      return normalizeLocale(localeCookie.value);
    }
  } catch {
    // cookies() might throw in certain contexts (static generation), fall back to default
  }

  return defaultLocale;
}

export default getRequestConfig(async () => {
  const locale = await getUserLocale();

  return {
    locale,
    messages: messages[locale],
  };
});
