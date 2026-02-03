'use client';

import { defaultLocale, normalizeLocale, type Locale } from './config';

const LOCALE_STORAGE_KEY = 'mymascada-locale';
const LOCALE_COOKIE_NAME = 'NEXT_LOCALE';

// Get locale from localStorage (client-side only)
export function getStoredLocale(): Locale {
  if (typeof window === 'undefined') return defaultLocale;

  try {
    // Check cookie first (for consistency with server)
    const cookieLocale = getCookieLocale();
    if (cookieLocale) return cookieLocale;

    const stored = localStorage.getItem(LOCALE_STORAGE_KEY);
    return normalizeLocale(stored);
  } catch {
    return defaultLocale;
  }
}

// Get locale from cookie
function getCookieLocale(): Locale | null {
  if (typeof document === 'undefined') return null;

  const cookies = document.cookie.split(';');
  for (const cookie of cookies) {
    const [name, value] = cookie.trim().split('=');
    if (name === LOCALE_COOKIE_NAME) {
      const locale = normalizeLocale(decodeURIComponent(value));
      return locale;
    }
  }
  return null;
}

// Store locale preference in localStorage and cookie
export function setStoredLocale(locale: Locale): void {
  if (typeof window === 'undefined') return;

  try {
    localStorage.setItem(LOCALE_STORAGE_KEY, locale);

    // Also set cookie for server-side access (expires in 1 year)
    const expires = new Date();
    expires.setFullYear(expires.getFullYear() + 1);
    document.cookie = `${LOCALE_COOKIE_NAME}=${encodeURIComponent(locale)};expires=${expires.toUTCString()};path=/;SameSite=Lax`;
  } catch {
    // Ignore storage errors
  }
}

// Get browser's preferred locale
export function getBrowserLocale(): Locale {
  if (typeof window === 'undefined') return defaultLocale;

  const browserLang = navigator.language || (navigator as { userLanguage?: string }).userLanguage;
  return normalizeLocale(browserLang);
}

// Determine the best locale for the current user
// Priority: 1. User's stored preference, 2. User profile (from API), 3. Browser, 4. Default
export function determineLocale(userProfileLocale?: string | null): Locale {
  // First check stored preference
  const storedLocale = getStoredLocale();
  if (storedLocale !== defaultLocale) {
    return storedLocale;
  }

  // Then check user profile from API
  if (userProfileLocale) {
    return normalizeLocale(userProfileLocale);
  }

  // Then check browser preference
  const browserLocale = getBrowserLocale();
  if (browserLocale !== defaultLocale) {
    return browserLocale;
  }

  return defaultLocale;
}
