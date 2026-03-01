import type { Locale } from '@/i18n/config';

export interface Country {
  code: string;
  name: string;
  currency: string;
  locale: Locale;
}

/**
 * List of supported countries with their ISO code, default currency, and locale mapping.
 * Countries that speak Portuguese map to 'pt-BR'; all others default to 'en'.
 */
export const countries: Country[] = [
  { code: 'AR', name: 'Argentina', currency: 'ARS', locale: 'en' },
  { code: 'AU', name: 'Australia', currency: 'AUD', locale: 'en' },
  { code: 'BR', name: 'Brazil', currency: 'BRL', locale: 'pt-BR' },
  { code: 'CA', name: 'Canada', currency: 'CAD', locale: 'en' },
  { code: 'CL', name: 'Chile', currency: 'CLP', locale: 'en' },
  { code: 'CO', name: 'Colombia', currency: 'COP', locale: 'en' },
  { code: 'DE', name: 'Germany', currency: 'EUR', locale: 'en' },
  { code: 'ES', name: 'Spain', currency: 'EUR', locale: 'en' },
  { code: 'FR', name: 'France', currency: 'EUR', locale: 'en' },
  { code: 'GB', name: 'United Kingdom', currency: 'GBP', locale: 'en' },
  { code: 'JP', name: 'Japan', currency: 'JPY', locale: 'en' },
  { code: 'MX', name: 'Mexico', currency: 'MXN', locale: 'en' },
  { code: 'NZ', name: 'New Zealand', currency: 'NZD', locale: 'en' },
  { code: 'PT', name: 'Portugal', currency: 'EUR', locale: 'pt-BR' },
  { code: 'US', name: 'United States', currency: 'USD', locale: 'en' },
].sort((a, b) => a.name.localeCompare(b.name)) as Country[];

/** Valid country codes for server-side validation */
export const validCountryCodes = new Set(countries.map((c) => c.code));

/** O(1) lookup by country code */
const countriesByCode = new Map(countries.map((c) => [c.code, c]));

/** Look up a country by ISO code */
export function getCountryByCode(code: string): Country | undefined {
  return countriesByCode.get(code);
}
