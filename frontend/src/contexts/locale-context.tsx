'use client';

import React, { createContext, useContext, useState, useEffect, useCallback, type ReactNode } from 'react';
import { type Locale, defaultLocale, locales, localeNames } from '@/i18n/config';
import { setStoredLocale, determineLocale } from '@/i18n/client';

interface LocaleContextType {
  locale: Locale;
  setLocale: (locale: Locale) => void;
  isLoading: boolean;
  locales: readonly Locale[];
  localeNames: Record<Locale, string>;
}

const LocaleContext = createContext<LocaleContextType | undefined>(undefined);

interface LocaleProviderProps {
  children: ReactNode;
  userLocale?: string | null; // Locale from user profile (API)
}

export function LocaleProvider({ children, userLocale }: LocaleProviderProps) {
  const [locale, setLocaleState] = useState<Locale>(defaultLocale);
  const [isLoading, setIsLoading] = useState(true);

  // Initialize locale on mount
  useEffect(() => {
    const initialLocale = determineLocale(userLocale);
    setLocaleState(initialLocale);
    setIsLoading(false);
  }, [userLocale]);

  // Update locale when user profile changes
  useEffect(() => {
    if (userLocale) {
      const normalizedLocale = determineLocale(userLocale);
      if (normalizedLocale !== locale) {
        setLocaleState(normalizedLocale);
        setStoredLocale(normalizedLocale);
      }
    }
  }, [userLocale, locale]);

  const setLocale = useCallback((newLocale: Locale) => {
    setLocaleState(newLocale);
    setStoredLocale(newLocale);
    // Reload the page to apply new locale
    window.location.reload();
  }, []);

  return (
    <LocaleContext.Provider
      value={{
        locale,
        setLocale,
        isLoading,
        locales,
        localeNames,
      }}
    >
      {children}
    </LocaleContext.Provider>
  );
}

export function useLocale(): LocaleContextType {
  const context = useContext(LocaleContext);
  if (context === undefined) {
    throw new Error('useLocale must be used within a LocaleProvider');
  }
  return context;
}
