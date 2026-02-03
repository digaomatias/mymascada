// Supported locales for the application
export const locales = ['en', 'pt-BR'] as const;
export type Locale = (typeof locales)[number];

// Default locale when no preference is set
export const defaultLocale: Locale = 'en';

// Locale display names for the language selector
export const localeNames: Record<Locale, string> = {
  'en': 'English',
  'pt-BR': 'Portugues (Brasil)',
};

// Check if a string is a valid locale
export function isValidLocale(locale: string): locale is Locale {
  return locales.includes(locale as Locale);
}

// Normalize a locale string to our supported format
export function normalizeLocale(locale: string | undefined | null): Locale {
  if (!locale) return defaultLocale;

  // Check for exact match
  if (isValidLocale(locale)) return locale;

  // Handle common variants
  const lowerLocale = locale.toLowerCase();

  // English variants (en-US, en-GB, etc.) -> en
  if (lowerLocale.startsWith('en')) return 'en';

  // Portuguese variants (pt, pt-PT, pt-BR) -> pt-BR
  if (lowerLocale.startsWith('pt')) return 'pt-BR';

  return defaultLocale;
}
